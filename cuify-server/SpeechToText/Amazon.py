import boto3
from .Base import Speech2Text

class Transcribe(Speech2Text):
    def __init__(self, access_key_id, secret_access_key, region_name="eu-central-1"):
        """
        Initializes the Amazon Transcribe service.

        Args:
            access_key_id (str): The access key ID for the AWS account.
            secret_access_key (str): The secret access key for the AWS account.
            region_name (str): The region name for the AWS account. Defaults to "eu-central-1".

        Returns:
            None
        """
        self.region_name = region_name
        self.client = boto3.client("transcribe", region_name=region_name, aws_access_key_id=access_key_id, aws_secret_access_key=secret_access_key)

    def forward_(self, audio):
        """
        Sends the audio to the Amazon Transcribe service and returns the response.

        Args:
            audio (bytes): The audio to send to the service.

        Returns:
            str: The response from the service.
        """
        # not implemented
        # check https://github.com/awslabs/amazon-transcribe-streaming-sdk for streaming transcription
        pass