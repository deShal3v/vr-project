# AGENTS.md — VR Project (Unity + CUIfy)

Instructions for coding agents (Claude Code, Cursor, etc.) working in this repository.

## What this project is

A **Unity 6 VR application** with a **CUIfy** conversational NPC: speech-to-text → LLM → text-to-speech, driven by a **Python TCP server** that Unity talks to on port **9999**.

- **Unity client** (repo root): VR scene, OpenXR / Meta XR, XR Interaction Toolkit, hand tracking, lip sync (SALSA)
- **CUIfy bridge** (`Assets/CUIfy/`): `NPCClient.cs` and related scripts connect Unity to the server
- **CUIfy server** (`cuify-server/`): Python backend for STT, LLM, TTS

**Primary scene:** `Assets/Scenes/BasicScene.unity`  
**Alternate scene:** `Assets/Scenes/SampleScene.unity`

---

## Hard requirements (do not guess versions)

| Component | Required version / note |
|-----------|-------------------------|
| **Unity Editor** | **6000.4.0f1** exactly (`ProjectSettings/ProjectVersion.txt`, changeset `8cf496087c8f`) |
| **Python** | **3.10 – 3.12** (avoid 3.14; `torch` may not install) |
| **ffmpeg** | On system PATH (audio pipeline) |
| **CUIfy server port** | **9999** TCP |
| **Disk space** | ~15–20 GB free for Unity install + first `Library/` import |

### Unity Hub modules

- **Always:** Editor `6000.4.0f1`
- **Quest / Android builds:** Android Build Support (+ NDK if prompted)
- **Windows dev PC:** Windows Build Support (usually default)

Packages are restored from `Packages/manifest.json` and `Packages/packages-lock.json` on first open. Do **not** upgrade Unity or packages unless the user explicitly asks.

### Key Unity packages (from manifest)

- URP `17.4.0`
- OpenXR `1.16.1`, Meta OpenXR `2.5.0`, Android XR OpenXR `1.2.0`
- XR Interaction Toolkit `3.4.1`, XR Hands `1.7.3`
- Input System `1.19.0`
- `com.coplaydev.unity-mcp` (git URL — needs network on first Unity open)

---

## Repository layout

```
vr-project/
├── Assets/
│   ├── CUIfy/              # Unity ↔ server bridge (NPCClient, audio utils, guide images)
│   ├── Scenes/             # BasicScene.unity (main), SampleScene.unity
│   ├── StreamingAssets/    # test_question.wav for mic-less testing
│   ├── Samples/            # XR Hands + XRI sample assets
│   ├── Plugins/            # SALSA LipSync
│   └── ...
├── Packages/               # manifest.json, packages-lock.json
├── ProjectSettings/
├── cuify-server/           # Python CUIfy backend
│   ├── Server.py           # Entry point
│   ├── ClientListener.py   # TCP protocol with Unity
│   ├── Configs/              # YAML configs (STT/LLM/TTS models)
│   ├── .env                  # API keys (present in this repo)
│   ├── .env.example
│   └── requirements.txt
├── scripts/                # Optional helpers (Mac-oriented; adapt on Windows)
├── README.md
└── AGENTS.md               # This file
```

**Gitignored / generated (never commit):** `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `cuify-server/.venv/`, `cuify-server/logs/`, `cuify-server/tmp/`

---

## First-time setup on a new machine

### 1. Clone

```bash
git clone https://github.com/deShal3v/vr-project.git
cd vr-project
```

### 2. Install system dependencies

- **Unity Hub** + editor **6000.4.0f1**
- **ffmpeg** on PATH  
  - Windows: `winget install ffmpeg`  
  - macOS: `brew install ffmpeg`
- **Python 3.10–3.12**

### 3. Python virtualenv + CUIfy dependencies

**Windows (PowerShell):**

```powershell
cd cuify-server
python -m venv .venv
.\.venv\Scripts\activate
pip install --upgrade pip
pip install -r requirements.txt
```

**macOS / Linux:**

```bash
cd cuify-server
python3.12 -m venv .venv
source .venv/bin/activate
pip install --upgrade pip
pip install -r requirements.txt
```

**Note:** First `pip install` downloads **PyTorch** and other large packages (~2–4 GB, several minutes).

**API keys:** `cuify-server/.env` should already exist. If missing, copy `.env.example` → `.env` and fill keys. Configs read via `api_key_path: "../.env"` in YAML files.

### 4. Open Unity project

1. Unity Hub → **Open** → select **repo root** (folder containing `Assets/`, `Packages/`, `ProjectSettings/`)
2. Choose editor **6000.4.0f1**
3. Wait for package resolve and `Library/` generation (first open: 10–30+ minutes)
4. Open `Assets/Scenes/BasicScene.unity`

---

## How to run (every session)

**Order matters:**

1. **Start CUIfy server** (keep terminal open):

   ```bash
   cd cuify-server
   # activate venv (see above)
   python Server.py --config Configs/deepgram-openai.yaml
   ```

   Other configs:
   - `Configs/deepgram-claude.yaml` — Deepgram STT + Anthropic Claude
   - `Configs/default.yaml` — OpenAI whisper + GPT-3.5 + OpenAI TTS

   Server binds `0.0.0.0:9999` (see YAML `host` / `port`).

2. **Unity Editor** → open `BasicScene.unity` → connect VR headset → **Play**

3. **Verify bridge** on the Guide/NPC GameObject with `NPCClient`:
   - `serverIP` = `127.0.0.1` when server runs on same machine
   - `serverPort` = `9999`
   - `LLM`, `STTModel`, `TTSModel` enums should match the active YAML config

### Test without microphone

On `NPCClient` in the Inspector:

- `useTestAudioInsteadOfMic` = true
- `testAudioFileName` = `test_question.wav` (in `Assets/StreamingAssets/`)
- Optional: `autoSendTestOnConnect` = true

---

## Architecture: Unity ↔ CUIfy

```
[VR Mic / test WAV] → NPCClient (Unity) --TCP:9999--> Server.py
                                                          ├─ STT (e.g. Deepgram)
                                                          ├─ LLM (e.g. OpenAI / Claude)
                                                          └─ TTS (e.g. Deepgram / ElevenLabs)
[Audio playback + lip sync] ← NPCClient ←────────────── WAV bytes
```

**Key scripts (`Assets/CUIfy/`):**

| File | Role |
|------|------|
| `NPCClient.cs` | Main bridge: records audio, sends to server, plays response |
| `SavWav.cs` / `WavUtility.cs` | WAV encode/decode |
| `AgentAnimation.cs` | NPC animation hooks |
| `GuideImageGenerator.cs` | Optional guide image generation (OpenAI) |
| `CharacterSelectMenu.cs` | Character selection UI |

**Server entry:** `cuify-server/Server.py`  
**Protocol / socket handling:** `cuify-server/ClientListener.py`

Default production config `Configs/deepgram-openai.yaml`:

- STT: `Deepgram_nova`
- LLM: `OpenAI_gpt4o_mini` (streaming)
- TTS: `Deepgram_aura` (with ElevenLabs override in YAML)

---

## What agents should / should not do

### Do

- Use Unity **6000.4.0f1** only
- Start `Server.py` before testing Play mode
- Match `NPCClient` model enums to the active server YAML
- Edit C# in `Assets/CUIfy/` and Python in `cuify-server/` for feature work
- Use `Configs/*.yaml` and `Configs/brain_prompt.txt` for persona / model changes
- Regenerate `Library/` by opening Unity — never commit `Library/`

### Do not

- Upgrade Unity editor or bump package versions without user approval
- Commit `Library/`, `Temp/`, `.venv/`, logs, or build artifacts
- Commit new secrets; treat `.env` as sensitive even if present in repo
- Assume Tailscale or remote mic/RDP — user may develop locally on one PC
- Run PowerShell mic scripts from macOS unless targeting a remote Windows host via SSH

---

## Troubleshooting

| Symptom | Likely fix |
|---------|------------|
| Unity package errors | Confirm **6000.4.0f1**; delete `Library/` and reopen project |
| `NPCClient` silent / no response | Is `Server.py` running? Firewall blocking **9999**? `serverIP` correct? |
| Empty / tiny WAV recordings | Mic permissions; on Windows run in interactive session, not headless SSH |
| `pip install` fails on SSL (macOS) | MATLAB `DYLD_LIBRARY_PATH` conflict — use `env -u DYLD_LIBRARY_PATH pip ...` or Python 3.12 from Homebrew |
| Server import errors | Activate `.venv`; `pip install -r requirements.txt` |
| Quest build fails | Install **Android Build Support** in Unity Hub |
| git MCP package fails | Unity needs internet on first open to fetch `com.coplaydev.unity-mcp` |

---

## Optional: Docker

From `cuify-server/`:

```bash
docker build -f DockerFile -t cuify-server .
# Run with .env mounted, expose port 9999
```

GPU/CUDA variant: `DockerFileWithCUDA`

---

## Optional: helper scripts (macOS)

```bash
./scripts/start-cuify-server.sh [Configs/deepgram-openai.yaml]
./scripts/open-unity.sh
```

On **Windows**, run the PowerShell/Python commands in this doc directly instead.

---

## Quick agent checklist

- [ ] Unity Hub + **6000.4.0f1** installed
- [ ] Repo cloned
- [ ] `cuify-server/.venv` created, `pip install -r requirements.txt` succeeded
- [ ] `ffmpeg` on PATH
- [ ] `python Server.py --config Configs/deepgram-openai.yaml` listening on 9999
- [ ] Unity project opened at repo root; `BasicScene.unity` loaded
- [ ] `NPCClient.serverIP` = `127.0.0.1`, `serverPort` = `9999`
- [ ] CUIfy server running **before** Unity Play

---

## References

- Human-oriented overview: `README.md`
- CUIfy paper / citation: `cuify-server/readme.md`
- Guide images: `Assets/CUIfy/GuideImageGeneration_README.md`
- GitHub: https://github.com/deShal3v/vr-project
