from openai import OpenAI
from .Base import LLM

class OpenAI_gpt(LLM):
    def __init__(self, api_key, model="gpt-3.5-turbo", stream=False):
        """
        Initializes the OpenAI GPT model.

        Args:
            api_key (str): The API key for the OpenAI GPT model.
            model (str): The model to use. Defaults to "gpt-3.5-turbo".
            stream (bool): Whether to use the stream endpoint. Defaults to False.

        Returns:
            None
        """
        self.client = OpenAI(api_key=api_key)
        self.model = model
        self.stream = stream

    def forward(self, text, histrory = [], preprompt = "You are a helpful assistant."):
        """
        Sends the text to the model and returns the response.

        Args:
            text (str): The text to send to the model.

        Returns:
            str: The response from the model.
        """
        messages = []
        messages.append({"role": "system", "content": preprompt})
        for i in range(len(histrory)):
            messages.append({"role": "user", "content": histrory[i]["user"]})
            messages.append({"role": "assistant", "content": histrory[i]["assistant"]})
        messages.append({"role": "user", "content": text})

        response = self.client.chat.completions.create(
            model=self.model,
            messages=messages,
            stream=self.stream
        )
        if self.stream:
            return response
        return response.choices[0].message.content

    def forward_tools(self, text, history=[], preprompt="You are a helpful assistant.",
                      tools=None, tool_choice="auto"):
        """
        Non-streaming completion that can call tools. Returns the raw message object
        (has .content and .tool_calls). Used by image-director mode so the model can
        both speak and choose which image to display in one structured turn.
        """
        messages = [{"role": "system", "content": preprompt}]
        for i in range(len(history)):
            messages.append({"role": "user", "content": history[i]["user"]})
            messages.append({"role": "assistant", "content": history[i]["assistant"]})
        messages.append({"role": "user", "content": text})

        response = self.client.chat.completions.create(
            model=self.model,
            messages=messages,
            tools=tools,
            tool_choice=tool_choice,
            stream=False,
        )
        return response.choices[0].message