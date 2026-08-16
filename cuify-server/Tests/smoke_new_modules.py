"""Smoke test for the three new provider modules.

Validates:
1. All modules import cleanly.
2. Deepgram TTS produces a real WAV from text (live API call).
3. Deepgram STT transcribes that WAV back (live API call, round-trip).
4. Anthropic module imports & instantiates (live call only if ANTHROPIC_API_KEY is set).

Run from E:/CUIfy/Server with: ../.venv/Scripts/python.exe Tests/smoke_new_modules.py
"""
import os
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from dotenv import dotenv_values

env = dotenv_values(Path(__file__).resolve().parent.parent.parent / ".env")
dg_key = env.get("DEEPGRAM_API_KEY") or os.environ.get("DEEPGRAM_API_KEY")
anth_key = env.get("ANTHROPIC_API_KEY") or os.environ.get("ANTHROPIC_API_KEY")

print(f"Deepgram key present: {bool(dg_key)}")
print(f"Anthropic key present: {bool(anth_key)}")

print("\n[1/4] Importing modules...")
from SpeechToText import getSpeechToText
from TextToSpeech import getTextToSpeech
from LargeLanguageModels import getLargeLanguageModel
print("  OK")

if not dg_key:
    print("\nSKIP: Deepgram tests need DEEPGRAM_API_KEY in .env")
    sys.exit(1)

print("\n[2/4] Deepgram TTS (Aura) — synthesizing test phrase...")
tts = getTextToSpeech("Deepgram_aura", deepgram_api_key=dg_key)
out_path = Path(__file__).resolve().parent.parent / "tmp" / "smoke_aura.wav"
out_path.parent.mkdir(parents=True, exist_ok=True)
t0 = time.time()
tts.forward_to_file("Hello from the VR agent prototype. This is a Deepgram round trip test.", str(out_path))
print(f"  Wrote {out_path} ({out_path.stat().st_size} bytes) in {time.time()-t0:.2f}s")

print("\n[3/4] Deepgram STT (Nova) — transcribing the file we just wrote...")
stt = getSpeechToText("Deepgram_nova", deepgram_api_key=dg_key)
with open(out_path, "rb") as f:
    audio_bytes = f.read()
t0 = time.time()
transcript = stt.forward(audio_bytes)
print(f"  Transcript: {transcript!r} ({time.time()-t0:.2f}s)")

print("\n[4/4] Anthropic Claude...")
if not anth_key:
    print("  SKIP: ANTHROPIC_API_KEY not set — module instantiation only")
    llm = None
    try:
        llm = getLargeLanguageModel("Anthropic_claude-sonnet-4-6", anthropic_api_key="dummy", stream=False)
        print(f"  Instantiation OK: {llm.__class__.__name__}")
    except Exception as e:
        print(f"  Instantiation FAILED: {e}")
else:
    llm = getLargeLanguageModel("Anthropic_claude-sonnet-4-6", anthropic_api_key=anth_key, stream=False)
    t0 = time.time()
    response = llm.forward("Say hi in 5 words.", history=[], preprompt="You are terse.")
    print(f"  Claude response: {response!r} ({time.time()-t0:.2f}s)")

print("\nDone.")
