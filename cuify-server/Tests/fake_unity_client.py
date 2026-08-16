"""Headless replacement for NPCClient.cs.

Speaks the CUIfy wire protocol against a running server (default 127.0.0.1:9999):
    1. Synthesize a user question with Deepgram Aura.
    2. Connect, send config string, wait for !OK[K] handshake.
    3. Send the WAV as a single final-flag chunk.
    4. Receive the reply (handles both streamed and non-streamed responses).
    5. Save reply to tmp/fake_reply.wav.

Usage:
    cd E:/CUIfy/Server
    .venv/Scripts/python.exe Tests/fake_unity_client.py

Optional env: USER_UTTERANCE to override the question.
"""
import os
import socket
import struct
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from dotenv import dotenv_values

ROOT = Path(__file__).resolve().parent.parent.parent  # E:/CUIfy
SERVER_DIR = Path(__file__).resolve().parent.parent  # E:/CUIfy/Server
TMP = SERVER_DIR / "tmp"
TMP.mkdir(parents=True, exist_ok=True)

env = dotenv_values(ROOT / ".env")
dg_key = env.get("DEEPGRAM_API_KEY") or os.environ.get("DEEPGRAM_API_KEY")
if not dg_key:
    print("ERROR: DEEPGRAM_API_KEY missing — needed only to synthesize the fake user utterance.")
    sys.exit(1)

HOST = "127.0.0.1"
PORT = 9999
STT = "Deepgram_nova"
LLM = "OpenAI_gpt4o_mini_stream"  # matches Unity inspector default
TTS = "Deepgram_aura"
STORE = "store"
VOICE = "alloy"  # unused for Deepgram TTS path
PROMPT = "You are a friendly conversational companion sitting on a couch across from the user in a small living room. Keep replies brief and natural - one to three sentences. Avoid monologues and markdown. React to what the user says."

USER_UTTERANCE = os.environ.get("USER_UTTERANCE") or "Hi there. What do you like to do for fun on a Sunday afternoon?"

# --- 1. synth user audio ---
print(f"[1] Synthesizing user utterance: {USER_UTTERANCE!r}")
from TextToSpeech.Deepgram import Deepgram_aura
aura = Deepgram_aura(dg_key, voice="aura-2-orion-en")  # different voice so reply is distinct
user_wav = TMP / "fake_user_question.wav"
t0 = time.time()
aura.forward_to_file_(USER_UTTERANCE, str(user_wav))
print(f"    wrote {user_wav} ({user_wav.stat().st_size} bytes) in {time.time()-t0:.2f}s")

# --- 2. connect & handshake ---
print(f"[2] Connecting to {HOST}:{PORT} ...")
sock = socket.create_connection((HOST, PORT), timeout=30)
config_str = f"{STT},{LLM},{TTS},,,,,{STORE},{VOICE},{PROMPT}"
cfg_bytes = config_str.encode("ascii")
sock.sendall(struct.pack("<I", len(cfg_bytes)) + cfg_bytes)
print(f"    sent config ({len(cfg_bytes)} bytes)")

handshake = sock.recv(4)
print(f"    handshake response: {handshake!r}")
if not handshake.startswith(b"!OK"):
    print(f"    ERROR: expected !OK[K], got {handshake!r}")
    sock.close()
    sys.exit(1)

# --- 3. send audio chunk (single final chunk) ---
audio_bytes = user_wav.read_bytes()
final_flag = b"\x01"
sock.sendall(struct.pack("<I", len(audio_bytes)) + final_flag + audio_bytes)
print(f"[3] Sent audio chunk ({len(audio_bytes)} bytes), final_flag=1")

# --- 4. receive reply ---
print("[4] Receiving reply...")
sock.settimeout(60)

def recv_exact(n):
    buf = b""
    while len(buf) < n:
        chunk = sock.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("server closed")
        buf += chunk
    return buf

is_stream = "_stream" in LLM
reply_chunks = []
chunk_idx = 0
while True:
    length_bytes = recv_exact(4)
    length = struct.unpack("<I", length_bytes)[0]
    if is_stream:
        flag = recv_exact(1)[0]
    else:
        flag = 1  # non-stream sends one chunk, treat as final
    if length > 0:
        payload = recv_exact(length)
        chunk_idx += 1
        out_chunk = TMP / f"fake_reply_part{chunk_idx}.wav"
        out_chunk.write_bytes(payload)
        reply_chunks.append(payload)
        print(f"    part {chunk_idx}: {length} bytes (flag={flag}) -> {out_chunk}")
    if flag == 1:
        break

# --- 5. concat reply ---
if reply_chunks:
    combined = TMP / "fake_reply.wav"
    # Naive: just save the first part as the canonical reply (each Deepgram chunk is a
    # standalone WAV; concat'ing WAV containers byte-wise gives an invalid file).
    # For listening, play the parts in order. We also save part1 as the primary reply.
    combined.write_bytes(reply_chunks[0])
    print(f"\nPRIMARY REPLY: {combined}  ({combined.stat().st_size} bytes)")
    print(f"ALL PARTS:    {[str(TMP / f'fake_reply_part{i+1}.wav') for i in range(len(reply_chunks))]}")
    print("\nPlay them in order to hear the agent's response.")
else:
    print("\nNo audio received in reply.")

sock.close()
print("\nDone.")
