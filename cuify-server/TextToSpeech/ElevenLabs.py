import json
import wave
import urllib.request
import urllib.error
from .Base import Text2Speech


class ElevenLabs(Text2Speech):
    """
    ElevenLabs TTS via the HTTP API (no SDK dependency).

    Delivery is driven by `emotion_level` (1-5), set per turn by the caller. The level
    maps to three things that together read as growing FATIGUE in the same voice:
      - stability  : lower  => less controlled, more wavering
      - style      : higher => more emotive
      - speed      : lower  => slower, heavier, worn-out delivery
    On models that support audio tags (eleven_v3), a short tag prefix like "[tired]" is
    prepended to push the performance further. Set use_audio_tags=False if the voice ever
    reads the tags aloud instead of performing them.
    """

    # emotion_level -> (stability, style, speed, audio_tags)
    EMOTION_SETTINGS = {
        1: (0.75, 0.00, 1.00, ""),                       # fresh, calm, helpful
        2: (0.55, 0.20, 0.97, "[sighs] "),               # slightly worn, mildly apologetic
        3: (0.40, 0.40, 0.93, "[tired] "),               # clearly fatigued / anxious
        4: (0.28, 0.60, 0.90, "[exhausted][sighs] "),    # worn out, fearful, pleading
        5: (0.18, 0.80, 0.86, "[weary][emotional] "),    # drained, near-crying
    }

    def __init__(self, api_key, voice_id="21m00Tcm4TlvDq8ikWAM",
                 model_id="eleven_multilingual_v2", sample_rate=24000,
                 use_audio_tags=None):
        self.api_key = api_key
        self.voice_id = voice_id
        self.model_id = model_id
        self.sample_rate = sample_rate
        self.emotion_level = 1  # set per-turn by ClientListener before each call
        # Audio tags are only interpreted by v3-class models.
        self.use_audio_tags = ("v3" in model_id) if use_audio_tags is None else use_audio_tags

    def _level(self):
        try:
            lvl = int(self.emotion_level)
        except (TypeError, ValueError):
            lvl = 1
        return lvl if lvl in self.EMOTION_SETTINGS else 1

    def _voice_settings(self):
        stability, style, speed, _ = self.EMOTION_SETTINGS[self._level()]
        return {"stability": stability, "similarity_boost": 0.75,
                "style": style, "use_speaker_boost": True, "speed": speed}

    def _decorate(self, text):
        if not self.use_audio_tags:
            return text
        return self.EMOTION_SETTINGS[self._level()][3] + text

    def forward_to_file_(self, text, file_path):
        url = (f"https://api.elevenlabs.io/v1/text-to-speech/{self.voice_id}"
               f"?output_format=pcm_{self.sample_rate}")
        body = json.dumps({
            "text": self._decorate(text),
            "model_id": self.model_id,
            "voice_settings": self._voice_settings(),
        }).encode("utf-8")
        req = urllib.request.Request(
            url, data=body,
            headers={"xi-api-key": self.api_key,
                     "Content-Type": "application/json",
                     "Accept": "audio/pcm"},
        )
        with urllib.request.urlopen(req, timeout=60) as r:
            pcm = r.read()

        if not file_path.lower().endswith(".wav"):
            file_path = file_path.rsplit(".", 1)[0] + ".wav"
        with wave.open(file_path, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)            # 16-bit
            w.setframerate(self.sample_rate)
            w.writeframes(pcm)
