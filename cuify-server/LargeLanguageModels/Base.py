class LLM:
    def __init__(self, ):
        pass

    def forward(self, text, histrory = [], preprompt = "You are a helpful assistant"):
        # return "I am a helpful assistant. I am a helpful assistant."
        return "I am a helpful assistant. I am a helpful assistant. I am a helpful assistant. I am a helpful assistant."
        # return "I am a helpful assistant. I am a helpful assistant. I am a helpful assistant. I am a helpful assistant. I am a helpful assistant. I am a helpful assistant. I am a helpful assistant. I am a helpful assistant."

    def __name__(self):
        return self.__class__.__name__