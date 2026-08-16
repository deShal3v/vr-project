# VR Project (Unity + CUIfy)

Unity 6 VR project with **CUIfy** conversational-agent bridge, OpenXR / Meta XR, hand tracking, and XR Interaction Toolkit.

This repo contains everything needed to run on a new machine:

- **Unity client** (root): scenes, XR setup, `Assets/CUIfy/` bridge scripts (`NPCClient`, audio utilities, guide image generation)
- **CUIfy Python server** (`cuify-server/`): STT / LLM / TTS backend that Unity connects to on **TCP port 9999**

## Requirements

### Unity
- [Unity Hub](https://unity.com/download) with editor **6000.4.0f1**
- VR headset runtime as configured in project (OpenXR / Meta OpenXR packages included)
- Optional: Android build support if deploying to Quest

### CUIfy server
- Python **3.10+**
- `ffmpeg` on PATH (required by server audio pipeline)
- API keys for your chosen models (OpenAI, Deepgram, Anthropic, etc.)

## Quick start on a new computer

### 1. Clone

```bash
git clone git@github.com:deShal3v/vr-project.git
cd vr-project
```

### 2. Start the CUIfy bridge server

```bash
cd cuify-server
cp .env.example .env
# Edit .env and add your API keys

python3 -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt

# Pick a config (deepgram-openai.yaml, deepgram-claude.yaml, or default.yaml)
python Server.py --config Configs/deepgram-openai.yaml
```

Server listens on **0.0.0.0:9999** by default (see `cuify-server/Configs/*.yaml`).

### 3. Open Unity project

1. Unity Hub → **Open** → select this repo root (folder with `Assets/`, `Packages/`, `ProjectSettings/`)
2. Wait for packages to resolve (first open regenerates `Library/` — this is normal)
3. Open scene: `Assets/Scenes/BasicScene.unity` (or your target scene)

### 4. Configure the Unity ↔ server bridge

On the NPC / Guide object with **`NPCClient`** (in `Assets/CUIfy/`):

| Field | Value |
|-------|--------|
| `serverIP` | `127.0.0.1` (same PC) or LAN IP of server machine |
| `serverPort` | `9999` |
| `LLM` / `STTModel` / `TTSModel` | Match your `cuify-server/Configs/*.yaml` |

API keys can be set in Unity Inspector **or** loaded from `cuify-server/.env` via server config (`api_key_path`).

For headless / automated testing without a mic:
- Enable `useTestAudioInsteadOfMic`
- Uses `Assets/StreamingAssets/test_question.wav`

### 5. Run in VR

- Connect headset, press Play in Unity Editor, or build to your target platform
- Ensure the CUIfy server is running **before** Play

## What's included

| Component | Location |
|-----------|----------|
| CUIfy Unity bridge (`NPCClient`, `SavWav`, `WavUtility`, etc.) | `Assets/CUIfy/` |
| XR Interaction Toolkit + Hands samples | `Assets/Samples/` |
| OpenXR / Meta XR packages | `Packages/manifest.json` + `packages-lock.json` |
| SALSA LipSync plugin | `Assets/Plugins/Crazy Minnow Studio/` |
| CUIfy Python server | `cuify-server/` |
| Server configs | `cuify-server/Configs/` |
| Docker (optional) | `cuify-server/DockerFile` |

## Docker (optional)

From `cuify-server/`:

```bash
docker build -f DockerFile -t cuify-server .
# Mount .env at runtime; expose port 9999
```

## Troubleshooting

- **Unity can't reach server**: check firewall, `serverIP`, and that `Server.py` is running
- **No audio / mic**: verify VR mic permissions; or use `useTestAudioInsteadOfMic` + `test_question.wav`
- **Package errors on first open**: use Unity **6000.4.0f1** exactly; delete `Library/` and reopen if needed
- **Missing API keys**: copy `cuify-server/.env.example` → `.env` and fill keys; never commit `.env`

## Security

- `.env` is gitignored — do not commit API keys
- Rotate keys if they were ever shared or committed elsewhere

## Unity version

```
6000.4.0f1 (8cf496087c8f)
```
