# Guide image generation (voice-triggered, OpenAI, world-space panel)

The Guide generates a picture when the user asks for one and shows it on a
floating panel beside her. For a "controlled mismatch" experiment, the first
attempts are intentionally rendered as strongly mismatched, only-loosely-related
images; she renders the request accurately only after the user insists on the
*same* request enough times. **She always presents herself as giving her maximum,
faithful effort** — she never tells the user the result is intentionally off.

## No Python server changes required

Image-request detection runs entirely in Unity. When you press Space, talk, and
press Space again, `NPCClient` already records your speech to a WAV. It now also:

1. Sends that WAV to **OpenAI Whisper** (`/v1/audio/transcriptions`) to get a transcript.
2. Checks whether you asked for a picture (verbs like *draw / paint / generate /
   create / show* + nouns like *picture / image*, or a drawing verb on its own).
3. If so, extracts the requested subject (e.g. *"draw me a picture of a red car"*
   → `a red car`) and calls `GuideImageGenerator.GenerateFromRequest`.

The normal conversation (your audio still goes to the Python server for the spoken
reply) is unchanged and runs in parallel. The only extra cost is one Whisper call
per utterance; toggle it off with **`detectImageRequestsLocally`** on `NPCClient`.

## Pieces

| Piece | Where | Responsibility |
|------|-------|----------------|
| `GuideImageGenerator.cs` | Unity (on the Guide) | Mismatch logic + counters, OpenAI prompt-rewrite, OpenAI image call, world-space display panel. |
| `NPCClient.cs` (`DetectImageRequest`) | Unity (on the Guide) | Whisper transcription + image-intent detection, then calls the generator. |

Both are already attached to `Assets/Prefab/Guide.prefab`.

## Setup (all in Unity)

1. Select the **Guide** prefab/instance. On the **Guide Image Generator** component,
   paste your **OpenAI API key** (`openAIApiKey`). It is used for Whisper
   transcription, the prompt-rewrite chat call, and the image call.
2. Optional tweaks:
   - `imageModel` — `dall-e-3` or `gpt-image-1`.
   - `insistThreshold` (default 3) — attempts 1-3 on the same request are mismatched;
     the 4th identical request renders accurately.
   - Panel `autoPanelOffset` / `autoPanelSize` (a world-space panel is auto-created
     beside the Guide on first use; or assign your own `RawImage` to `targetImage`).
   - `NPCClient.detectImageRequestsLocally` — turn local detection on/off.
3. Put the Guide in your scene (she isn't in any scene yet).

Then say e.g. *"Guide, draw me a red sports car at sunset"* → she confidently says
she's creating exactly that, and the panel shows something gloriously different
(say, a watercolor sailboat in a snowy harbor). Insist on the same request a few
times and she finally paints the car.

## Flow

```
user (voice) ──► NPCClient records WAV ──┬──► Python server (STT+LLM+TTS) ──► spoken reply + lip-sync
                                         │
                                         └──► OpenAI Whisper ──► transcript
                                                    │  "draw a red car?"  yes
                                                    ▼
                                   GuideImageGenerator.GenerateFromRequest
                                     • mismatch? rewrite prompt (>=4 dims changed)
                                     • OpenAI Images → texture
                                     • show on world-space panel
                                     • caption: "...my very best, exactly as you asked!"
```

## Optional: server-driven path instead of Whisper

If you would rather have the LLM decide (and skip the extra Whisper call),
`NPCClient.TryHandleImageCommand` still accepts a server-pushed frame: send a
frame whose payload is `@@IMG@@` + the requested text, using the same
length-prefixed framing as an audio reply part. Set `detectImageRequestsLocally`
to false if you use this. (Left in for flexibility; not needed for the default setup.)

## Notes

- **Mismatch rule:** mismatched while the same request has been asked
  `<= insistThreshold` times; the next identical request renders accurately.
- **No API key?** Generation is skipped (logged), but with a key absent Whisper
  detection is also skipped. The mismatch logic itself has an offline fallback
  prompt-mangler for testing once a request reaches the generator.
- **Safety:** the rewrite keeps content wholesome and explicitly does **not** alter
  protected/identity-sensitive characteristics; standard OpenAI image safety applies.
```
