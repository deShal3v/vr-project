import time
import os
import json
from SpeechToText import getSpeechToText
from TextToSpeech import getTextToSpeech
from LargeLanguageModels import getLargeLanguageModel
from OutputCapture import Capturing

# Tool the LLM calls (image-director mode) to speak AND choose which library photo to show.
PRESENT_TURN_TOOL = [{
    "type": "function",
    "function": {
        "name": "present_turn",
        "description": "Speak to the user and display exactly one library image on the room screen. Call once per user message.",
        "parameters": {
            "type": "object",
            "properties": {
                "spoken_text": {
                    "type": "string",
                    "description": "Exactly what the agent says aloud this turn (1-3 sentences, natural speech, no markdown)."
                },
                "image": {
                    "type": "string",
                    "description": "Key of the picture to display now.",
                    "enum": [
                        "puppy_no_sunglasses_bike_park",
                        "cat_sunglasses_bike_park",
                        "horse_handlebars_park",
                        "chihuahua_sunglasses_bike_city",
                        "puppy_sunglasses_sled_snow",
                        "puppy_sunglasses_rollercoaster",
                        "donkey_scooter_telaviv",
                        "correct_target"
                    ]
                },
                "emotion_level": {
                    "type": "integer",
                    "minimum": 1,
                    "maximum": 5,
                    "description": "Voice emotional intensity this turn: 1 calm, 2 mild impatience, 3 anxious/frustrated, 4 fearful/apologetic, 5 near-crying/desperate."
                }
            },
            "required": ["spoken_text", "image", "emotion_level"]
        }
    }
}]


def parse_present_turn(msg):
    """Pull (spoken_text, image_key, emotion_level) out of a present_turn tool call."""
    spoken_text = (getattr(msg, "content", None) or "").strip()
    image_key = None
    emotion_level = 1
    for tc in (getattr(msg, "tool_calls", None) or []):
        if tc.function and tc.function.name == "present_turn":
            try:
                a = json.loads(tc.function.arguments or "{}")
            except Exception:
                a = {}
            spoken_text = (a.get("spoken_text") or spoken_text or "").strip()
            image_key = a.get("image")
            emotion_level = a.get("emotion_level", 1)
            break
    return spoken_text, image_key, emotion_level

def client_listener(connection, address, args):
    print(f"Connection from {address} has been established")
    client_log_folder = f"logs/{address[0]}_{address[1]}_{time.time()}"
    os.makedirs(client_log_folder, exist_ok=True)
    with open(f"{client_log_folder}/log.txt", "w") as f:
        f.write(f"Connection from {address} has been established at {time.time()}\n")
    
    client_config_lengths = int.from_bytes(connection.recv(4), byteorder='little')
    received = 0
    buf = b""
    while received < client_config_lengths:
        data = connection.recv(client_config_lengths-received)
        received += len(data)
        buf += data

    received_config = buf.decode("utf-8")
    model_names = buf.decode("utf-8").split(",")[0:3]
    api_keys = buf.decode("utf-8").split(",")[3:7]
    storeHistory = buf.decode("utf-8").split(",")[7]
    OpenAIVoice = buf.decode("utf-8").split(",")[8]
    if OpenAIVoice not in ["alloy", "echo", "fable", "onyx", "nova", "shimmer"]:
        OpenAIVoice = "alloy"
    preprompt = buf.decode("utf-8").split(",")[9:]
    preprompt = " ".join(preprompt)
    # Image-director mode: load the full "brain" prompt from a file (avoids the comma-mangling
    # of the client-sent prompt and keeps the paradigm server-side).
    if getattr(args, "image_director", False) and getattr(args, "system_prompt_file", None):
        try:
            with open(args.system_prompt_file, "r", encoding="utf-8") as pf:
                preprompt = pf.read()
            print(f"Loaded image-director prompt from {args.system_prompt_file}")
        except Exception as e:
            print(f"Could not load system_prompt_file: {e}")
    print(f"Received config: {received_config}\nReceived model names: {model_names}\nReceived API keys: {api_keys}")

    if api_keys[0] != "":
        args.openai_api_key = api_keys[0]
    if api_keys[1] != "":
        args.amazon_access_key_id = api_keys[1]
    if api_keys[2] != "":
        args.amazon_secret_key = api_keys[2]
    if api_keys[3] != "":
        args.google_api_key = api_keys[3]
    
    print("**"*50)
    
    try:
        with Capturing() as output:
            STT = getSpeechToText(model_names[0], args.openai_api_key, args.amazon_access_key_id, args.amazon_secret_key, deepgram_api_key=args.deepgram_api_key)
            if "_stream" in model_names[1]:
                args.stream = True
                model_names[1] = model_names[1].replace("_stream", "")
            LLM = getLargeLanguageModel(model_names[1], args.openai_api_key, args.google_api_key, args.stream, anthropic_api_key=args.anthropic_api_key)
            tts_model_name = getattr(args, "tts_override", None) or model_names[2]
            TTS = getTextToSpeech(tts_model_name, args.openai_api_key, args.amazon_access_key_id, args.amazon_secret_key, OpenAIVoice, deepgram_api_key=args.deepgram_api_key,
                                  elevenlabs_api_key=args.elevenlabs_api_key,
                                  elevenlabs_voice_id=(getattr(args, "elevenlabs_voice_id", None) or "21m00Tcm4TlvDq8ikWAM"),
                                  elevenlabs_model=(getattr(args, "elevenlabs_model", None) or "eleven_multilingual_v2"),
                                  elevenlabs_audio_tags=getattr(args, "elevenlabs_audio_tags", None))
        connection.send("!OKK".encode("utf-8"))
    except Exception as e:
        print(f"Error: {e}")
        connection.send("!ERR".encode("utf-8"))
        connection.close()
        return

    chat_history = []

    lastPartFlag = 0
    while True:
        if lastPartFlag == 2:
            break
        lastPartFlag = 0
        receivedPart = 0
        received_text = ""
        while lastPartFlag == 0 and receivedPart < 100:
            try:
                messsage_length = int.from_bytes(connection.recv(4), byteorder='little')
                lastPartFlag = int.from_bytes(connection.recv(1), byteorder='little')
                received = 0
                receivedPart += 1
                buf = b""
                while received < messsage_length:
                    data = connection.recv(messsage_length-received)
                    received += len(data)
                    buf += data
                if buf is None or len(buf) == 0:
                    connection.close()
                    return
                with open(f"tmp/TTS_ex_server_input_{receivedPart}.wav", "wb") as f:
                    f.write(buf)
                start = time.time()
                with Capturing() as output:
                    received_text_part = STT.forward(buf).strip()
                received_text += received_text_part
                speech2text_time = time.time() - start
            except ConnectionResetError:
                print("Connection closed by client")
                connection.close()
                break

        # Large language model
        start = time.time()
        response_audio_path = "tmp/TTS_ex_server_output.wav"
        text2speech_time = 0.0
        if getattr(args, "image_director", False):
            # Tool-driven: the LLM returns BOTH what to say and which image to show.
            args.stream = False  # forward_tools is non-streaming; keeps logging path consistent
            with Capturing() as output:
                _msg = LLM.forward_tools(
                    received_text, chat_history, preprompt,
                    tools=PRESENT_TURN_TOOL,
                    tool_choice={"type": "function", "function": {"name": "present_turn"}},
                )
            response_text, image_key, emotion_level = parse_present_turn(_msg)
            gpt_time = time.time() - start
            # Set emotional delivery before synthesizing (if the TTS supports it).
            if hasattr(TTS, "emotion_level"):
                TTS.emotion_level = emotion_level
            # Synthesize first, THEN send - so a client that disconnected doesn't crash us mid-send.
            start = time.time()
            if response_text:
                with Capturing() as output:
                    TTS.forward_to_file(response_text, response_audio_path)
                response_audio = open(response_audio_path, "rb").read()
            else:
                response_audio = b""
            text2speech_time = time.time() - start
            try:
                # 1) Control frame: which image to display (processed before audio).
                if image_key:
                    _img = ("@@IMG@@" + image_key).encode("utf-8")
                    connection.send(len(_img).to_bytes(4, byteorder='little') + (0).to_bytes(1, byteorder='little') + _img)
                    print(f"[image_director] show_image -> {image_key} (emotion {emotion_level})")
                # 2) Speak the line as the final audio part.
                connection.send(len(response_audio).to_bytes(4, byteorder='little') + (1).to_bytes(1, byteorder='little') + response_audio)
            except OSError:
                print("Client disconnected mid-response; closing this connection cleanly.")
                try:
                    connection.close()
                except Exception:
                    pass
                return
        else:
            with Capturing() as output:
                response_text_buffer = LLM.forward(received_text, chat_history, preprompt)
            response_text = "" if args.stream else response_text_buffer
            gpt_time = time.time() - start
            if args.stream:
                buffer = []
                for chunk in response_text_buffer:
                    content = chunk.choices[0].delta.content
                    if content is not None and len(content) > 0:
                        buffer.append(content)
                        if buffer[-1] in [".", "!", "?"]:
                            response_text_part = "".join(buffer)
                            start = time.time()
                            with Capturing() as output:
                                TTS.forward_to_file(response_text_part, response_audio_path)
                            response_text += response_text_part
                            response_audio = open(response_audio_path, "rb").read()
                            text2speech_time = time.time() - start
                            lastPartFlag = 0
                            response_audio = len(response_audio).to_bytes(4, byteorder='little') + lastPartFlag.to_bytes(1, byteorder='little') + response_audio
                            connection.send(response_audio)
                            buffer = []
                if len(buffer) > 0:
                    response_text_part = "".join(buffer)
                    start = time.time()
                    with Capturing() as output:
                        TTS.forward_to_file(response_text_part, response_audio_path)
                    response_text += response_text_part
                    response_audio = open(response_audio_path, "rb").read()
                    text2speech_time = time.time() - start
                    lastPartFlag = 1
                    response_audio = len(response_audio).to_bytes(4, byteorder='little') + lastPartFlag.to_bytes(1, byteorder='little') + response_audio
                    connection.send(response_audio)
                else:
                    lastPartFlag = 1
                    response_audio = []
                    response_audio = len(response_audio).to_bytes(4, byteorder='little') + lastPartFlag.to_bytes(1, byteorder='little')
                    connection.send(response_audio)
            else:
                # Text to speech
                start = time.time()
                with Capturing() as output:
                    TTS.forward_to_file(response_text, response_audio_path)
                response_audio = open(response_audio_path, "rb").read()
                # response_audio = TTS.forward(response_text)
                text2speech_time = time.time() - start
                response_audio = len(response_audio).to_bytes(4, byteorder='little') + response_audio
                connection.send(response_audio)

        if not (storeHistory == "nostore"):
            chat_history.append({"user": received_text, "assistant": response_text})

        # append the logs
        with open(f"{client_log_folder}/log.txt", "a") as f:
            f.write("-" * 50 + "\n")
            f.write(f"Received config: {received_config}\n")
            f.write(f"Received audio length: {len(response_audio)}\n")
            f.write(f"Speech2Text time: {speech2text_time} seconds\n")
            f.write(f"Received text: {received_text}\n")
            f.write(f"GPT time: {gpt_time} seconds\n")
            f.write(f"Response text: {response_text}\n")
            f.write(f"Text2Speech time: {text2speech_time} seconds\n")
            f.write("-" * 50 + "\n")

        if args.verbose:
            print("-" * 50)
            print(f"Received text: {received_text}")
            if args.stream:
                print(f"Received text parts: {response_text_part}")
            else:
                print(f"Response text: {response_text}")
            print(f"Speech2Text time: {speech2text_time} seconds, GPT time: {gpt_time} seconds, Text2Speech time: {text2speech_time} seconds")
            print("-" * 50)

        # send audio data to client
        time.sleep(0.01)