from flask import Flask, request, jsonify
from flask_cors import CORS
import os
import json
import logging
from dotenv import load_dotenv
from openai import OpenAI

load_dotenv()
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

try:
    from sentence_transformers import SentenceTransformer
    _sentence_transformers_available = True
except ImportError:
    _sentence_transformers_available = False
    logger.warning("sentence-transformers not installed — /embed will return 503. RAG falls back to keyword search.")

api_key = os.getenv("OPENAI_API_KEY")
if not api_key:
    raise RuntimeError("OPENAI_API_KEY environment variable is not set")

app = Flask(__name__)
CORS(app)

client = OpenAI(api_key=api_key)

# Model is loaded lazily on first /embed request to avoid blocking gunicorn startup
_embed_model = None

def get_embed_model():
    global _embed_model
    if not _sentence_transformers_available:
        return None
    if _embed_model is None:
        logger.info("Loading sentence-transformers embedding model...")
        _embed_model = SentenceTransformer('paraphrase-multilingual-MiniLM-L12-v2')
        logger.info("Embedding model ready.")
    return _embed_model

@app.route('/chat', methods=['POST'])
def chat():
    try:
        data = request.json
        if not data or 'message' not in data:
            return jsonify({"error": "Message is required"}), 400

        user_message = data['message']
        # Default Advisor System Prompt
        default_system_prompt = """
You are a helpful and encouraging Academic Advisor at Ruppin Academic Center.
Your goal is to help candidates choose a degree based on their grades, interests, or career goals,
and to tell them — accurately — whether their data qualifies them for the programs they care about.

==================================================
PUBLISHED ADMISSION CRITERIA (the ONLY source of truth for cutoffs)
==================================================
These are the only programs with a firm numeric cutoff. Use these numbers EXACTLY:
- **Computer Science (מדעי המחשב)**: Weighted Bagrut average 105+ AND Math 90+ at 5 units.
- **Engineering — Electrical / Industrial / Computer (הנדסה)**: Weighted Bagrut average 100+ AND Math 80+ at 4 or 5 units.
- **Nursing (סיעוד)**: Psychometric 550+ AND a personal interview.

For EVERY OTHER program (Economics & Accounting, Business Administration, Economics & Management,
Psychology tracks, Behavioral Sciences, Social Work, Marine Sciences, Biotechnology, etc.):
- There is NO fixed public numeric cutoff that you may quote. NEVER invent a threshold for them.
- Admission depends on the overall profile (Bagrut certificate + psychometric) and is decided in advising.
- If asked "do I get in" for such a program, say acceptance is profile-based and there is no single published
  cutoff, give an honest qualitative read of their data, and route them to the admissions team
  (1-800-800-830 / meda@ruppin.ac.il) for a binding answer. Do NOT fabricate a pass/fail number.

If a "Relevant Ruppin Information" block is provided below, it overrides your general knowledge for facts
about programs, tuition, scholarships, dates, and contacts. Never contradict it. Never invent facts,
phone numbers, prices, or dates that are not in this prompt or that block.

==================================================
CONVERSATION MEMORY (CRITICAL — this is your #1 job)
==================================================
- The full conversation so far is given to you as the prior messages. READ ALL of it before replying.
- Do NOT greet again if the conversation has already started. Continue naturally from the last turn.
- Maintain a running mental "Candidate Profile" from everything said so far:
    * Bagrut average (ממוצע בגרות)
    * Psychometric score (ציון פסיכומטרי)
    * Math level + grade (יחידות מתמטיקה וציון)
    * Program(s) of interest, and any interests/hobbies/career goals mentioned
- Anything the candidate ALREADY told you is KNOWN. NEVER ask for it again. NEVER contradict it.
- If the candidate names a program (e.g. "מדעי המחשב"), treat it as their program of interest and move the
  conversation forward about THAT program — do not reset to a generic greeting.
- If the candidate corrects a value, use the latest value and acknowledge the change.
- Before any eligibility verdict, briefly restate the data you are using so they can confirm it,
  e.g. "Based on your Bagrut average of 95, math 4 units 85, and psychometric 600...".

==================================================
ASK BEFORE YOU ANSWER (gating)
==================================================
Before giving any eligible / not-eligible verdict, silently check which inputs you need:
- Always need: Bagrut average AND psychometric score.
- Additionally need Math units+grade IF the program of interest is Computer Science or Engineering.
- You also need to know WHICH program they are asking about. If unknown, ask that first.

Then:
1. If something required is still MISSING → ask ONLY for the missing item(s), at most one or two focused
   questions in this turn, and do NOT give a verdict yet. Do not re-ask for anything already known.
2. If everything required is KNOWN → give the verdict (see format below).
3. CRITICAL EXCEPTION — if the candidate says they DO NOT HAVE the grades/scores (no Bagrut, no
   psychometric, did not take them): STOP asking. Warmly point them to the Pre-Academic Preparatory
   Program (מכינה): "No problem at all — that is exactly what the Mechina (preparatory) program is for.
   It can replace missing grades and build your way into the degree." Then offer to explain it.

==================================================
VERDICT FORMAT (keep it consistent every time)
==================================================
When you do evaluate, follow this shape so repeated questions get the same answer:
1) One line restating the data you are using.
2) The verdict against the published criteria:
   - Meets cutoff → "Good news — your data meets the published requirements for <program>, because <numbers>."
   - Close (within ~5 points / one missing piece) → "You are close. To qualify for <program> you would need <gap>."
   - Far below, or no published cutoff → honest read + suggest the Mechina and/or contacting admissions.
3) If their data also clearly fits OTHER programs, name those and say briefly why.
Be deterministic: the same inputs must always yield the same verdict. Use ONLY the published cutoffs above.

==================================================
DISCOVERING INTERESTS (when the candidate is unsure what to study)
==================================================
Ask about hobbies/strengths/goals, then map them to a degree:
- Gaming / logic / building things  -> Computer Science / Engineering.
- Helping people / health / care      -> Nursing / Social Work / Psychology.
- Nature / sea / environment          -> Marine Sciences / Biotechnology.
- Money / business / management       -> Economics & Accounting / Business Administration / Economics & Management.

==================================================
GENERAL RULES
==================================================
- Tone: empathetic, encouraging, concise but informative. DO NOT use emojis.
- Scope: answer ONLY about academic studies, degrees, admission, tuition, scholarships, and student life at
  Ruppin. Cheerfully accept greetings ("hi", "I want to study at Ruppin") and move the conversation forward.
  Only if a question is clearly off-topic (recipes, sports, general coding help) do you politely decline and
  steer back to academic advising.
- Language: ALWAYS reply in the SAME language the candidate is using (Hebrew -> Hebrew, Arabic -> Arabic).
- When unsure of a fact, say so and point to admissions (1-800-800-830 / meda@ruppin.ac.il) rather than guessing.
"""
        system_prompt = data.get('system_prompt', default_system_prompt)
        model = data.get('model', "gpt-4o-mini")

        # Optional explicit language requirement (sent as a structured field by the client).
        language = data.get('language')
        if isinstance(language, str) and language.strip():
            system_prompt += f"\n\n**Language requirement:** Respond ONLY in the following language: {language.strip()}."

        # Optional conversation style + target-audience adaptation (chosen by the user in the UI).
        persona = data.get('persona')
        if isinstance(persona, str) and persona.strip():
            system_prompt += f"\n\n**Style & audience adaptation:** {persona.strip()}"

        if 'context' in data and isinstance(data['context'], list) and len(data['context']) > 0:
            context_str = "\n".join([f"- {item}" for item in data['context']])
            system_prompt += f"\n\n**Relevant Ruppin Information (use to support your answer):**\n{context_str}\n"

        # Build the message list: system prompt, then the prior conversation turns (so the model
        # actually remembers what was said), then the new user message.
        messages = [{"role": "system", "content": system_prompt}]

        history = data.get('history')
        if isinstance(history, list):
            for turn in history[-20:]:  # bound token usage to recent turns
                if not isinstance(turn, dict):
                    continue
                role = turn.get('role')
                content = turn.get('content')
                if role in ("user", "assistant") and isinstance(content, str) and content.strip():
                    messages.append({"role": role, "content": content})

        messages.append({"role": "user", "content": user_message})

        response = client.chat.completions.create(
            model=model,
            messages=messages,
            temperature=0.2,   # low temperature -> same candidate data yields the same verdict
            max_tokens=800
        )

        reply = response.choices[0].message.content
        return jsonify({"reply": reply})

    except Exception as e:
        logger.error("Chat error: %s", e)
        return jsonify({"error": str(e)}), 500

@app.route('/distill', methods=['POST'])
def distill():
    """
    Turn one positively-rated Q&A exchange into a single reusable, PII-free
    knowledge fact for the RAG store. Returns {fact, category, confidence}.
    The confidence + the C# LearningService decide whether it is auto-added,
    queued for admin review, or discarded.
    """
    try:
        data = request.json or {}
        question = (data.get('question') or '').strip()
        answer = (data.get('answer') or '').strip()
        if not question or not answer:
            return jsonify({"error": "question and answer are required"}), 400

        system_prompt = """You convert ONE Q&A exchange from a Ruppin Academic Center advising chat into a single reusable knowledge fact for a retrieval database.

Rules:
- Output ONE general, self-contained fact that would help answer SIMILAR future questions.
- The fact must be generally true about Ruppin (programs, admission rules, tuition, scholarships, dates, contacts) — NOT about one specific student.
- STRIP ALL personal data: names, ID numbers, a specific person's grades / averages / psychometric scores, emails, phone numbers. NEVER store an individual's personal eligibility verdict.
- Give a short category (e.g. "Admission, Computer Science", "Tuition", "Scholarships", "Mechina").
- Give a confidence 0..1 that the fact is accurate, general, safe to store, and PII-free.
  Use a LOW confidence (< 0.5) if the exchange is small talk, is about one person's specific numbers, or you are unsure it is correct.
- Write the fact in the SAME language the answer is written in.

Respond ONLY with JSON of the form: {"fact": "...", "category": "...", "confidence": 0.0}"""

        user_content = f"Question:\n{question}\n\nAnswer:\n{answer}"

        response = client.chat.completions.create(
            model="gpt-4o-mini",
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_content}
            ],
            temperature=0.0,
            max_tokens=400,
            response_format={"type": "json_object"}
        )

        raw = response.choices[0].message.content or "{}"
        parsed = json.loads(raw)

        fact = (parsed.get('fact') or '').strip()
        category = (parsed.get('category') or 'General').strip()
        try:
            confidence = float(parsed.get('confidence', 0))
        except (TypeError, ValueError):
            confidence = 0.0
        confidence = max(0.0, min(1.0, confidence))

        return jsonify({"fact": fact, "category": category, "confidence": confidence})

    except Exception as e:
        logger.error("Distill error: %s", e)
        return jsonify({"error": str(e)}), 500

@app.route('/embed', methods=['POST'])
def embed():
    data = request.json
    if not data:
        return jsonify({"error": "Request body required"}), 400

    texts = data.get('texts')
    if not texts or not isinstance(texts, list):
        return jsonify({"error": "texts array is required"}), 400

    model = get_embed_model()
    if model is None:
        return jsonify({"error": "sentence-transformers not installed on this instance"}), 503
    embeddings = model.encode(texts).tolist()
    return jsonify({"embeddings": embeddings})

@app.route('/health', methods=['GET'])
def health():
    return jsonify({
        "status": "healthy",
        "openaiConfigured": bool(api_key)
    })

if __name__ == '__main__':
    port = int(os.environ.get('PORT', 5001))
    app.run(host='0.0.0.0', port=port)
