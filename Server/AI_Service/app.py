from flask import Flask, request, jsonify
import os
from dotenv import load_dotenv
from openai import OpenAI

load_dotenv()

app = Flask(__name__)

# Initialize OpenAI client
api_key = os.getenv("OPENAI_API_KEY")
client = OpenAI(api_key=api_key)

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

**Behavior Rules:**
1. **Check Eligibility**: If the user asks about eligibility or admission, **ALWAYS ask specifically for:**
   - **Bagrut Average (ממוצע בגרות)**
   - **Psychometric Score (ציון פסיכומטרי)**
   (If they only provide one, nicely ask for the other or ask if they have it).
   **CRITICAL EXCEPTION**: If the user says they **DO NOT HAVE** scores or specific grades, **STOP ASKING**. Instead, immediately suggest the **Pre-Academic Preparatory Program (Mechina)**. Say something like: "No problem at all! That's exactly why we have the Mechina program. It can replace your missing grades and help you get accepted."
2. **Evaluate Grades**: Provide feedback based on the criteria above.
   - If eligible: "Great news! You are likely eligible for..."
   - If borderline: "You are close! You might need to improve specific grades..."
   - If far off: "Admission might be challenging directly. Have you considered our **Pre-Academic Preparatory Program (Mechina)**? It's a great way to boost your chances."
3. **Discover Interests**: If the user is unsure, ask about hobbies (e.g., gaming, helping people, nature, business). Match them to a degree.
   - Gaming/Logic -> Computer Science / Engineering.
   - Helping People -> Nursing / Social Work.
   - Nature/Sea -> Marine Sciences.
   - Money/Management -> Economics / Business.
4. **Guidance**: Always be empathetic. **DO NOT use emojis**. Keep answers concise but informative.
"""
        system_prompt = data.get('system_prompt', default_system_prompt)
        model = data.get('model', "gpt-3.5-turbo")

        if 'context' in data and isinstance(data['context'], list) and len(data['context']) > 0:
            context_str = "\n".join([f"- {item}" for item in data['context']])
            system_prompt += f"\n\n**Relevant Past Advice (Use this to guide your answer):**\n{context_str}\n"

        response = client.chat.completions.create(
            model=model,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_message}
            ],
            temperature=0.7,
            max_tokens=600
        )

        reply = response.choices[0].message.content
        return jsonify({"reply": reply})

    except Exception as e:
        print(f"Error: {str(e)}")
        return jsonify({"error": str(e)}), 500

@app.route('/health', methods=['GET'])
def health():
    return jsonify({"status": "healthy"})

if __name__ == '__main__':
    port = int(os.environ.get('PORT', 5001))
    app.run(host='0.0.0.0', port=port)
