import torch
from transformers import pipeline
from transformers.utils import is_flash_attn_2_available
from .Base import Speech2Text

class TransformersSTT(Speech2Text):
    def __init__(self, model_name="openai/whisper-tiny", device="cpu"):
        """
        Initialize a local pipeline for automatic speech recognition.

        Args:
            model_name (str): The model name to use. Defaults to "openai/whisper-tiny".
            device (str): The device to use. Defaults to "cpu".

        Returns:
            None
        """
        self.pipe = pipeline(
            "automatic-speech-recognition",
            # model="openai/whisper-large-v3", # select checkpoint from https://huggingface.co/openai/whisper-large-v3#model-details
            # model="openai/whisper-tiny", # select checkpoint from https://huggingface.co/openai/whisper-large-v3#model-details
            model=model_name,
            
            # torch_dtype=torch.float16, # for mps
            device=device, # or mps for Mac devices
            
            torch_dtype=torch.float32,
            # device="cpu", # or mps for Mac devices
            
            model_kwargs={"attn_implementation": "flash_attention_2"} if is_flash_attn_2_available() else {"attn_implementation": "sdpa"},
        )
    
    def forward_(self, audio):
        """
        Sends the audio to the model and returns the transcript.

        Args:
            audio (bytes): The audio file to transcribe.

        Returns:
            str: The transcript of the audio.
        """
        outputs = self.pipe(
            audio,
            chunk_length_s=30,
            batch_size=24,
            return_timestamps=False,
        )
        # return outputs["text"]
        return 'Hello world, this is a streaming test.'