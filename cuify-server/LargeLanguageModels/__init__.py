from .Base import LLM

def getLargeLanguageModel(model_name, openai_api_key=None, google_api_key=None, stream=False, anthropic_api_key=None):
    if model_name == "Local_base":
        return LLM()
    elif model_name == "OpenAI_gpt3_5_turbo":
        if openai_api_key is None:
            raise ValueError("OpenAI api key must be provided")
        from .OpenAI import OpenAI_gpt
        return OpenAI_gpt(openai_api_key, stream=stream)
    elif model_name == "OpenAI_gpt4_turbo":
        if openai_api_key is None:
            raise ValueError("OpenAI api key must be provided")
        from .OpenAI import OpenAI_gpt
        return OpenAI_gpt(openai_api_key, model="gpt-4-turbo", stream=stream)
    elif model_name == "OpenAI_gpt4o":
        if openai_api_key is None:
            raise ValueError("OpenAI api key must be provided")
        from .OpenAI import OpenAI_gpt
        return OpenAI_gpt(openai_api_key, model="gpt-4o", stream=stream)
    elif model_name == "OpenAI_gpt4o_mini":
        if openai_api_key is None:
            raise ValueError("OpenAI api key must be provided")
        from .OpenAI import OpenAI_gpt
        return OpenAI_gpt(openai_api_key, model="gpt-4o-mini", stream=stream)
    elif model_name == "Google_gemini":
        if google_api_key is None:
            raise ValueError("Google api key must be provided")
        from .Google import Google
        return Google(google_api_key, stream=stream)
    elif "HuggingFace" in model_name:
        from .HuggingFace import HuggingFace
        model_name = model_name.replace("HuggingFace_", "")
        return HuggingFace(model_name, stream=stream)
    elif model_name.startswith("Anthropic_"):
        if anthropic_api_key is None:
            raise ValueError("Anthropic api key must be provided")
        from .Anthropic import Anthropic_claude
        claude_model = model_name.replace("Anthropic_", "").replace("_", "-")
        return Anthropic_claude(anthropic_api_key, model=claude_model, stream=stream)
    else:
        raise ValueError(f"Model name {model_name} is not supported")
