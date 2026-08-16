def getSpeechToText(model_name, openai_api_key=None, amazon_access_key_id=None, amazon_secret_key=None, deepgram_api_key=None):
    if model_name == "Local_STT":
        from .TransformersSTT import TransformersSTT
        return TransformersSTT()
    elif model_name == "Amazon_transcribe":
        if amazon_access_key_id is None or amazon_secret_key is None:
            raise ValueError("Amazon access key id and secret key must be provided")
        from .Amazon import Transcribe
        return Transcribe(amazon_access_key_id, amazon_secret_key)
    elif model_name == "OpenAI_whisper":
        if openai_api_key is None:
            raise ValueError("OpenAI api key must be provided")
        from .OpenAI import OpenAI_whisper
        return OpenAI_whisper(openai_api_key)
    elif model_name == "Deepgram_nova":
        if deepgram_api_key is None:
            raise ValueError("Deepgram api key must be provided")
        from .Deepgram import Deepgram_nova
        return Deepgram_nova(deepgram_api_key)
    else:
        raise ValueError(f"Model name {model_name} is not supported")
