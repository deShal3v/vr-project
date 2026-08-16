import boto3
from contextlib import closing
from pydub import AudioSegment
from .Base import Text2Speech

class Polly(Text2Speech):
    def __init__(self, access_key_id, secret_access_key, region_name="eu-central-1", voice_id="Joanna"):
        """
        Initializes the Amazon Polly service.

        Args:
            access_key_id (str): The access key ID for the AWS account.
            secret_access_key (str): The secret access key for the AWS account.
            region_name (str): The region name for the AWS account. Defaults to "eu-central-1".
            voice_id (str): The voice ID to use. Defaults to "Joanna".

        Returns:
            None
        """
        self.region_name = region_name
        self.voice_id = voice_id
        # session = Session(aws_access_key_id=access_key_id, aws_secret_access_key=secret_access_key, region_name=region_name)
        self.client = boto3.client("polly", region_name=region_name, aws_access_key_id=access_key_id, aws_secret_access_key=secret_access_key)

    def forward_(self, text):
        """
        Sends the text to the Amazon Polly service and returns the audio.

        Args:
            text (str): The text to send to the service.

        Returns:
            bytes: The audio from the service.
        """
        response = self.client.synthesize_speech(Text=text, OutputFormat="mp3", VoiceId=self.voice_id)
        if "AudioStream" in response:
            with closing(response["AudioStream"]) as stream:
                audio = stream.read()
        return audio
    
    def forward_to_file_(self, text, file_path):
        """
        Sends the text to the Amazon Polly service and saves the audio to a file.

        Args:
            text (str): The text to send to the service.
            file_path (str): The file path to save the audio to.

        Returns:
            None
        """
        response = self.client.synthesize_speech(Text=text, OutputFormat="mp3", VoiceId=self.voice_id)
        if "AudioStream" in response:
            with closing(response["AudioStream"]) as stream:
                with open(file_path.split(".")[0]+".mp3", "wb") as file:
                    file.write(stream.read())
            sound = AudioSegment.from_mp3(file_path.split(".")[0]+".mp3")
            sound.export(file_path, format="wav")