from deepgram import DeepgramClient
from .Base import Speech2Text


class Deepgram_nova(Speech2Text):
    def __init__(self, api_key, model="nova-3", language="en"):
        self.client = DeepgramClient(api_key=api_key)
        self.model = model
        self.language = language

    def forward_(self, audio):
        response = self.client.listen.v1.media.transcribe_file(
            request=audio,
            model=self.model,
            language=self.language,
            smart_format=True,
            punctuate=True,
        )
        return response.results.channels[0].alternatives[0].transcript
