from transformers import VitsModel, AutoTokenizer
import scipy
from AudioFormat import float2pcm
from .Base import Text2Speech

class TransformersTTS(Text2Speech):
    def __init__(self, model_name="facebook/mms-tts-eng", device="cpu"):
        """
        Initializes the VITS model.

        Args:
            model_name (str): The model name to use. Defaults to "facebook/mms-tts-eng".
            device (str): The device to use. Defaults to "cpu".

        Returns:
            None
        """
        self.device = device
        self.tokenizer = AutoTokenizer.from_pretrained(model_name)
        self.model = VitsModel.from_pretrained(model_name).to(device)

    def forward_(self, text):
        """
        Sends the text to the model and returns the audio.

        Args:
            text (str): The text to send to the model.

        Returns:
            bytes: The audio from the model.
        """
        input_ids = self.tokenizer(text, return_tensors="pt")
        audio = self.model(**input_ids).waveform
        audio = float2pcm(audio.T.detach().numpy())
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
        input_ids = self.tokenizer(text, return_tensors="pt")
        output = self.model(**input_ids).waveform
        output = float2pcm(output.T.detach().numpy())
        scipy.io.wavfile.write(file_path, rate=self.model.config.sampling_rate, data=output)