import transformers
import torch
from threading import Thread
from .Base import LLM
from huggingface_hub import login
import os

class HuggingFace(LLM):
    def __init__(self, model_id = "meta-llama/Llama-3.2-1B", device_map='auto', stream=False):
        """
        Initializes the HuggingFace model with given configurations.

        Args:
            model_id (str): The ID of the model to use.
            quantization_config (transformers.BitsAndBytesConfig): Configuration for model quantization.
            device_map (str or dict): Device placement strategy for the model.

        Returns:
            None
        """

        login(os.environ["HF_TOKEN"])
        print("Logged in")
        self.tokenizer = transformers.AutoTokenizer.from_pretrained(model_id)
        # quantization_config = transformers.BitsAndBytesConfig(
        #     load_in_4bit=True,
        #     bnb_4bit_use_double_quant=True,
        #     bnb_4bit_quant_type="nf4",
        #     bnb_4bit_compute_dtype=torch.bfloat16
        # )
        self.model = transformers.AutoModelForCausalLM.from_pretrained(
            model_id,
            trust_remote_code=True,
            # quantization_config=quantization_config,
            device_map=device_map
        )
        self.device = 'cuda' if torch.cuda.is_available() else 'cpu'
        self.model.to(self.device)
        self.stream = stream

    def streamer(self, streamer):
        print("streaming")
        model_output = ""
        for new_text in streamer:
            model_output += new_text
            yield model_output

    def forward(self, text, history = [], preprompt = "You are a helpful assistant."):
        """
        Sends the user input to the model and streams back the response.

        Args:
            user_input (str): The text input from the user.
            history (list): A list of previous message pairs (user, assistant).

        Returns:
            str: Generated text from the model.
        """
        messages = []
        messages.append({"role": "system", "content": preprompt})
        for i in range(len(history)):
            messages.append({"role": "user", "content": history[i]["user"]})
            messages.append({"role": "assistant", "content": history[i]["assistant"]})
        messages.append({"role": "user", "content": text})

        encoded_inputs = self.tokenizer.apply_chat_template(messages, return_tensors="pt")

        if self.stream:
            model_inputs = {'input_ids': encoded_inputs.to(self.device)}  
            streamer = transformers.TextIteratorStreamer(self.tokenizer, timeout=10., skip_prompt=True, skip_special_tokens=True)
            generate_kwargs = dict(
                model_inputs,
                streamer=streamer,
                max_new_tokens=2048,
                do_sample=True,
                # Other parameters like temperature or top_k can be added here
            )

            t = Thread(target=self.model.generate, kwargs=generate_kwargs)
            t.start()

            return self.streamer(streamer)
        else:
            model_output = self.model.generate(encoded_inputs.to(self.device), max_new_tokens=2048)
            return self.tokenizer.decode(model_output[0], skip_special_tokens=True)
