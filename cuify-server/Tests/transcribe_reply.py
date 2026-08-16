"""Transcribe the fake_reply_part*.wav files so we can see what the agent said.

Also merges PCM data into a single playable WAV (Tests/tmp/fake_reply_merged.wav).
"""
import sys, wave
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from dotenv import dotenv_values

ROOT = Path(__file__).resolve().parent.parent.parent
SERVER_DIR = Path(__file__).resolve().parent.parent
TMP = SERVER_DIR / "tmp"

env = dotenv_values(ROOT / ".env")
dg_key = env.get("DEEPGRAM_API_KEY")

from SpeechToText.Deepgram import Deepgram_nova
stt = Deepgram_nova(dg_key)

parts = sorted(TMP.glob("fake_reply_part*.wav"), key=lambda p: int(p.stem.replace("fake_reply_part", "")))
print(f"Found {len(parts)} reply parts")

# --- transcribe each part ---
print("\nAgent reply transcribed:")
print("-" * 60)
full_text = []
for p in parts:
    text = stt.forward(p.read_bytes()).strip()
    full_text.append(text)
    print(f"[{p.name}] {text}")
print("-" * 60)
print(f"FULL REPLY: {' '.join(full_text)}")

# --- merge PCM (each chunk's WAV has same params; rewrite a single WAV) ---
merged = TMP / "fake_reply_merged.wav"
with wave.open(str(parts[0]), "rb") as w0:
    nchannels, sampwidth, framerate = w0.getnchannels(), w0.getsampwidth(), w0.getframerate()

with wave.open(str(merged), "wb") as out:
    out.setnchannels(nchannels)
    out.setsampwidth(sampwidth)
    out.setframerate(framerate)
    for p in parts:
        with wave.open(str(p), "rb") as wi:
            out.writeframes(wi.readframes(wi.getnframes()))

print(f"\nMerged playable WAV: {merged} ({merged.stat().st_size} bytes)")
