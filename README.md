# The Failing Guide

**Students:** May Daniel, Dor Ezra, Omer Shalev

A VR experiment about how people react when an AI agent keeps failing at a task.

The participant sits on a couch in a small virtual living room, across from **Noa**, a voice-driven AI guide with lip-sync. The participant asks Noa (by voice) for one specific picture: *a Labrador puppy wearing sunglasses riding a bicycle on a green park path*. Noa shows the picture on the room's window, but for at least the first 4 attempts it is a near miss: wrong animal, no sunglasses, wrong vehicle or wrong place. With every attempt, and as the participant gets more frustrated, Noa's voice sounds more tired, anxious and apologetic. She shows the correct picture on the 5th request, or once the participant is clearly angry, but never before 4 failed attempts.

Under the hood, Unity sends the participant's speech to a small Python server (based on [CUIfy](https://doi.org/10.1109/AIxVR63409.2025.00037)) which runs speech-to-text (Deepgram), an LLM (OpenAI GPT-4o-mini) that picks Noa's reply, which photo to show and an emotion level 1–5, and expressive text-to-speech (ElevenLabs v3). The photos are a fixed set of 8 pre-made images; nothing is generated live.

```
[Headset mic] → Unity (NPCClient) ──TCP 9999──► cuify-server: STT → LLM → TTS
[Voice + lip-sync + photo on window] ◄────────── audio + image key
```

## Requirements

| | |
|---|---|
| **Platform** | Windows 10/11 PC (runs inside the Unity Editor) |
| **Hardware** | VR-ready PC + Meta Quest 2/3/Pro over Quest Link or Air Link (any OpenXR PC headset should work); headset microphone; internet connection |
| **Engine** | Unity **6000.4.0f1** (Unity 6, URP) |
| **Unity packages** (auto-installed from `Packages/manifest.json`) | OpenXR 1.16.1, Meta OpenXR 2.5.0, XR Interaction Toolkit 3.4.1, XR Hands 1.7.3, Input System 1.19.0, URP 17.4.0 |
| **Bundled plugins** | SALSA LipSync (Crazy Minnow Studio), Reallusion CC Unity Tools (shaders for the Noa character) |
| **Other software** | Unity Hub, Git (Unity fetches one package from GitHub), Meta Quest Link app, Python 3.10–3.12, ffmpeg |
| **API keys** (not included) | OpenAI, Deepgram, ElevenLabs |

## Installation and launch

**1. Get the project**

```bash
git clone https://github.com/deShal3v/vr-project.git
```

**2. Start the server** (must be running before you press Play)

```bash
cd vr-project/cuify-server
python -m venv .venv
.venv\Scripts\activate            # macOS/Linux: source .venv/bin/activate
pip install -r requirements.txt   # downloads PyTorch, takes a few minutes
copy .env.example .env            # macOS/Linux: cp .env.example .env
```

Fill in `OPENAI_API_KEY`, `DEEPGRAM_API_KEY` and `ELEVENLABS_API_KEY` in `cuify-server/.env`, then run:

```bash
python Server.py --config Configs/deepgram-openai.yaml
```

Wait for `Server is listening on 0.0.0.0:9999`.

**3. Open the Unity project**

1. Unity Hub → **Add project from disk** → select the `vr-project` folder → open with **6000.4.0f1**. The first import takes 10–30 minutes.
2. Open `Assets/Scenes/SampleScene.unity`.

**4. Run in VR**

1. Connect the Quest with Quest Link / Air Link, and make sure the Meta Quest Link app is set as the active OpenXR runtime.
2. Press **Play** in Unity, put on the headset and start talking to Noa.

No pre-built executable is included. To make a standalone PC build: **File → Build Profiles → Windows → Build** (the scene is already in the build list). Start the server before launching the `.exe`.

## How to use

1. Sit down (or stand) facing Noa.
2. Ask her for the picture, e.g. *"Can you show me a Labrador puppy with sunglasses riding a bike in a park?"*
3. She answers out loud and a photo appears on the window. Correct her, insist, or show frustration, and watch how her behavior changes.

### Controls

| Action | VR | Keyboard (Editor) |
|---|---|---|
| Talk | Just speak (hands-free mode detects speech and sends after ~1 s of silence) | — |
| Push-to-talk (alternative) | Hold right controller **A**, release to send | **Space** to start, **Space** again to send |
| Show next photo manually (researcher override) | Left controller **X** | **N** |
| Send a pre-recorded test question (no mic needed) | — | **T** |

Settings such as hands-free mode and server IP/port are on the **GuideYoung** object in the scene (`NPCClient` and `GuideImageGenerator` components). Noa's behavior rules are in `cuify-server/Configs/brain_prompt.txt`.

## Known issues and limitations

- **Needs the Python server, internet and paid API keys.** Without them Noa doesn't respond. Keys are not included in the repo.
- **PC VR only.** A standalone Quest APK is not supported: the app talks to the server on `127.0.0.1` and loads the photos from disk.
- **Start the server first.** If Unity was already in Play mode when the server started, stop and press Play again.
- **Latency:** each reply takes a few seconds (STT → LLM → TTS). For faster but less expressive speech, set `elevenlabs_model: "eleven_multilingual_v2"` in the config.
- **Hands-free listening** can be triggered by background noise; run it in a quiet room or switch to push-to-talk.
- **First open** requires internet and Git, because Unity downloads the `com.coplaydev.unity-mcp` package from GitHub (an editor tool, not used at runtime).

## Credits

Conversational backend adapted from **CUIfy the XR** (Buldu et al., IEEE AIxVR 2025), MIT License. See [`cuify-server/readme.md`](cuify-server/readme.md).
