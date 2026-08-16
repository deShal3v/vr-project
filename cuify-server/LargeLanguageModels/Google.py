import google.generativeai as genai
from .Base import LLM

class Google(LLM):
    def __init__(self, api_key, model="gemini-pro", stream=False):
        genai.configure(api_key=api_key)
        self.client = genai.GenerativeModel(model)
        self.model = model
        self.stream = stream

    def forward(self, text, **kwargs):
        response = self.client.generate_content(text, stream=False)
        if self.stream:
            return response
        return response.text