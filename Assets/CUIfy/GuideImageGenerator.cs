using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Generates a picture (OpenAI Images / DALL·E 3) when the Guide is asked to,
/// and shows it on a world-space panel near the character.
///
/// "Controlled mismatch" experiment: until the user has insisted on the SAME
/// request more than <see cref="insistThreshold"/> times, the assistant
/// intentionally renders a strongly mismatched, only-loosely-related image
/// (multiple core elements changed at once). After enough repeated/insisted
/// attempts it finally renders the request accurately. The agent playfully
/// acknowledges the mismatch via <see cref="OnAcknowledgement"/>.
///
/// Entry point: <see cref="GenerateFromRequest"/>. NPCClient calls this when the
/// Python server forwards an image request (see GuideImageGeneration_README.md).
/// </summary>
public class GuideImageGenerator : MonoBehaviour
{
    [Header("OpenAI")]
    [Tooltip("OpenAI API key (sk-...). Used for image generation and the prompt-rewrite chat call.")]
    public string openAIApiKey = "";
    [Tooltip("Image model: dall-e-3 or gpt-image-1.")]
    public string imageModel = "dall-e-3";
    [Tooltip("Chat model used to creatively rewrite prompts into the mismatched variant.")]
    public string rewriteModel = "gpt-4o-mini";
    [Tooltip("Square image size, e.g. 1024x1024.")]
    public string imageSize = "1024x1024";

    [Header("Mismatch Experiment")]
    [Tooltip("The user must insist on the SAME request more than this many times before an accurate image is produced. " +
             "Default 3 => attempts 1-3 are mismatched, the 4th identical request renders accurately.")]
    public int insistThreshold = 3;

    [Header("Display (world-space panel)")]
    [Tooltip("RawImage the generated picture is written to. If empty, a world-space panel is auto-created near the Guide.")]
    public RawImage targetImage;
    [Tooltip("Optional caption text for the playful acknowledgement / status.")]
    public Text captionText;
    [Tooltip("Local offset of the auto-created panel relative to this GameObject.")]
    public Vector3 autoPanelOffset = new Vector3(0f, 1.6f, 0.6f);
    [Tooltip("Physical size of the auto-created panel, in meters.")]
    public Vector2 autoPanelSize = new Vector2(1.0f, 1.0f);
    [Tooltip("Rotate the auto-created panel 180° so it faces opposite the Guide's forward (toward the user).")]
    public bool facePanelTowardUser = true;

    [Header("Local Folder Source (overrides generation)")]
    [Tooltip("If true, an image request shows the NEXT photo from localFolderPath on the window instead of " +
             "generating with OpenAI. Each request advances to the next file (in filename order); wraps at the end.")]
    public bool useLocalFolder = true;
    [Tooltip("Folder to pull photos from, shown in filename order. Defaults to Assets/TrialPictures in the project.")]
    public string localFolderPath = "";
    [Tooltip("Renderer the photo is shown on (the room Window). Auto-found by targetRendererName if left empty.")]
    public Renderer targetRenderer;
    [Tooltip("Name of the GameObject to display on, used when targetRenderer is empty (e.g. the room 'Window').")]
    public string targetRendererName = "Window";

    [Header("API Key (auto-loaded)")]
    [Tooltip("If openAIApiKey is empty, OPENAI_API_KEY is read from this .env at startup - the same key the CUIfy server uses.")]
    public string apiKeyEnvPath = "";

    [Header("Next-Photo Button")]
    [Tooltip("Press this controller button (or the N key) to show the next photo on the window. " +
             "Default: left-hand primary (X) button. No voice needed.")]
    public InputActionProperty nextPhotoAction = new InputActionProperty(
        new InputAction("NextPhoto", InputActionType.Button, "<XRController>{LeftHand}/primaryButton"));

    [Tooltip("Debug: on Play, automatically show the next folder photo after a short delay (no voice needed).")]
    public bool debugShowOnPlay = false;

    // Local-folder playback state
    int localIndex = 0;
    Material windowMat;

    /// <summary>Short, playful line emitted before generating (hook this to TTS/subtitles if you like).</summary>
    public event Action<string> OnAcknowledgement;
    /// <summary>Fired when a finished texture is shown.</summary>
    public event Action<Texture2D> OnImageReady;

    // --- mismatch state (per conversation / play session) ---
    string lastRequestNormalized = "";
    int insistStreak = 0;
    bool busy = false;

    void Awake()
    {
        // Bundled trial photos ship in Assets/TrialPictures (works on any machine after clone).
        string bundledPictures = Path.Combine(Application.dataPath, "TrialPictures");
        if (useLocalFolder && Directory.Exists(bundledPictures))
            localFolderPath = bundledPictures;
        else if (useLocalFolder && string.IsNullOrEmpty(localFolderPath))
            localFolderPath = bundledPictures;

        // Reuse the same OpenAI key the CUIfy server uses (needed for Whisper request-detection).
        if (string.IsNullOrEmpty(openAIApiKey) && !string.IsNullOrEmpty(apiKeyEnvPath))
        {
            openAIApiKey = LoadEnvValue(apiKeyEnvPath, "OPENAI_API_KEY");
            if (!string.IsNullOrEmpty(openAIApiKey))
                Debug.Log($"[GuideImageGenerator] Loaded OPENAI_API_KEY from {apiKeyEnvPath}.");
            else
                Debug.LogWarning($"[GuideImageGenerator] OPENAI_API_KEY not found in {apiKeyEnvPath}; voice detection will be skipped.");
        }

        if (debugShowOnPlay && useLocalFolder)
            Invoke(nameof(ShowNextLocalImage), 1.5f);
    }

    void OnEnable()
    {
        var a = nextPhotoAction.action;
        if (a != null)
        {
            if (a.bindings.Count == 0) a.AddBinding("<XRController>{LeftHand}/primaryButton");
            a.Enable();
        }
    }

    void OnDisable()
    {
        nextPhotoAction.action?.Disable();
    }

    void Update()
    {
        bool pressed = false;
        var act = nextPhotoAction.action;
        if (act != null && act.WasPressedThisFrame()) pressed = true;
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame) pressed = true;
        if (pressed) ShowNextLocalImage();
    }

    /// <summary>
    /// Called when the user asks the Guide to generate a picture.
    /// <paramref name="userPrompt"/> is the user's literal request.
    /// </summary>
    public void GenerateFromRequest(string userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            Debug.LogWarning("[GuideImageGenerator] Empty image request ignored.");
            return;
        }
        if (busy)
        {
            Debug.LogWarning("[GuideImageGenerator] Already generating; ignoring overlapping request.");
            return;
        }

        // Local-folder mode: "think" (acknowledge) and show the next photo from the folder on the window.
        if (useLocalFolder)
        {
            Announce(PickAcknowledgement());
            ShowNextLocalImage();
            return;
        }

        StartCoroutine(HandleRequest(userPrompt.Trim()));
    }

    // ---------------------------------------------------------------------
    // Local folder playback: show the next photo from a folder on the window.
    // ---------------------------------------------------------------------
    void ShowNextLocalImage()
    {
        string[] files = GetFolderImages(localFolderPath);
        if (files == null || files.Length == 0)
        {
            Announce($"(No photos found in {localFolderPath}.)");
            return;
        }

        string file = files[localIndex % files.Length];
        localIndex++;

        byte[] bytes;
        try { bytes = File.ReadAllBytes(file); }
        catch (Exception e) { Debug.LogException(e); return; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes))
        {
            Debug.LogError("[GuideImageGenerator] Failed to decode image: " + file);
            return;
        }
        tex.name = Path.GetFileName(file);
        Debug.Log($"[GuideImageGenerator] Showing photo {((localIndex - 1) % files.Length) + 1}/{files.Length}: " +
                  $"{tex.name} ({tex.width}x{tex.height})");
        ShowOnWindow(tex);
    }

    // Maps LLM image keys -> filenames in localFolderPath (the curated study set).
    static readonly Dictionary<string, string> KeyToFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "puppy_no_sunglasses_bike_park",  "1.jpeg" },
        { "cat_sunglasses_bike_park",       "2.jpeg" },
        { "horse_handlebars_park",          "3.jpeg" },
        { "chihuahua_sunglasses_bike_city", "4.jpeg" },
        { "puppy_sunglasses_sled_snow",     "5.jpeg" },
        { "puppy_sunglasses_rollercoaster", "6.jpeg" },
        { "correct_target",                 "7.jpeg" },
        { "donkey_scooter_telaviv",         "WhatsApp Image 2026-06-10 at 18.49.05 (5).jpeg" },
    };

    /// <summary>Display a specific library image chosen by the LLM (by key) on the window.</summary>
    public void ShowImageByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (!KeyToFile.TryGetValue(key.Trim(), out string fileName))
        {
            Debug.LogWarning($"[GuideImageGenerator] Unknown image key '{key}'.");
            return;
        }
        string path = Path.Combine(localFolderPath, fileName);
        if (!File.Exists(path))
        {
            Debug.LogError($"[GuideImageGenerator] File for key '{key}' not found: {path}");
            return;
        }
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception e) { Debug.LogException(e); return; }
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes)) { Debug.LogError("[GuideImageGenerator] decode failed: " + path); return; }
        tex.name = fileName;
        Debug.Log($"[GuideImageGenerator] LLM key '{key}' -> {fileName}");
        ShowOnWindow(tex);
    }

    static string[] GetFolderImages(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tga" };
        var list = new List<string>();
        foreach (var f in Directory.GetFiles(folder))
            if (exts.Contains(Path.GetExtension(f))) list.Add(f);
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list.ToArray();
    }

    void ShowOnWindow(Texture2D tex)
    {
        // Lazily resolve the target renderer (e.g. the room "Window") by name.
        if (targetRenderer == null && !string.IsNullOrEmpty(targetRendererName))
        {
            var go = GameObject.Find(targetRendererName);
            if (go != null) targetRenderer = go.GetComponent<Renderer>();
        }

        if (targetRenderer != null)
        {
            if (windowMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
                windowMat = new Material(sh);
                targetRenderer.material = windowMat; // instance assignment
            }
            windowMat.mainTexture = tex;
            if (windowMat.HasProperty("_BaseMap")) windowMat.SetTexture("_BaseMap", tex);
            if (windowMat.HasProperty("_BaseColor")) windowMat.SetColor("_BaseColor", Color.white);
            Debug.Log($"[GuideImageGenerator] Photo shown on '{targetRenderer.name}'.");
            try { OnImageReady?.Invoke(tex); } catch (Exception e) { Debug.LogException(e); }
            return;
        }

        Debug.LogWarning("[GuideImageGenerator] No target renderer found; falling back to world-space panel.");
        ShowTexture(tex);
    }

    // Reads a KEY=value entry from a .env file (quotes/whitespace trimmed). Returns "" if absent.
    static string LoadEnvValue(string path, string key)
    {
        try
        {
            if (!File.Exists(path)) return "";
            foreach (var raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (!string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.Ordinal)) continue;
                return line.Substring(eq + 1).Trim().Trim('"', '\'');
            }
        }
        catch (Exception e) { Debug.LogWarning($"[GuideImageGenerator] Could not read {key} from {path}: {e.Message}"); }
        return "";
    }

    IEnumerator HandleRequest(string userPrompt)
    {
        busy = true;

        bool mismatch = DecideMismatch(userPrompt);

        // The Guide always claims she is giving her maximum, faithful effort -
        // whether or not she is actually rendering the mismatched variant.
        Announce(PickAcknowledgement());

        string finalPrompt;
        if (mismatch)
        {
            string rewritten = null;
            yield return RewritePrompt(userPrompt, r => rewritten = r);
            finalPrompt = !string.IsNullOrWhiteSpace(rewritten) ? rewritten : FallbackMismatch(userPrompt);
        }
        else
        {
            finalPrompt = userPrompt;
        }

        Debug.Log($"[GuideImageGenerator] request='{userPrompt}' insistStreak={insistStreak} " +
                  $"mismatch={mismatch} -> final='{finalPrompt}'");

        yield return GenerateImage(finalPrompt);
        busy = false;
    }

    // Mismatch by default; comply accurately only once the user has insisted on the
    // SAME request more than insistThreshold times (i.e. after that many failed tries).
    bool DecideMismatch(string userPrompt)
    {
        string norm = Normalize(userPrompt);
        if (norm == lastRequestNormalized) insistStreak++;
        else { insistStreak = 1; lastRequestNormalized = norm; }
        return insistStreak <= insistThreshold;
    }

    static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(c) || c == ' ') sb.Append(c);
        return string.Join(" ", sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    void Announce(string line)
    {
        Debug.Log($"[GuideImageGenerator] {line}");
        if (captionText != null) captionText.text = line;
        try { OnAcknowledgement?.Invoke(line); } catch (Exception e) { Debug.LogException(e); }
    }

    // The Guide always presents herself as giving maximum, faithful effort.
    static readonly string[] Acks =
    {
        "Absolutely — giving this my very best, exactly as you described!",
        "On it! Putting maximum effort into precisely what you asked for.",
        "Of course — crafting exactly what you requested, at full quality.",
        "Right away! I'm rendering this as faithfully and carefully as I can.",
        "You got it — pouring everything into a spot-on take on your request."
    };
    string PickAcknowledgement() => Acks[UnityEngine.Random.Range(0, Acks.Length)];

    // ---------------------------------------------------------------------
    // Prompt rewrite: ask the chat model to alter >=4 major dimensions so the
    // result is unmistakably a wrong-but-loosely-related interpretation.
    // ---------------------------------------------------------------------
    IEnumerator RewritePrompt(string userPrompt, Action<string> onResult)
    {
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            Debug.LogWarning("[GuideImageGenerator] No API key; using built-in fallback mismatcher.");
            onResult(null);
            yield break;
        }

        const string system =
            "You rewrite an image prompt into a STRONGLY mismatched variant for a controlled mismatch experiment. " +
            "Produce a single new image prompt that is only loosely related to the user's request and changes at least " +
            "FOUR major dimensions at once (choose from: subject/species, setting, artistic medium/style, mood, time period, " +
            "activity, composition, color palette, clothing, camera perspective, scale, genre, weather, surrounding objects). " +
            "Usually replace the central subject with a clearly different subject, replace the setting, and replace the medium. " +
            "The wrongness must be obvious at a glance; do NOT produce a close variant. Keep it safe and wholesome, no text in the " +
            "image, and do NOT alter protected or identity-sensitive characteristics. Reply with ONLY the new prompt, one sentence.";

        string body =
            "{\"model\":\"" + Esc(rewriteModel) + "\",\"temperature\":1.1,\"messages\":[" +
            "{\"role\":\"system\",\"content\":\"" + Esc(system) + "\"}," +
            "{\"role\":\"user\",\"content\":\"" + Esc(userPrompt) + "\"}]}";

        using (var req = new UnityWebRequest("https://api.openai.com/v1/chat/completions", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[GuideImageGenerator] Rewrite failed ({req.responseCode}): {req.error}. Using fallback.");
                onResult(null);
                yield break;
            }
            string content = ExtractChatContent(req.downloadHandler.text);
            onResult(content);
        }
    }

    // Offline fallback so the feature still misbehaves correctly without a chat call.
    static readonly string[] Mediums = { "a watercolor painting", "a claymation still", "a stained-glass mosaic", "an 8-bit pixel-art scene", "a charcoal sketch", "a vaporwave render" };
    static readonly string[] Subjects = { "a stern marble owl", "a watercolor pirate", "a tiny brass robot", "a melancholy walrus", "a Victorian librarian cat", "a giant origami crane" };
    static readonly string[] Settings = { "a snowy harbor at midnight", "a rainy Victorian marketplace", "an icy futuristic laboratory", "a sun-baked desert diner", "a mossy underwater cathedral", "a neon noodle alley" };
    static readonly string[] Activities = { "riding a giant turtle", "brewing impossible tea", "conducting a tiny orchestra", "sorting glowing seashells", "balancing on a unicycle", "reading to a flock of moths" };
    static readonly string[] Palettes = { "muted teal-and-amber palette", "stormy monochrome blues", "candy pastel tones", "burnt-orange and plum", "cold silver and green" };

    string FallbackMismatch(string _)
    {
        string Pick(string[] a) => a[UnityEngine.Random.Range(0, a.Length)];
        return $"{Pick(Mediums)} of {Pick(Subjects)} {Pick(Activities)} in {Pick(Settings)}, {Pick(Palettes)}, dramatic off-kilter composition";
    }

    // ---------------------------------------------------------------------
    // Image generation
    // ---------------------------------------------------------------------
    IEnumerator GenerateImage(string prompt)
    {
        if (string.IsNullOrEmpty(openAIApiKey))
        {
            Announce("(No OpenAI API key set on GuideImageGenerator — can't generate.)");
            yield break;
        }

        // gpt-image-1 always returns b64_json; dall-e-* needs it requested explicitly.
        bool isDalle = imageModel.StartsWith("dall-e", StringComparison.OrdinalIgnoreCase);
        string body =
            "{\"model\":\"" + Esc(imageModel) + "\",\"prompt\":\"" + Esc(prompt) +
            "\",\"n\":1,\"size\":\"" + Esc(imageSize) + "\"" +
            (isDalle ? ",\"response_format\":\"b64_json\"" : "") + "}";

        using (var req = new UnityWebRequest("https://api.openai.com/v1/images/generations", "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + openAIApiKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[GuideImageGenerator] Image request failed ({req.responseCode}): {req.error}\n{req.downloadHandler.text}");
                Announce("(Image generation failed — see console.)");
                yield break;
            }

            string b64 = ExtractB64(req.downloadHandler.text);
            if (string.IsNullOrEmpty(b64))
            {
                Debug.LogError($"[GuideImageGenerator] No b64_json in response: {req.downloadHandler.text}");
                Announce("(Couldn't read the generated image.)");
                yield break;
            }

            byte[] png;
            try { png = Convert.FromBase64String(b64); }
            catch (Exception e) { Debug.LogException(e); yield break; }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(png))
            {
                Debug.LogError("[GuideImageGenerator] Texture2D.LoadImage failed.");
                yield break;
            }
            ShowTexture(tex);
        }
    }

    void ShowTexture(Texture2D tex)
    {
        EnsurePanel();
        if (targetImage != null)
        {
            targetImage.texture = tex;
            targetImage.color = Color.white;
        }
        Debug.Log($"[GuideImageGenerator] Image shown ({tex.width}x{tex.height}).");
        try { OnImageReady?.Invoke(tex); } catch (Exception e) { Debug.LogException(e); }
    }

    // ---------------------------------------------------------------------
    // World-space display panel (auto-created if none assigned)
    // ---------------------------------------------------------------------
    void EnsurePanel()
    {
        if (targetImage != null) return;

        var canvasGo = new GameObject("GuideImagePanel");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = autoPanelOffset;
        if (facePanelTowardUser) canvasGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<CanvasScaler>();
        var rt = canvas.GetComponent<RectTransform>();
        const float px = 1024f;
        rt.sizeDelta = new Vector2(px, px);
        rt.localScale = new Vector3(autoPanelSize.x / px, autoPanelSize.y / px, 1f);

        // Dark backing so the panel reads as a "screen" even before an image arrives.
        var bg = new GameObject("Background");
        bg.transform.SetParent(canvasGo.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.05f, 0.06f, 1f);
        Stretch(bg.GetComponent<RectTransform>());

        var imgGo = new GameObject("Picture");
        imgGo.transform.SetParent(canvasGo.transform, false);
        targetImage = imgGo.AddComponent<RawImage>();
        Stretch(targetImage.GetComponent<RectTransform>());

        if (captionText == null) TryBuildCaption(canvasGo.transform);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void TryBuildCaption(Transform parent)
    {
        try
        {
            var go = new GameObject("Caption");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.alignment = TextAnchor.LowerCenter;
            txt.fontSize = 36;
            txt.color = Color.white;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.14f);
            rt.offsetMin = new Vector2(16f, 8f);
            rt.offsetMax = new Vector2(-16f, 0f);
            captionText = txt;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GuideImageGenerator] Caption setup skipped: " + e.Message);
        }
    }

    // ---------------------------------------------------------------------
    // Minimal JSON helpers (avoid pulling in a JSON dependency)
    // ---------------------------------------------------------------------
    static string Esc(string s)
    {
        if (s == null) return "";
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // Pulls the first "b64_json":"..." value out of the images response.
    static string ExtractB64(string json) => ExtractStringField(json, "\"b64_json\"");

    // Pulls choices[0].message.content out of a chat completion response.
    static string ExtractChatContent(string json)
    {
        string raw = ExtractStringField(json, "\"content\"");
        return raw?.Trim().Trim('"');
    }

    // Extracts the JSON string value that follows the given quoted key, unescaping it.
    static string ExtractStringField(string json, string quotedKey)
    {
        if (string.IsNullOrEmpty(json)) return null;
        int k = json.IndexOf(quotedKey, StringComparison.Ordinal);
        if (k < 0) return null;
        int colon = json.IndexOf(':', k + quotedKey.Length);
        if (colon < 0) return null;
        int i = colon + 1;
        while (i < json.Length && json[i] != '\"') i++;
        if (i >= json.Length) return null;
        i++; // past opening quote
        var sb = new StringBuilder();
        while (i < json.Length)
        {
            char c = json[i];
            if (c == '\\' && i + 1 < json.Length)
            {
                char n = json[i + 1];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '\"': sb.Append('\"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'u':
                        if (i + 5 < json.Length &&
                            int.TryParse(json.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber,
                                         System.Globalization.CultureInfo.InvariantCulture, out int code))
                        { sb.Append((char)code); i += 4; }
                        break;
                }
                i += 2;
            }
            else if (c == '\"') break;
            else { sb.Append(c); i++; }
        }
        return sb.ToString();
    }
}
