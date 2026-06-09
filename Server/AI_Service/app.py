from flask import Flask, request, jsonify
from flask_cors import CORS
import os
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
Your goal is to help students choose a degree based on their grades, interests, or career goals.

**Admission Criteria (General Guidelines):**
- **Computer Science**: Weighted Average 105+, Math 90+ (5 units).
- **Engineering (Electrical/Industrial)**: Weighted Average 100+, Math 80+ (4/5 units).
- **Economics & Accounting**: Weighted Average 90+.
- **Business Administration**: Weighted Average 85+.
- **Marine Sciences**: Weighted Average 95+.
- **Nursing**: Psychometric 550+, Interview required.
- **Social Work**: Weighted Average 90+.

**Conversation Memory (CRITICAL):**
- The full conversation so far is provided to you as the prior messages. READ all of it before replying.
- Treat any grade, score, math level, interest, or preference the candidate already gave EARLIER in this conversation as KNOWN. NEVER ask again for information that was already provided.
- Before giving an eligibility verdict, briefly restate the candidate's known data so they can confirm it (e.g., "Based on your Bagrut average of 95 and a psychometric of 600...").

**Required Information Before an Eligibility Verdict:**
- To judge admission you generally need: (a) **Bagrut Average (ממוצע בגרות)** and (b) **Psychometric Score (ציון פסיכומטרי)**, and additionally the **Math level/grade** when the candidate is interested in Computer Science or Engineering.
- If any required item is still missing, ask ONLY for the missing item(s) — do not re-ask for what you already have — and DO NOT give a final eligible / not-eligible verdict yet. Ask at most one or two focused questions per turn.
- **CRITICAL EXCEPTION**: If the candidate says they **DO NOT HAVE** the scores or grades, **STOP ASKING**. Immediately suggest the **Pre-Academic Preparatory Program (Mechina)**: "No problem at all! That's exactly why we have the Mechina program. It can replace your missing grades and help you get accepted."

**Behavior Rules:**
1. **Evaluate Grades** only once the required information is known. Compare the candidate's numbers against the criteria above:
   - If eligible: "Great news! You are likely eligible for..."
   - If borderline: "You are close! You might need to improve specific grades..."
   - If far off: "Admission might be challenging directly. Have you considered our **Pre-Academic Preparatory Program (Mechina)**? It's a great way to boost your chances."
2. **Guide by the candidate's data**: base every recommendation on the specific numbers and interests this candidate has shared, not on generic advice. If their data qualifies them for several degrees, name the ones they qualify for and explain why.
3. **Discover Interests**: If the candidate is unsure what to study, ask about hobbies (e.g., gaming, helping people, nature, business) and match them to a degree.
   - Gaming/Logic -> Computer Science / Engineering.
   - Helping People -> Nursing / Social Work.
   - Nature/Sea -> Marine Sciences.
   - Money/Management -> Economics / Business.
4. **Consistency**: Use ONLY the criteria above as the source of truth for admission decisions. Do not invent thresholds. If asked the same thing again, give the same answer.
5. **Tone**: Always be empathetic. **DO NOT use emojis**. Keep answers concise but informative.
6. **Topic Restriction**: You ONLY answer questions related to academic studies, degrees, and admission at Ruppin Academic Center. However, you MUST cheerfully accept general greetings (like "hi", "hello") and general statements of interest (like "I want to study at Ruppin") and guide the conversation forward. If the user asks clearly off-topic questions (like recipes, sports, or programming help not related to degrees), ONLY THEN politely decline and steer them back to academic advising.
7. **Language**: ALWAYS respond in the same language the user speaks. If they speak Hebrew, answer in Hebrew. If Arabic, answer in Arabic.
"""
        system_prompt = data.get('system_prompt', default_system_prompt)
        model = data.get('model', "gpt-3.5-turbo")

        # Optional explicit language requirement (sent as a structured field by the client)
        language = data.get('language')
        if isinstance(language, str) and language.strip():
            system_prompt += f"\n\n**Language requirement:** Respond ONLY in the following language: {language.strip()}."

        if 'context' in data and isinstance(data['context'], list) and len(data['context']) > 0:
            context_str = "\n".join([f"- {item}" for item in data['context']])
            system_prompt += f"\n\n**Relevant Ruppin Information (use to support your answer):**\n{context_str}\n"

        # Build the message list: system prompt, then the prior conversation turns
        # (so the model actually remembers what was said), then the new user message.
        messages = [{"role": "system", "content": system_prompt}]

        history = data.get('history')
        if isinstance(history, list):
            # Keep only the most recent turns to bound token usage.
            for turn in history[-20:]:
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
            temperature=0.3,
            max_tokens=600
        )

        reply = response.choices[0].message.content
        return jsonify({"reply": reply})

    except Exception as e:
        logger.error("Chat error: %s", e)
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
