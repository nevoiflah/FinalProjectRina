"""
robot_client.py — Reference client for physical/virtual robot integration.

Demonstrates how any robot (NAO, Pepper, Raspberry Pi, ROS node, etc.)
can use the Ruppin Academic Advisor wrapper over a standard HTTP interface.
No robot-specific SDK is required on the server side — the wrapper is
hardware-agnostic by design.

Usage:
    python robot_client.py --api https://rina-api-05111937.azurewebsites.net
"""

import argparse
import sys
import tempfile
import os

try:
    import requests
except ImportError:
    sys.exit("Install requests: pip install requests")


class RobotWrapperClient:
    """
    Thin HTTP client that a robot's control software would embed.
    The three methods map 1-to-1 to the wrapper's REST endpoints:
        STT  ->  POST /api/stt
        Chat ->  POST /api/chat
        TTS  ->  POST /api/tts
    """

    def __init__(self, api_base_url: str, user_id: str):
        self.base = api_base_url.rstrip("/")
        self.user_id = user_id
        self.session = requests.Session()

    # ------------------------------------------------------------------ #
    # 1. Speech-to-Text: send raw audio bytes, receive transcript string  #
    # ------------------------------------------------------------------ #
    def transcribe(self, audio_path: str, language: str = "he") -> str:
        with open(audio_path, "rb") as f:
            response = self.session.post(
                f"{self.base}/api/stt",
                files={"audio": ("recording.webm", f, "audio/webm")},
                data={"language": language},
            )
        response.raise_for_status()
        return response.json()["transcript"]

    # ------------------------------------------------------------------ #
    # 2. Chat: send user text, receive AI reply string                    #
    # ------------------------------------------------------------------ #
    def chat(self, message: str) -> str:
        response = self.session.post(
            f"{self.base}/api/chat",
            json={"message": message, "userId": self.user_id},
        )
        response.raise_for_status()
        return response.json()["reply"]

    # ------------------------------------------------------------------ #
    # 3. Text-to-Speech: send text, receive MP3 bytes                     #
    # ------------------------------------------------------------------ #
    def synthesize(self, text: str) -> bytes:
        response = self.session.post(
            f"{self.base}/api/tts",
            json={"text": text},
        )
        response.raise_for_status()
        return response.content

    # ------------------------------------------------------------------ #
    # Convenience: full voice turn (STT -> Chat -> TTS)                   #
    # ------------------------------------------------------------------ #
    def voice_turn(self, audio_path: str, language: str = "he") -> tuple[str, bytes]:
        """
        One complete interaction cycle for a robot:
          1. Transcribe the user's speech from an audio file.
          2. Send the transcript to the AI.
          3. Synthesize the AI reply into audio.
        Returns (reply_text, reply_audio_bytes).
        """
        transcript = self.transcribe(audio_path, language)
        print(f"[STT] Transcript: {transcript}")

        reply_text = self.chat(transcript)
        print(f"[AI]  Reply:      {reply_text}")

        reply_audio = self.synthesize(reply_text)
        print(f"[TTS] Audio:      {len(reply_audio)} bytes")

        return reply_text, reply_audio


# --------------------------------------------------------------------------- #
# Demo: simulate a robot interaction using a local WAV/WebM file               #
# --------------------------------------------------------------------------- #
def main():
    parser = argparse.ArgumentParser(description="Ruppin Wrapper — robot demo client")
    parser.add_argument("--api", default="http://localhost:5102", help="API base URL")
    parser.add_argument("--user", default="robot-demo-user", help="User ID")
    parser.add_argument("--audio", default=None, help="Path to audio file for STT demo")
    parser.add_argument("--text", default="שלום, אני רוצה ללמוד מדעי המחשב", help="Text to send directly (skips STT)")
    args = parser.parse_args()

    client = RobotWrapperClient(api_base_url=args.api, user_id=args.user)

    if args.audio:
        reply_text, reply_audio = client.voice_turn(args.audio)
        out_path = tempfile.mktemp(suffix=".mp3")
        with open(out_path, "wb") as f:
            f.write(reply_audio)
        print(f"[Robot] Playing reply audio from: {out_path}")
        # On a real robot: robot.say(reply_text)  or  play_audio(out_path)
    else:
        print(f"[Robot] Sending text: {args.text}")
        reply = client.chat(args.text)
        print(f"[Robot] AI reply:    {reply}")
        audio = client.synthesize(reply)
        print(f"[Robot] TTS audio:   {len(audio)} bytes — ready to play on robot speaker")


if __name__ == "__main__":
    main()
