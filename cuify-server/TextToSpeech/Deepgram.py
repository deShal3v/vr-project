from deepgram import DeepgramClient
from .Base import Text2Speech


class Deepgram_aura(Text2Speech):
    def __init__(self, api_key, voice="aura-2-thalia-en", sample_rate=24000):
        self.client = DeepgramClient(api_key=api_key)
        self.voice = voice
        self.sample_rate = sample_rate

    def forward_to_file_(self, text, file_path):
        audio_iter = self.client.speak.v1.audio.generate(
            text=text,
            model=self.voice,
            encoding="linear16",
            container="wav",
            sample_rate=self.sample_rate,
        )
        with open(file_path, "wb") as f:
            for chunk in audio_iter:
                f.write(chunk)
