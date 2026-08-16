using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

public class NPCClient : MonoBehaviour
{
    public enum TextToSpeechModels
    {
        [InspectorName("Facebook TTS Model")]Local_TTS,
        [InspectorName("Amazon Polly Model")]Amazon_polly,
        [InspectorName("OpenAI TTS Model")]OpenAI_tts,
        [InspectorName("Deepgram Aura")]Deepgram_aura
    }
    public TextToSpeechModels TTSModel = TextToSpeechModels.Deepgram_aura;

    private string OpenAIVoice = "alloy";

    public enum LLMModels
    {
        [InspectorName("Base Model")]Local_base,
        [InspectorName("OpenAI GPT 3.5 Model")]OpenAI_gpt3_5_turbo,
        [InspectorName("OpenAI GPT 3.5 Model with Streaming mode")]OpenAI_gpt3_5_turbo_stream,
        [InspectorName("OpenAI GPT 4 Model")]OpenAI_gpt4_turbo,
        [InspectorName("OpenAI GPT 4 Model with Streaming mode")]OpenAI_gpt4_turbo_stream,
        [InspectorName("OpenAI GPT 4o Model")]OpenAI_gpt4o,
        [InspectorName("OpenAI GPT 4o Model with Streaming mode")]OpenAI_gpt4o_stream,
        [InspectorName("OpenAI GPT 4o-mini Model")]OpenAI_gpt4o_mini,
        [InspectorName("OpenAI GPT 4o-mini Model with Streaming mode")]OpenAI_gpt4o_mini_stream,
        [InspectorName("OpenAI Gemini Model")]Google_gemini,
        [InspectorName("HuggingFace Custom Model")]HuggingFace,
        [InspectorName("Anthropic Claude Sonnet 4.6")]Anthropic_claude_sonnet_4_6,
        [InspectorName("Anthropic Claude Sonnet 4.6 (Streaming)")]Anthropic_claude_sonnet_4_6_stream,
        [InspectorName("Anthropic Claude Haiku 4.5")]Anthropic_claude_haiku_4_5,
        [InspectorName("Anthropic Claude Haiku 4.5 (Streaming)")]Anthropic_claude_haiku_4_5_stream,
        [InspectorName("Anthropic Claude Opus 4.7")]Anthropic_claude_opus_4_7,
        [InspectorName("Anthropic Claude Opus 4.7 (Streaming)")]Anthropic_claude_opus_4_7_stream
    }
    public LLMModels LLM = LLMModels.OpenAI_gpt4o_mini_stream;
    public enum SpeechToTextModels
    {
        [InspectorName("Facebook STT Model")]Local_STT,
        [InspectorName("OpenAI Whisper Model")]OpenAI_whisper,
        [InspectorName("Amazon Transcribe Model")]Amazon_transcribe,
        [InspectorName("Deepgram Nova")]Deepgram_nova
    }
    public SpeechToTextModels STTModel = SpeechToTextModels.Deepgram_nova;
    public string CustomLLM = "";
    [TextArea(5,15)]public string systemPrompt = ""; // TextArea(minArea, maxArea) areas that shown on the inspector

    [Header("Server Settings")] // Header for grouping variables in the inspector
    public string serverIP = "127.0.0.1";
    public int serverPort = 9999;

    [Header("OpenAI")] // Header for grouping variables in the inspector
    public string SecretAPIKey = "";

    [Header("Amazon")] // Header for grouping variables in the inspector
    public string AccessKeyID = "";
    public string SecretKey = "";

    [Header("Google")] // Header for grouping variables in the inspector
    public string APIKey = "";

    [Header("Other Settings")] // Header for grouping variables in the inspector

    public bool storeHistory = true;

    public int recordTime = 10; // Record time in seconds

    [Header("Debug / Headless Test")]
    [Tooltip("If true, skip mic. Press T (or use autoSendOnConnect) to send testAudioFileName from StreamingAssets through the same wire protocol the mic would use.")]
    public bool useTestAudioInsteadOfMic = false;
    public string testAudioFileName = "test_question.wav";
    [Tooltip("Auto-send the test audio once the connection handshake completes (good for remote/headless testing).")]
    public bool autoSendTestOnConnect = false;
    [Tooltip("Delay before auto-sending (lets domain reload / audio system warm up).")]
    public float autoSendDelaySeconds = 1.0f;
    
    [Header("VR Push-To-Talk")]
    [Tooltip("Hold this controller button to talk: press to start recording, release to send. " +
             "Bind it in the Inspector (default: right-hand primary / A button). " +
             "The keyboard Space key keeps working too.")]
    public InputActionProperty pushToTalkAction = new InputActionProperty(
        new InputAction("PushToTalk", InputActionType.Button, "<XRController>{RightHand}/primaryButton"));
    [Tooltip("Hold-to-talk (press = record, release = send). If false, the button toggles like the Space key.")]
    public bool holdToTalk = true;

    [Header("Hands-Free Listening (no button needed)")]
    [Tooltip("Always-on mic. Detects when you start and stop speaking and sends automatically. " +
             "Space / the controller button still work as a manual override.")]
    public bool handsFree = true;
    [Tooltip("Mic loudness (RMS) above this counts as speech. Raise it if a noisy room keeps triggering her.")]
    public float vadSpeechThreshold = 0.015f;
    [Tooltip("Endpointing: this much silence after you speak ends the utterance and sends it.")]
    public float vadSilenceSeconds = 1.0f;
    [Tooltip("Utterances shorter than this are discarded (coughs, clicks, door bumps).")]
    public float vadMinSpeechSeconds = 0.35f;
    [Tooltip("Hard cap on a single utterance before it is sent anyway.")]
    public float vadMaxSpeechSeconds = 20f;
    [Tooltip("Pause after the agent finishes speaking before listening again, so she never hears herself.")]
    public float vadPostSpeechCooldown = 0.4f;
    [Tooltip("Log the measured mic level - useful for tuning vadSpeechThreshold.")]
    public bool vadDebugLevels = false;

    // --- hands-free state ---
    const int VadSampleRate = 16000;
    const int VadClipSeconds = 30;
    private AudioClip vadClip;
    private int vadLastPos = 0;
    private bool vadInSpeech = false;
    private float vadSpeechElapsed = 0f;
    private float vadSilenceElapsed = 0f;
    private readonly List<float> vadBuffer = new List<float>();
    private float vadResumeAt = 0f;
    private bool vadAwaitingReply = false;
    private float vadDebugTimer = 0f;
    private float vadNextDeviceRetry = 0f;

    private bool isRecording = false;
    private AudioClip recordedClip;
    private TcpClient client;
    private NetworkStream stream;

    private byte[] lengthDataReceived = new byte[4];
    private int expectedDataLength = -1;
    private byte[] receivedData;
    private int totalBytesRead = 0;
    private bool connectionEstablished = false;

    // --- receive-loop safety ---------------------------------------------
    // Frames are [4-byte length][1-byte flag][payload]. A dropped/restarted server can
    // leave the stream misaligned, which used to be read as an absurd length (huge
    // allocation) or block the main thread on ReadByte(). These guard against both,
    // and time the wait out so a server-side error stops looking like "still thinking".
    const int MaxFrameBytes = 64 * 1024 * 1024;   // reject anything larger as garbage
    const float ReceiveTimeoutSeconds = 45f;
    private bool awaitingFlagByte = false;
    private int pendingLength = -1;
    private float receiveDeadline = -1f;
    private IEnumerator coroutine;
    private float lastSentTime;
    private AudioSource audioSource;
    int lastPartFlag = 0;
    private Queue<AudioClip> audioQueue = new Queue<AudioClip>(); // Queue to hold audio clips

    [Header("Image Generation")]
    [Tooltip("Renders pictures the Guide is asked to create. Auto-found on this GameObject if left empty.")]
    public GuideImageGenerator imageGenerator;
    [Tooltip("Transcribe each recorded utterance with OpenAI Whisper and, if it's an image request, " +
             "trigger image generation locally - no Python server changes required.")]
    public bool detectImageRequestsLocally = true;
    // Optional alternative path: the server can instead push an image request by sending a frame
    // whose payload starts with this ASCII marker (same framing as an audio reply part).
    const string ImageCmdMagic = "@@IMG@@";


    // Start is called before the first frame update
    void Start()
    {
        StartSocketConnection();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        if (imageGenerator == null) imageGenerator = GetComponent<GuideImageGenerator>();
    }

    void OnEnable()
    {
        var ptt = pushToTalkAction.action;
        if (ptt != null)
        {
            // Ensure a usable default binding even if the serialized action came across empty.
            if (ptt.bindings.Count == 0)
                ptt.AddBinding("<XRController>{RightHand}/primaryButton");
            ptt.Enable();
        }
    }

    void OnDisable()
    {
        pushToTalkAction.action?.Disable();
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && !useTestAudioInsteadOfMic)
        {
            if (!connectionEstablished)
            {
                // Used to fail silently, which looks exactly like "the key doesn't work".
                Debug.LogWarning($"[NPCClient] Not connected to the server at {serverIP}:{serverPort} - " +
                                 "can't record. Start the Python server, then stop/re-enter Play mode.");
            }
            else if (!isRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
                SendReceiveRecordedAudio();
            }
        }

        // VR controller push-to-talk (mirrors the Space-key flow above).
        var pttAction = pushToTalkAction.action;
        if (pttAction != null && connectionEstablished && !useTestAudioInsteadOfMic)
        {
            if (holdToTalk)
            {
                if (pttAction.WasPressedThisFrame() && !isRecording)
                {
                    StartRecording();
                }
                else if (pttAction.WasReleasedThisFrame() && isRecording)
                {
                    StopRecording();
                    SendReceiveRecordedAudio();
                }
            }
            else if (pttAction.WasPressedThisFrame())
            {
                if (!isRecording)
                {
                    StartRecording();
                }
                else
                {
                    StopRecording();
                    SendReceiveRecordedAudio();
                }
            }
        }
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame && connectionEstablished)
        {
            SendTestAudio();
        }
        if (!audioSource.isPlaying && audioQueue.Count > 0)
        {
            AudioClip clip = audioQueue.Dequeue();
            // Use clip + Play (not PlayOneShot) so AudioSource.GetOutputData works,
            // which AgentAnimation reads for jaw-flap amplitude.
            audioSource.clip = clip;
            audioSource.Play();
        }

        if (handsFree) PollHandsFree();
    }

    // ---------------------------------------------------------------------
    // Hands-free listening.
    // Deepgram's endpointing/utterance_end options only exist on their streaming
    // (websocket) API, but this project transcribes complete WAVs via the batch API.
    // So we do the same job client-side: keep the mic open, watch loudness, and treat
    // ~1s of silence after speech as end-of-utterance - then send that segment through
    // the exact same wire protocol push-to-talk uses.
    // ---------------------------------------------------------------------
    void StartHandsFree()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            // Don't disable hands-free permanently - the user may just have the mic unplugged.
            // Retry periodically so plugging it back in starts listening without restarting Play.
            if (Time.time >= vadNextDeviceRetry)
            {
                vadNextDeviceRetry = Time.time + 3f;
                Debug.LogWarning("[NPCClient] No microphone detected - hands-free is waiting. " +
                                 "Plug a mic in (Windows > Sound > Input) and it will start automatically.");
            }
            return;
        }
        vadClip = Microphone.Start(null, true, VadClipSeconds, VadSampleRate);
        vadLastPos = 0;
        ResetHandsFreeState();
        Debug.Log($"[NPCClient] Hands-free listening enabled on '{Microphone.devices[0]}' " +
                  $"(threshold {vadSpeechThreshold}, endpoint after {vadSilenceSeconds}s of silence).");
    }

    void ResetHandsFreeState()
    {
        vadInSpeech = false;
        vadSpeechElapsed = 0f;
        vadSilenceElapsed = 0f;
        vadBuffer.Clear();
    }

    void PollHandsFree()
    {
        if (!connectionEstablished || useTestAudioInsteadOfMic) return;
        if (isRecording) return;                 // manual push-to-talk takes priority
        if (vadClip == null) { StartHandsFree(); return; }

        // Never listen while she is talking (or just after) - otherwise she hears herself.
        if (audioSource.isPlaying || audioQueue.Count > 0)
        {
            vadResumeAt = Time.time + vadPostSpeechCooldown;
            if (vadInSpeech || vadBuffer.Count > 0) ResetHandsFreeState();
            vadLastPos = Microphone.GetPosition(null);
            return;
        }
        if (Time.time < vadResumeAt)
        {
            vadLastPos = Microphone.GetPosition(null);
            return;
        }
        // Wait for the current reply to arrive before capturing the next utterance.
        if (vadAwaitingReply)
        {
            if (IsInvoking("ReceiveDataParts") || IsInvoking("ReceiveData"))
            {
                vadLastPos = Microphone.GetPosition(null);
                return;
            }
            vadAwaitingReply = false;
        }

        int pos = Microphone.GetPosition(null);
        int total = vadClip.samples;
        if (pos == vadLastPos) return;

        int count = pos >= vadLastPos ? pos - vadLastPos : (total - vadLastPos) + pos;
        if (count <= 0) { vadLastPos = pos; return; }

        float[] chunk = new float[count];
        if (pos >= vadLastPos)
        {
            vadClip.GetData(chunk, vadLastPos);
        }
        else // wrapped around the ring buffer
        {
            int tail = total - vadLastPos;
            float[] a = new float[tail];
            vadClip.GetData(a, vadLastPos);
            Array.Copy(a, 0, chunk, 0, tail);
            if (pos > 0)
            {
                float[] b = new float[pos];
                vadClip.GetData(b, 0);
                Array.Copy(b, 0, chunk, tail, pos);
            }
        }
        vadLastPos = pos;

        // RMS loudness of this chunk.
        double sum = 0.0;
        for (int i = 0; i < chunk.Length; i++) sum += chunk[i] * chunk[i];
        float rms = (float)Math.Sqrt(sum / Math.Max(1, chunk.Length));
        float dt = chunk.Length / (float)VadSampleRate;

        if (vadDebugLevels)
        {
            vadDebugTimer += dt;
            if (vadDebugTimer >= 0.5f)
            {
                vadDebugTimer = 0f;
                Debug.Log($"[NPCClient][VAD] level={rms:F4} (threshold {vadSpeechThreshold:F4}) speaking={vadInSpeech}");
            }
        }

        bool loud = rms >= vadSpeechThreshold;

        if (!vadInSpeech)
        {
            if (loud)
            {
                vadInSpeech = true;
                vadSpeechElapsed = 0f;
                vadSilenceElapsed = 0f;
                vadBuffer.Clear();
                vadBuffer.AddRange(chunk);
            }
            // else: idle silence, drop it
            return;
        }

        // Currently capturing an utterance.
        vadBuffer.AddRange(chunk);
        vadSpeechElapsed += dt;
        vadSilenceElapsed = loud ? 0f : vadSilenceElapsed + dt;

        bool endedBySilence = vadSilenceElapsed >= vadSilenceSeconds;
        bool endedByLength = vadSpeechElapsed >= vadMaxSpeechSeconds;
        if (!endedBySilence && !endedByLength) return;

        float voiced = vadSpeechElapsed - vadSilenceElapsed;
        if (voiced < vadMinSpeechSeconds)
        {
            ResetHandsFreeState();   // too short - noise, not speech
            return;
        }

        float[] utterance = vadBuffer.ToArray();
        ResetHandsFreeState();
        Debug.Log($"[NPCClient] Utterance captured ({voiced:F1}s of speech) - sending.");
        SendUtterance(utterance);
    }

    /// <summary>Send captured samples using the same framing as the push-to-talk path.</summary>
    void SendUtterance(float[] samples)
    {
        try
        {
            var clip = AudioClip.Create("utterance", samples.Length, 1, VadSampleRate, false);
            clip.SetData(samples, 0);

            const string partFile = "Assets/handsfree.wav";
            SavWav.Save(partFile, clip);
            string filePath = Application.persistentDataPath + "/" + partFile;
            byte[] bytesData = System.IO.File.ReadAllBytes(filePath);

            byte[] lengthData = BitConverter.GetBytes(bytesData.Length);
            byte[] finishedFlag = new byte[1] { 1 };
            byte[] frame = new byte[bytesData.Length + lengthData.Length + 1];
            lengthData.CopyTo(frame, 0);
            finishedFlag.CopyTo(frame, lengthData.Length);
            bytesData.CopyTo(frame, lengthData.Length + 1);
            stream.Write(frame, 0, frame.Length);
            stream.Flush();

            vadAwaitingReply = true;
            SendReceiveRecordedAudio();
        }
        catch (Exception e)
        {
            Debug.LogError("[NPCClient] Failed to send hands-free utterance: " + e.Message);
            vadAwaitingReply = false;
        }
    }

    void StartSocketConnection()
    {
        try
        {
            client = new TcpClient(serverIP, serverPort);
            client.NoDelay = true;
            stream = client.GetStream();
            Debug.Log("Connected to Python server.");
            string message = "";
            if (LLM == LLMModels.HuggingFace)
                message = STTModel + "," + "HuggingFace_" + CustomLLM + "," + TTSModel;
            else
                message = STTModel + "," + LLM + "," + TTSModel;
            message += "," + SecretAPIKey + "," + AccessKeyID + "," + SecretKey + "," + APIKey;
            if (storeHistory)
                message += "," + "store";
            else
                message += "," + "nostore";
            message += "," + OpenAIVoice;
            message += "," + systemPrompt;
            byte[] messageData = System.Text.Encoding.ASCII.GetBytes(message);
            byte[] lengthData = BitConverter.GetBytes(messageData.Length);
            byte[] bytesDataWithLength = new byte[messageData.Length + lengthData.Length];
            lengthData.CopyTo(bytesDataWithLength, 0);
            messageData.CopyTo(bytesDataWithLength, lengthData.Length);
            stream.Write(bytesDataWithLength, 0, bytesDataWithLength.Length);
            Debug.Log("Message" + message + " sent to Python server.");
            Debug.Log("Save path: " + Application.persistentDataPath);
            InvokeRepeating("checkConnection", 0.1f, 0.1f);
        }
        catch (SocketException e)
        {
            Debug.Log("SocketException: " + e);
        }
    }

    void checkConnection()
    {
        byte [] buffer = new byte[3];
        if(stream.DataAvailable && stream.Read(buffer, 0, buffer.Length) == buffer.Length)
        {
            string response = System.Text.Encoding.ASCII.GetString(buffer);
            if(response == "!OK")
            {
                connectionEstablished = true;
                Debug.Log("Connection established with Python server.");
                CancelInvoke("checkConnection");
                if (handsFree && !useTestAudioInsteadOfMic) StartHandsFree();
                if (autoSendTestOnConnect)
                {
                    Debug.Log($"autoSendTestOnConnect=true; sending test audio in {autoSendDelaySeconds}s");
                    Invoke(nameof(SendTestAudio), autoSendDelaySeconds);
                }
            }
            else
            {
                Debug.Log("Connection failed with Python server.");
                CancelInvoke("checkConnection");
            }
        }
    }

    /// <summary>
    /// Headless test path: read a WAV from StreamingAssets and push it through the
    /// same wire protocol the mic flow uses. Lets us drive a full STT->LLM->TTS turn
    /// from inside Unity without needing a microphone.
    /// </summary>
    public void SendTestAudio()
    {
        if (!connectionEstablished)
        {
            Debug.LogWarning("[SendTestAudio] Not connected yet.");
            return;
        }
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, testAudioFileName);
        if (!System.IO.File.Exists(path))
        {
            Debug.LogError($"[SendTestAudio] File not found: {path}");
            return;
        }
        byte[] bytesData = System.IO.File.ReadAllBytes(path);
        Debug.Log($"[SendTestAudio] Sending {bytesData.Length} bytes from {path}. stream.CanWrite={stream.CanWrite} client.Connected={client.Connected}");
        byte[] lengthData = BitConverter.GetBytes(bytesData.Length);
        byte[] finishedFlag = new byte[1] {1};
        byte[] frame = new byte[bytesData.Length + lengthData.Length + 1];
        lengthData.CopyTo(frame, 0);
        finishedFlag.CopyTo(frame, lengthData.Length);
        bytesData.CopyTo(frame, lengthData.Length + 1);
        stream.Write(frame, 0, frame.Length);
        stream.Flush();
        Debug.Log($"[SendTestAudio] Frame ({frame.Length} bytes) sent + flushed. Starting receive loop.");
        SendReceiveRecordedAudio();
    }

    /*async*/ void StartRecording()
    {
        // Manual push-to-talk and hands-free share one mic device - release it first.
        if (vadClip != null)
        {
            Microphone.End(null);
            vadClip = null;
            ResetHandsFreeState();
        }
        isRecording = true;
        recordedClip = Microphone.Start(null, true, recordTime, 44100);
        coroutine = sendRecordingPart();
        StartCoroutine(coroutine);
        // Debug.Log("Recording started.");
    }

    IEnumerator sendRecordingPart()
    {
        int partNumber = 1;
        while(true)
        {
            yield return new WaitForSeconds(recordTime);
            string partFile = "Assets/recorded" + partNumber + ".wav";
            SavWav.Save(partFile, recordedClip);
            lastSentTime = Time.time;
            string filePath = Application.persistentDataPath + "/" + partFile;
            byte[] bytesData = System.IO.File.ReadAllBytes(filePath);
            Debug.Log("Audio data length: " + bytesData.Length + " bytes.");
            byte[] lengthData = new byte[4];
            lengthData = BitConverter.GetBytes(bytesData.Length);
            // finished flag 1 if recording is finished 0 if not
            byte[] finishedFlag = new byte[1] {0};
            byte[] bytesDataWithLength = new byte[bytesData.Length + lengthData.Length + 1];
            lengthData.CopyTo(bytesDataWithLength, 0);
            finishedFlag.CopyTo(bytesDataWithLength, lengthData.Length);
            bytesData.CopyTo(bytesDataWithLength, lengthData.Length+1);
            // Debug.Log("Audio data length: " + lengthData + " bytes." + " with length: " + BitConverter.ToInt32(lengthData, 0) + " bytes.");
            // Debug.Log("Audio data length with length: " + bytesDataWithLength.Length + " bytes.");
            stream.Write(bytesDataWithLength, 0, bytesDataWithLength.Length);
            Debug.Log("Audio data sent to Python server.");
        }
    }

    /*async*/ void StopRecording()
    {
        Microphone.End(null);
        StopCoroutine(coroutine);
        isRecording = false;
        string partFile = "Assets/recorded0.wav";
        var samples = new float[recordedClip.samples];
        int alreadySent = (int)((recordTime - (Time.time - lastSentTime)) * recordedClip.frequency * recordedClip.channels);
        // Debug.Log("Already sent: " + alreadySent + " samples. recording length: " + recordedClip.samples + " samples.");
        // Debug.Log("Time: " + Time.time + " lastSentTime: " + lastSentTime);
		recordedClip.GetData(samples, 0);
        for (int i = 0; i < alreadySent; i++)
        {
            samples[recordedClip.samples-i-1] = 0;
        }
        recordedClip = AudioClip.Create("trimmed", recordedClip.samples, recordedClip.channels, recordedClip.frequency, false);
        recordedClip.SetData(samples, 0);
        SavWav.Save(partFile, recordedClip);
        string filePath = Application.persistentDataPath + "/" + partFile;
        byte[] bytesData = System.IO.File.ReadAllBytes(filePath);
        // Debug.Log("Audio data length: " + bytesData.Length + " bytes.");
        byte[] lengthData = new byte[4];
        lengthData = BitConverter.GetBytes(bytesData.Length);
        byte[] finishedFlag = new byte[1] {1};
        byte[] bytesDataWithLength = new byte[bytesData.Length + lengthData.Length + 1];
        lengthData.CopyTo(bytesDataWithLength, 0);
        finishedFlag.CopyTo(bytesDataWithLength, lengthData.Length);
        bytesData.CopyTo(bytesDataWithLength, lengthData.Length+1);
        // Debug.Log("Audio data length: " + lengthData + " bytes." + " with length: " + BitConverter.ToInt32(lengthData, 0) + " bytes.");
        // Debug.Log("Audio data length with length: " + bytesDataWithLength.Length + " bytes.");
        stream.Write(bytesDataWithLength, 0, bytesDataWithLength.Length);
        Debug.Log("Audio data sent to Python server.");

        // Hand the mic back to hands-free listening (re-initialised on the next poll).
        vadClip = null;
        vadResumeAt = Time.time + vadPostSpeechCooldown;
        vadAwaitingReply = true;

        // Locally check whether the user asked for an image (no server change needed).
        if (imageGenerator == null) imageGenerator = GetComponent<GuideImageGenerator>();
        if (detectImageRequestsLocally && imageGenerator != null)
            StartCoroutine(DetectImageRequest(bytesData));
    }

    private float[] ConvertByteToFloat(byte[] array)
    {
        float[] floatArr = new float[array.Length / 4];
        for (int i = 0; i < floatArr.Length; i++) 
        {
            if (BitConverter.IsLittleEndian) 
                Array.Reverse(array, i * 4, 4);
            floatArr[i] = BitConverter.ToSingle(array, i * 4);
        }
        return floatArr;
    } 

    void SendReceiveRecordedAudio()
    {
        receiveDeadline = Time.realtimeSinceStartup + ReceiveTimeoutSeconds;
        if (LLM.ToString().Contains("_stream"))
        {
            Debug.Log("[SendReceiveRecordedAudio] Scheduling ReceiveDataParts (streaming LLM).");
            InvokeRepeating("ReceiveDataParts", 0.1f, 0.01f); // Adjust the repeat rate as needed
        }else
        {
            Debug.Log("[SendReceiveRecordedAudio] Scheduling ReceiveData (non-streaming LLM).");
            InvokeRepeating("ReceiveData", 0.1f, 0.01f); // Adjust the repeat rate as needed
        }
    }

    void ReceiveData()
    {
        if (ReceiveTimedOut("ReceiveData")) return;
        if(expectedDataLength == -1) // Check if we are waiting for the length info
        {
            if(stream.DataAvailable && stream.Read(lengthDataReceived, 0, lengthDataReceived.Length) == lengthDataReceived.Length)
            {
                int len = BitConverter.ToInt32(lengthDataReceived, 0);
                if (len < 0 || len > MaxFrameBytes)
                {
                    Debug.LogError($"[NPCClient] Bogus frame length {len} (stream out of sync). Dropping this response.");
                    AbortReceive("ReceiveData");
                    return;
                }
                expectedDataLength = len;
                receivedData = new byte[expectedDataLength];
                // Debug.Log("Expected audio data length: " + expectedDataLength + " bytes.");
            }
        }
        else // We are receiving the actual data
        {
            if(stream.DataAvailable)
            {
                int bytesRead = stream.Read(receivedData, totalBytesRead, expectedDataLength - totalBytesRead);
                totalBytesRead += bytesRead;

                if(totalBytesRead >= expectedDataLength) // Check if all data is received
                {
                    Debug.Log("All audio data received.");
                    ProcessReceivedData(); // Process the received audio data
                    expectedDataLength = -1; // Reset the expected data length
                    totalBytesRead = 0; // Reset the total bytes read
                    receivedData = null; // Reset the received data
                    lengthDataReceived = new byte[4]; // Reset the length data received
                    CancelInvoke("ReceiveData"); // Stop invoking this method
                }
            }
        }
    }

    void ReceiveDataParts()
    {
        if (ReceiveTimedOut("ReceiveDataParts")) return;
        try
        {
        if(expectedDataLength == -1) // Check if we are waiting for the length info
        {
            if(!awaitingFlagByte && stream.DataAvailable && stream.Read(lengthDataReceived, 0, lengthDataReceived.Length) == lengthDataReceived.Length)
            {
                pendingLength = BitConverter.ToInt32(lengthDataReceived, 0);
                if (pendingLength < 0 || pendingLength > MaxFrameBytes)
                {
                    Debug.LogError($"[NPCClient] Bogus frame length {pendingLength} (stream out of sync). Dropping this response.");
                    AbortReceive("ReceiveDataParts");
                    return;
                }
                awaitingFlagByte = true;   // flag byte may not have arrived yet
            }
            // Read the 1-byte flag only once it's actually available (never block the main thread).
            if (awaitingFlagByte)
            {
                if (!stream.DataAvailable) return;   // try again next tick
                lastPartFlag = stream.ReadByte();
                awaitingFlagByte = false;
                expectedDataLength = pendingLength;
                receivedData = new byte[expectedDataLength];
            }
        }
        else // We are receiving the actual data
        {
            if(stream.DataAvailable)
            {
                int bytesRead = stream.Read(receivedData, totalBytesRead, expectedDataLength - totalBytesRead);
                totalBytesRead += bytesRead;

                if(totalBytesRead >= expectedDataLength) // Check if all data is received
                {
                    if(lastPartFlag == 1)
                    {
                        if(expectedDataLength != 0){
                            Debug.Log("All audio data received for streaming.");
                            ProcessReceivedData(); // Process the received audio data
                        }
                        Debug.Log("Finish audio data received for streaming.");
                        expectedDataLength = -1; // Reset the expected data length
                        totalBytesRead = 0; // Reset the total bytes read
                        receivedData = null; // Reset the received data
                        lengthDataReceived = new byte[4]; // Reset the length data received
                        lastPartFlag = 0;
                        CancelInvoke("ReceiveDataParts"); // Stop invoking this method    
                    }
                    else
                    {
                        Debug.Log("Part of audio data received for streaming.");
                        ProcessReceivedData(); // Process the received audio data
                        expectedDataLength = -1; // Reset the expected data length
                        totalBytesRead = 0; // Reset the total bytes read
                        receivedData = null; // Reset the received data
                        lastPartFlag = 0;
                        lengthDataReceived = new byte[4]; // Reset the length data received
                    }             
                }
            }else if(lastPartFlag == 1)
            {
                Debug.Log("Finish audio data received for streaming without audio.");
                expectedDataLength = -1; // Reset the expected data length
                totalBytesRead = 0; // Reset the total bytes read
                receivedData = null; // Reset the received data
                lengthDataReceived = new byte[4]; // Reset the length data received
                lastPartFlag = 0;
                CancelInvoke("ReceiveDataParts"); // Stop invoking this method
            }
        }
        }
        catch (System.IO.IOException e)
        {
            Debug.LogError("[NPCClient] Connection lost while receiving: " + e.Message);
            AbortReceive("ReceiveDataParts");
            connectionEstablished = false;
        }
        catch (ObjectDisposedException)
        {
            AbortReceive("ReceiveDataParts");
            connectionEstablished = false;
        }
    }

    /// <summary>Reset receive state and stop the polling loop.</summary>
    void AbortReceive(string invokeName)
    {
        expectedDataLength = -1;
        pendingLength = -1;
        totalBytesRead = 0;
        receivedData = null;
        lengthDataReceived = new byte[4];
        lastPartFlag = 0;
        awaitingFlagByte = false;
        receiveDeadline = -1f;
        CancelInvoke(invokeName);
    }

    /// <summary>
    /// True (and aborts) if we've waited too long for a reply - which usually means the
    /// server hit an error (e.g. no API credits) and will never answer.
    /// </summary>
    bool ReceiveTimedOut(string invokeName)
    {
        if (receiveDeadline > 0f && Time.realtimeSinceStartup > receiveDeadline)
        {
            Debug.LogError($"[NPCClient] No response from server after {ReceiveTimeoutSeconds:0}s. " +
                           "The server probably errored - check its console/log. Press talk to try again.");
            AbortReceive(invokeName);
            return true;
        }
        return false;
    }

    /// <summary>
    /// If the just-received frame is an image-generation command (payload starts with
    /// ImageCmdMagic), route the remaining text to the GuideImageGenerator and return true.
    /// Otherwise return false so the frame is processed as audio.
    /// </summary>
    bool TryHandleImageCommand(byte[] data)
    {
        if (data == null || data.Length < ImageCmdMagic.Length) return false;
        for (int i = 0; i < ImageCmdMagic.Length; i++)
            if (data[i] != (byte)ImageCmdMagic[i]) return false;

        string prompt = System.Text.Encoding.UTF8.GetString(
            data, ImageCmdMagic.Length, data.Length - ImageCmdMagic.Length);
        Debug.Log($"[NPCClient] Image command received: \"{prompt}\"");

        if (imageGenerator == null) imageGenerator = GetComponent<GuideImageGenerator>();
        if (imageGenerator != null) imageGenerator.ShowImageByKey(prompt);
        else Debug.LogWarning("[NPCClient] Image command but no GuideImageGenerator on this GameObject.");
        return true;
    }

    // ---------------------------------------------------------------------
    // Local voice -> image-request detection (no Python server changes needed).
    // Transcribes the just-recorded WAV with OpenAI Whisper, and if the user asked
    // for a picture, extracts the requested subject and triggers generation.
    // ---------------------------------------------------------------------
    static readonly string[] ImageVerbs =
        { "draw", "paint", "sketch", "render", "generate", "create", "make", "show", "imagine", "design" };
    static readonly string[] ImageNouns =
        { "picture", "image", "photo", "drawing", "painting", "portrait", "illustration", "art", "sketch" };

    IEnumerator DetectImageRequest(byte[] wavBytes)
    {
        string key = imageGenerator != null ? imageGenerator.openAIApiKey : null;
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[NPCClient] Local image detection needs an OpenAI key on GuideImageGenerator.");
            yield break;
        }

        var form = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", wavBytes, "speech.wav", "audio/wav"),
            new MultipartFormDataSection("model", "whisper-1")
        };

        using (var req = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form))
        {
            req.SetRequestHeader("Authorization", "Bearer " + key);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[NPCClient] Whisper transcription failed ({req.responseCode}): {req.error}");
                yield break;
            }

            string transcript = ExtractJsonString(req.downloadHandler.text, "\"text\"");
            if (string.IsNullOrWhiteSpace(transcript)) yield break;
            Debug.Log($"[NPCClient] Heard: \"{transcript}\"");

            if (TryExtractImageRequest(transcript, out string subject))
            {
                Debug.Log($"[NPCClient] Image request detected -> \"{subject}\"");
                imageGenerator.GenerateFromRequest(subject);
            }
        }
    }

    // Returns true (and the requested subject) if the transcript is asking for a picture.
    static bool TryExtractImageRequest(string transcript, out string subject)
    {
        subject = null;
        string lower = transcript.ToLowerInvariant();

        // Conversational follow-ups that mean "show me the next one" - keep the picture loop going.
        string[] followUps = { "another", "next one", "next picture", "next photo", "one more",
                               "show me more", "different one", "a different", "again", "keep going" };
        foreach (var f in followUps)
            if (lower.Contains(f)) { subject = "another"; return true; }

        bool hasVerb = false;
        foreach (var v in ImageVerbs) if (ContainsWord(lower, v)) { hasVerb = true; break; }
        bool hasNoun = false;
        foreach (var n in ImageNouns) if (ContainsWord(lower, n)) { hasNoun = true; break; }
        // "draw/paint/sketch X" implies an image on its own; otherwise require a noun like "picture".
        bool drawingVerb = ContainsWord(lower, "draw") || ContainsWord(lower, "paint") ||
                           ContainsWord(lower, "sketch") || ContainsWord(lower, "illustrate");
        if (!(hasVerb && (hasNoun || drawingVerb))) return false;

        // Prefer the text after the last " of " ("...a picture of a red car" -> "a red car").
        int ofIdx = lower.LastIndexOf(" of ", StringComparison.Ordinal);
        string s = ofIdx >= 0 ? transcript.Substring(ofIdx + 4) : StripLeadingRequest(transcript);
        s = s.Trim().TrimEnd('.', '!', '?', ',');
        if (s.Length < 2) return false;
        subject = s;
        return true;
    }

    // Drops a leading "draw me a", "can you make a", etc., leaving the subject.
    static string StripLeadingRequest(string text)
    {
        string s = text.Trim();
        string lower = s.ToLowerInvariant();
        string[] leads = { "can you", "could you", "please", "hey guide", "guide", "i want you to",
                           "i'd like", "would you", "draw", "paint", "sketch", "render", "generate",
                           "create", "make", "show", "imagine", "design", "me", "us", "a ", "an ", "the ",
                           "picture", "image", "photo", "drawing", "of" };
        bool changed = true;
        while (changed)
        {
            changed = false;
            lower = s.ToLowerInvariant();
            foreach (var w in leads)
            {
                string token = w.EndsWith(" ") ? w : w + " ";
                if (lower.StartsWith(token, StringComparison.Ordinal))
                {
                    s = s.Substring(token.Length).TrimStart();
                    changed = true;
                    break;
                }
            }
        }
        return s;
    }

    static bool ContainsWord(string haystackLower, string word)
    {
        int i = haystackLower.IndexOf(word, StringComparison.Ordinal);
        while (i >= 0)
        {
            bool leftOk = i == 0 || !char.IsLetterOrDigit(haystackLower[i - 1]);
            int end = i + word.Length;
            bool rightOk = end >= haystackLower.Length || !char.IsLetterOrDigit(haystackLower[end]);
            if (leftOk && rightOk) return true;
            i = haystackLower.IndexOf(word, i + 1, StringComparison.Ordinal);
        }
        return false;
    }

    // Tiny extractor for the string value following a quoted JSON key.
    static string ExtractJsonString(string json, string quotedKey)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int k = json.IndexOf(quotedKey, StringComparison.Ordinal);
        if (k < 0) return null;
        int i = json.IndexOf('\"', k + quotedKey.Length);
        if (i < 0) return null;
        i++;
        var sb = new System.Text.StringBuilder();
        while (i < json.Length && json[i] != '\"')
        {
            if (json[i] == '\\' && i + 1 < json.Length)
            {
                char n = json[i + 1];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                i += 2;
            }
            else sb.Append(json[i++]);
        }
        return sb.ToString();
    }

    void ProcessReceivedData()
    {
        // Image-generation control frame? (server tagged the payload with the IMG marker)
        if (TryHandleImageCommand(receivedData)) return;

        AudioClip clip = BytesToAudioClip(receivedData, "agent_reply");
        if (clip != null)
        {
            audioQueue.Enqueue(clip);
            Debug.Log($"[ProcessReceivedData] Enqueued clip: {receivedData.Length} bytes -> {clip.samples} samples @ {clip.frequency} Hz x {clip.channels} ch");
        }
        else
        {
            Debug.LogError($"[ProcessReceivedData] Failed to decode WAV ({receivedData?.Length ?? 0} bytes)");
        }
    }

    /// <summary>
    /// Robust WAV->AudioClip converter. Searches for the "data" chunk rather than
    /// assuming it immediately follows "fmt " (which is what CUIfy's bundled
    /// WavUtility assumes — that breaks on Deepgram Aura's containers when extra
    /// chunks like LIST/INFO are present).
    /// </summary>
    static AudioClip BytesToAudioClip(byte[] wav, string name)
    {
        if (wav == null || wav.Length < 44) return null;
        ushort channels = BitConverter.ToUInt16(wav, 22);
        int sampleRate = BitConverter.ToInt32(wav, 24);
        ushort bitDepth = BitConverter.ToUInt16(wav, 34);

        int idx = 12; // skip "RIFF" + size + "WAVE"
        while (idx + 8 <= wav.Length)
        {
            string id = System.Text.Encoding.ASCII.GetString(wav, idx, 4);
            int size = BitConverter.ToInt32(wav, idx + 4);
            if (id == "data")
            {
                int dataStart = idx + 8;
                int dataSize = Math.Min(size, wav.Length - dataStart);
                int bytesPerSample = bitDepth / 8;
                if (bytesPerSample == 0) return null;
                int totalSamples = dataSize / bytesPerSample;
                float[] samples = new float[totalSamples];
                if (bitDepth == 16)
                {
                    for (int i = 0; i < totalSamples; i++)
                        samples[i] = BitConverter.ToInt16(wav, dataStart + i * 2) / 32768f;
                }
                else if (bitDepth == 8)
                {
                    for (int i = 0; i < totalSamples; i++)
                        samples[i] = (wav[dataStart + i] - 128) / 128f;
                }
                else
                {
                    Debug.LogError($"BytesToAudioClip: unsupported bit depth {bitDepth}");
                    return null;
                }
                int frameCount = totalSamples / Math.Max(1, (int)channels);
                AudioClip clip = AudioClip.Create(name, frameCount, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            idx += 8 + size;
        }
        Debug.LogError("BytesToAudioClip: no 'data' chunk found");
        return null;
    }
}
