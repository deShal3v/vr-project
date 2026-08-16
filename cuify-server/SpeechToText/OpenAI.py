from openai import OpenAI
from .Base import Speech2Text
import io
class OpenAI_whisper(Speech2Text):
    def __init__(self, api_key, model="whisper-1"):
        """
        Initializes the OpenAI API.

        Args:
            api_key (str): The API key for the OpenAI account.
            model (str, optional): The model name. Defaults to "whisper-1".
        """
        self.client = OpenAI(api_key=api_key)
        self.model = model

    def forward_(self, audio):
        """
        # Sends the audio to the model and returns the transcript.

        Args:
            audio (bytes): The audio file to transcribe.

        Returns:
            str: The transcript of the audio.
        """
        audio = io.BytesIO(audio)
        audio.name = "audio.wav"
        transcript = self.client.audio.transcriptions.create(
            model=self.model,
            file=audio,
            response_format="text",
            language="En"
        )
        return transcript
