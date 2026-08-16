from types import SimpleNamespace
from anthropic import Anthropic
from .Base import LLM


def _as_openai_chunk(text):
    return SimpleNamespace(choices=[SimpleNamespace(delta=SimpleNamespace(content=text))])


class Anthropic_claude(LLM):
    def __init__(self, api_key, model="claude-sonnet-4-6", stream=False, max_tokens=512):
        self.client = Anthropic(api_key=api_key)
        self.model = model
        self.stream = stream
        self.max_tokens = max_tokens

    def _build_messages(self, text, history):
        messages = []
        for turn in history:
            messages.append({"role": "user", "content": turn["user"]})
            messages.append({"role": "assistant", "content": turn["assistant"]})
        messages.append({"role": "user", "content": text})
        return messages

    def _stream_iter(self, messages, system_prompt):
        with self.client.messages.stream(
            model=self.model,
            max_tokens=self.max_tokens,
            system=system_prompt,
            messages=messages,
        ) as stream:
            for text in stream.text_stream:
                if text:
                    yield _as_openai_chunk(text)

    def forward(self, text, history=[], preprompt="You are a helpful assistant."):
        messages = self._build_messages(text, history)
        if self.stream:
            return self._stream_iter(messages, preprompt)
        msg = self.client.messages.create(
            model=self.model,
            max_tokens=self.max_tokens,
            system=preprompt,
            messages=messages,
        )
        return "".join(block.text for block in msg.content if getattr(block, "type", None) == "text")
