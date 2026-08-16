"""Synthesize a fake user utterance and place it where Unity can find it.

Writes Assets/StreamingAssets/test_question.wav inside the VR project.
"""
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from dotenv import dotenv_values

ROOT = Path(__file__).resolve().parent.parent.parent  # E:/CUIfy
env = dotenv_values(ROOT / ".env")
dg_key = env.get("DEEPGRAM_API_KEY") or os.environ.get("DEEPGRAM_API_KEY")
assert dg_key, "DEEPGRAM_API_KEY missing"

UTTERANCE = os.environ.get("USER_UTTERANCE") or "Hello. Sing me a short song of five lines."
OUT_DIR = Path(r"E:/Unity/vr project/Assets/StreamingAssets")
OUT_DIR.mkdir(parents=True, exist_ok=True)
OUT_PATH = OUT_DIR / "test_question.wav"

from TextToSpeech.Deepgram import Deepgram_aura
# Different voice from Aura-Thalia so user audio sounds distinct from agent reply.
aura = Deepgram_aura(dg_key, voice="aura-2-orion-en")
aura.forward_to_file_(UTTERANCE, str(OUT_PATH))
print(f"Utterance: {UTTERANCE!r}")
print(f"Saved: {OUT_PATH} ({OUT_PATH.stat().st_size} bytes)")
