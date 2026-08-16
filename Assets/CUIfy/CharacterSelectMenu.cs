using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Game-style opening screen, rendered as a world-space panel inside the VR room.
///
/// On Play it takes over: both guide characters are disabled, the room shows a
/// "CHOOSE YOUR GUIDE" panel with one card per character, and the experience only
/// starts once the player picks one. The chosen character is enabled (which is what
/// connects its NPCClient to the server) and the panel disappears.
///
/// Selection works with the XR ray interactors (the panel is a normal world-space
/// Canvas, so the scene's XRUIInputModule drives it), with the mouse in the editor,
/// and with the 1 / 2 keys as a keyboard fallback.
/// </summary>
public class CharacterSelectMenu : MonoBehaviour
{
    [Serializable]
    public class CharacterOption
    {
        [Tooltip("GameObject of this character in the scene (disabled until chosen).")]
        public GameObject character;
        [Tooltip("Name shown on the card, e.g. \"Noa\".")]
        public string displayName = "Guide";
        [Tooltip("Short line under the name, e.g. \"Grown woman\".")]
        public string tagline = "";
        [Tooltip("Optional portrait. If empty the card just shows the text.")]
        public Texture portrait;
    }

    [Header("Characters")]
    public List<CharacterOption> options = new List<CharacterOption>();

    [Header("Panel Placement (world space)")]
    [Tooltip("Where the panel floats, in world coordinates.")]
    public Vector3 panelPosition = new Vector3(0f, 1.6f, 0.4f);
    [Tooltip("Euler rotation of the panel.")]
    public Vector3 panelRotation = new Vector3(0f, 180f, 0f);
    [Tooltip("Physical size of the panel in meters (width, height).")]
    public Vector2 panelSize = new Vector2(1.6f, 1.0f);

    [Header("Behaviour")]
    [Tooltip("Disable every character on Start so nothing runs until the player picks.")]
    public bool disableCharactersOnStart = true;
    [Tooltip("Also allow choosing with the 1 / 2 number keys (handy on desktop).")]
    public bool allowNumberKeys = true;

    [Header("Style")]
    public string title = "CHOOSE YOUR GUIDE";
    public Color panelColor = new Color(0.05f, 0.06f, 0.10f, 0.92f);
    public Color accentColor = new Color(0.35f, 0.75f, 1f, 1f);
    public Color cardColor = new Color(0.12f, 0.14f, 0.20f, 1f);

    /// <summary>Fires with the chosen character once a selection is made.</summary>
    public event Action<GameObject> OnCharacterChosen;

    GameObject panelRoot;
    bool chosen = false;

    void Start()
    {
        if (disableCharactersOnStart)
        {
            foreach (var o in options)
                if (o != null && o.character != null) o.character.SetActive(false);
        }
        BuildPanel();
    }

    void Update()
    {
        if (chosen || !allowNumberKeys || Keyboard.current == null) return;
        if (Keyboard.current.digit1Key.wasPressedThisFrame && options.Count > 0) Choose(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame && options.Count > 1) Choose(1);
    }

    // ---------------------------------------------------------------------
    // UI construction (built in code so the screen needs no scene wiring)
    // ---------------------------------------------------------------------
    void BuildPanel()
    {
        const float px = 1000f;                    // virtual pixels across the panel
        float pxHeight = px * (panelSize.y / panelSize.x);

        panelRoot = new GameObject("CharacterSelectPanel");
        panelRoot.transform.SetPositionAndRotation(panelPosition, Quaternion.Euler(panelRotation));

        var canvas = panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panelRoot.AddComponent<CanvasScaler>();
        panelRoot.AddComponent<GraphicRaycaster>();          // lets XR rays / mouse hit the buttons

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(px, pxHeight);
        rt.localScale = new Vector3(panelSize.x / px, panelSize.x / px, 1f);

        // Backdrop
        var bg = NewUI("Background", panelRoot.transform);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = panelColor;
        Stretch(bg);

        // Accent bar along the top
        var bar = NewUI("AccentBar", panelRoot.transform);
        var barImg = bar.gameObject.AddComponent<Image>();
        barImg.color = accentColor;
        bar.anchorMin = new Vector2(0f, 0.93f);
        bar.anchorMax = new Vector2(1f, 0.955f);
        bar.offsetMin = Vector2.zero; bar.offsetMax = Vector2.zero;

        // Title
        var titleRt = NewUI("Title", panelRoot.transform);
        var titleTxt = titleRt.gameObject.AddComponent<Text>();
        titleTxt.font = BuiltinFont();
        titleTxt.text = title;
        titleTxt.fontSize = 64;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = Color.white;
        titleRt.anchorMin = new Vector2(0f, 0.74f);
        titleRt.anchorMax = new Vector2(1f, 0.92f);
        titleRt.offsetMin = Vector2.zero; titleRt.offsetMax = Vector2.zero;

        // Cards
        int n = Mathf.Max(1, options.Count);
        const float margin = 0.04f;
        float cardW = (1f - margin * (n + 1)) / n;
        for (int i = 0; i < options.Count; i++)
        {
            float x0 = margin + i * (cardW + margin);
            BuildCard(options[i], i, x0, cardW);
        }

        // Hint line
        var hint = NewUI("Hint", panelRoot.transform);
        var hintTxt = hint.gameObject.AddComponent<Text>();
        hintTxt.font = BuiltinFont();
        hintTxt.text = allowNumberKeys ? "Point and click a card  -  or press 1 / 2" : "Point and click a card";
        hintTxt.fontSize = 26;
        hintTxt.alignment = TextAnchor.MiddleCenter;
        hintTxt.color = new Color(1f, 1f, 1f, 0.55f);
        hint.anchorMin = new Vector2(0f, 0.005f);
        hint.anchorMax = new Vector2(1f, 0.075f);
        hint.offsetMin = Vector2.zero; hint.offsetMax = Vector2.zero;
    }

    void BuildCard(CharacterOption opt, int index, float x0, float cardW)
    {
        var card = NewUI($"Card_{index}", panelRoot.transform);
        card.anchorMin = new Vector2(x0, 0.10f);
        card.anchorMax = new Vector2(x0 + cardW, 0.70f);
        card.offsetMin = Vector2.zero; card.offsetMax = Vector2.zero;

        var img = card.gameObject.AddComponent<Image>();
        img.color = cardColor;

        var btn = card.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;
        int captured = index;
        btn.onClick.AddListener(() => Choose(captured));

        // Portrait (or a placeholder block in the accent colour)
        var port = NewUI("Portrait", card);
        port.anchorMin = new Vector2(0.08f, 0.30f);
        port.anchorMax = new Vector2(0.92f, 0.94f);
        port.offsetMin = Vector2.zero; port.offsetMax = Vector2.zero;
        if (opt.portrait != null)
        {
            var raw = port.gameObject.AddComponent<RawImage>();
            raw.texture = opt.portrait;
            raw.raycastTarget = false;
        }
        else
        {
            var ph = port.gameObject.AddComponent<Image>();
            ph.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f);
            ph.raycastTarget = false;
        }

        // Name
        var nameRt = NewUI("Name", card);
        var nameTxt = nameRt.gameObject.AddComponent<Text>();
        nameTxt.font = BuiltinFont();
        nameTxt.text = opt.displayName;
        nameTxt.fontSize = 44;
        nameTxt.fontStyle = FontStyle.Bold;
        nameTxt.alignment = TextAnchor.MiddleCenter;
        nameTxt.color = Color.white;
        nameTxt.raycastTarget = false;
        nameRt.anchorMin = new Vector2(0f, 0.15f);
        nameRt.anchorMax = new Vector2(1f, 0.30f);
        nameRt.offsetMin = Vector2.zero; nameRt.offsetMax = Vector2.zero;

        // Tagline
        var tagRt = NewUI("Tagline", card);
        var tagTxt = tagRt.gameObject.AddComponent<Text>();
        tagTxt.font = BuiltinFont();
        tagTxt.text = opt.tagline;
        tagTxt.fontSize = 28;
        tagTxt.alignment = TextAnchor.MiddleCenter;
        tagTxt.color = accentColor;
        tagTxt.raycastTarget = false;
        tagRt.anchorMin = new Vector2(0f, 0.03f);
        tagRt.anchorMax = new Vector2(1f, 0.15f);
        tagRt.offsetMin = Vector2.zero; tagRt.offsetMax = Vector2.zero;
    }

    // ---------------------------------------------------------------------
    public void Choose(int index)
    {
        if (chosen || index < 0 || index >= options.Count) return;
        var opt = options[index];
        if (opt == null || opt.character == null)
        {
            Debug.LogWarning($"[CharacterSelect] Option {index} has no character assigned.");
            return;
        }
        chosen = true;

        for (int i = 0; i < options.Count; i++)
            if (options[i] != null && options[i].character != null)
                options[i].character.SetActive(i == index);

        if (panelRoot != null) panelRoot.SetActive(false);
        Debug.Log($"[CharacterSelect] Chose '{opt.displayName}' ({opt.character.name}).");
        try { OnCharacterChosen?.Invoke(opt.character); } catch (Exception e) { Debug.LogException(e); }
    }

    // ---------------------------------------------------------------------
    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Font _font;
    static Font BuiltinFont()
    {
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _font;
    }
}
