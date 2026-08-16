from openai import OpenAI
from pydub import AudioSegment
from .Base import Text2Speech

class OpenAI_tts(Text2Speech):
    def __init__(self, api_key, model="tts-1", voice="alloy"):
        """
        Initializes the OpenAI TTS model.
        
        Args:
            api_key (str): The API key for the OpenAI TTS model.
            model (str): The model to use. Defaults to "tts-1".
            voice (str): The voice to use. Defaults to "alloy".

        Returns:
            None
        """
        self.client = OpenAI(api_key=api_key)
        self.model = model
        self.voice = voice

    def forward_(self, text):
        """
        Sends the text to the model and returns the audio.

        Args:
            text (str): The text to send to the model.

        Returns:
            bytes: The audio from the model.
        """
        audio = self.client.audio.speech.create(
            model=self.model,
            voice=self.voice,
            input=text,
        )
        return audio
    
    def forward_to_file_(self, text, file_path):
        """
        Sends the text to the model and saves the audio to a file.

        Args:
            text (str): The text to send to the model.
            file_path (str): The file path to save the audio to.

        Returns:
            None
        """
        audio = self.client.audio.speech.create(
            model=self.model,
            voice=self.voice,
            input=text,
            response_format="mp3"
        )
        audio.stream_to_file(file_path)
        sound = AudioSegment.from_mp3(file_path)
        sound.export(file_path.split(".")[0]+".wav", format="wav")