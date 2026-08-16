def getTextToSpeech(model_name, openai_api_key=None, amazon_access_key_id=None, amazon_secret_key=None, OpenAIVoice="alloy", deepgram_api_key=None, DeepgramVoice="aura-2-thalia-en", elevenlabs_api_key=None, elevenlabs_voice_id="21m00Tcm4TlvDq8ikWAM", elevenlabs_model="eleven_multilingual_v2", elevenlabs_audio_tags=None):
    if model_name == "Amazon_polly":
        if amazon_access_key_id is None or amazon_secret_key is None:
            raise ValueError("Amazon access key id and secret key must be provided")
        from .Amazon import Polly
        return Polly(amazon_access_key_id, amazon_secret_key)
    elif model_name == "OpenAI_tts":
        if openai_api_key is None:
            raise ValueError("OpenAI api key must be provided")
        from .OpenAI import OpenAI_tts
        return OpenAI_tts(openai_api_key, voice=OpenAIVoice)
    elif model_name == "Local_TTS":
        from .TransformersTTS import TransformersTTS
        return TransformersTTS()
    elif model_name == "Deepgram_aura":
        if deepgram_api_key is None:
            raise ValueError("Deepgram api key must be provided")
        from .Deepgram import Deepgram_aura
        return Deepgram_aura(deepgram_api_key, voice=DeepgramVoice)
    elif model_name == "ElevenLabs":
        if elevenlabs_api_key is None:
            raise ValueError("ElevenLabs api key must be provided")
        from .ElevenLabs import ElevenLabs
        return ElevenLabs(elevenlabs_api_key, voice_id=elevenlabs_voice_id,
                          model_id=elevenlabs_model, use_audio_tags=elevenlabs_audio_tags)
    else:
        raise ValueError(f"Model name {model_name} is not supported")
