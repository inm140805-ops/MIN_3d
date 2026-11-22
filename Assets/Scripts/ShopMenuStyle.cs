using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Restyles the existing controls without replacing their serialized button/slider callbacks.
[ExecuteAlways]
public class ShopMenuStyle : MonoBehaviour
{
    [SerializeField] private RectTransform pausePanel;
    [SerializeField] private RectTransform introPanel;
    [SerializeField] private RectTransform settingsPanel;
    [SerializeField] private RectTransform guidePanel;
    [SerializeField] private RectTransform reportPanel;
    [SerializeField] private TMP_FontAsset font;
    private static readonly Color Ink = LaundrySkin.Ink;
    private static readonly Color Mint = LaundrySkin.Teal;
    private readonly List<Button> washerButtons = new List<Button>();
    private RectTransform cycleKnob;
    private Image cycleLight;
    private Vector3 selectedButtonScale;
    private int selectedPauseIndex;
    private bool pauseWasVisible;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            ApplyStyles();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        float time = Time.unscaledTime;
        if (cycleKnob != null)
            cycleKnob.localRotation = Quaternion.Euler(0, 0, Mathf.Repeat(time * 36f, 360f));
        if (cycleLight != null)
        {
            float glow = 0.55f + 0.45f * Mathf.Sin(time * 3f);
            cycleLight.color = new Color(glow * 0.62f, glow, glow * 0.84f, 1f);
        }
        for (int i = 0; i < washerButtons.Count; i++)
        {
            Button button = washerButtons[i];
            if (button == null) continue;
            float phase = time * 2.4f + i * 0.32f;
            float pulse = 1f + 0.025f * Mathf.Max(0f, Mathf.Sin(phase));
            button.transform.localScale = selectedButtonScale * pulse;
        }
        if (pausePanel != null && pausePanel.gameObject.activeInHierarchy)
        {
            if (!pauseWasVisible) SetPauseSelection(selectedPauseIndex);
            pauseWasVisible = true;
            if (GameInput.GetKeyDown(KeyCode.UpArrow))
                SetPauseSelection(Mathf.Max(0, selectedPauseIndex - 1));
            if (GameInput.GetKeyDown(KeyCode.DownArrow))
                SetPauseSelection(Mathf.Min(washerButtons.Count - 1, selectedPauseIndex + 1));
        }
        else pauseWasVisible = false;
    }

    private void Start()
    {
        if (Application.isPlaying)
            ApplyStyles();
    }

    private void ApplyStyles()
    {
        if (introPanel != null)
        {
            StyleIntro();
            StyleSettings();
            return;
        }
        StylePause();
        StyleSettings();
        StyleGuide();
        StyleReport();
    }

    private void StyleIntro()
    {
        RectTransform logo = (RectTransform)introPanel.Find("Image");
        string[] names = { "StartButton", "SettingButton", "ExitButton" };
        for (int i = 0; i < names.Length; i++)
        {
            var button = introPanel.Find(names[i]).GetComponent<Button>();
            StyleButtonFace(button, i == 0);
            AddHoverSelection(button, i, false);
        }
        if (logo != null) logo.GetComponent<Image>().preserveAspect = true;
        var start = introPanel.Find(names[0]).GetComponent<Button>().GetComponent<Image>();
        if (start != null) LaundrySkin.Surface(start, LaundrySkin.SelectedButton, Color.white);
    }

    private void StylePause()
    {
        pausePanel.GetComponent<Image>().color = new Color(0.12f, 0.19f, 0.21f, 0.56f);
        RectTransform card = WasherMenu(pausePanel, "잠깐 쉬어가기", "Esc · 계속하기");
        washerButtons.Clear();
        string[] names = { "KeepGoing", "Setting", "Stop" };
        for (int i = 0; i < names.Length; i++)
        {
            var button = FindButton(pausePanel, names[i]);
            button.transform.SetParent(card, false);
            StyleWasherButton(button, i, i == 0);
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            AddHoverSelection(button, i, true);
        }
        washerButtons.Sort((a, b) => a.name.CompareTo(b.name));
        SetPauseSelection(Mathf.Clamp(selectedPauseIndex, 0, washerButtons.Count - 1));
    }

    private void StyleSettings()
    {
        LaundrySkin.Surface(settingsPanel.GetComponent<Image>(), LaundrySkin.Paper, Color.white);
        Place(settingsPanel, Vector2.zero, new Vector2(900, 560));
        TMP_Text title = settingsPanel.Find("Text (TMP)").GetComponent<TMP_Text>();
        title.text = "세탁소 설정";
        var ribbon = LaundrySkin.Decoration(settingsPanel, "Settings ribbon", LaundrySkin.Ribbon, new Vector2(0, 200), new Vector2(936, 94));
        ribbon.color = LaundrySkin.Teal;
        ribbon.transform.SetAsFirstSibling();
        PlaceText(title, new Vector2(0, 200), new Vector2(780, 60), 38);
        title.color = Color.white;
        string[] labels = { "Text (TMP) (1)", "Text (TMP) (2)" };
        string[] sliders = { "sensitivitySlider", "MusicSlider" };
        string[] inputs = { "SensitivityInput", "MusicInput" };
        for (int i = 0; i < 2; i++)
        {
            float y = 65 - i * 130;
            var label = settingsPanel.Find(labels[i]).GetComponent<TMP_Text>();
            label.text = i == 0 ? "마우스 감도" : "배경 음악";
            PlaceText(label, new Vector2(-300, y + 10), new Vector2(190, 48), 27);
            var slider = settingsPanel.Find(sliders[i]).GetComponent<Slider>();
            Place((RectTransform)slider.transform, new Vector2(10, y), new Vector2(380, 36));
            foreach (Image image in slider.GetComponentsInChildren<Image>(true))
                Surface(image, image.name == "Background" ? new Color32(211, 222, 215, 255) : Mint);
            if (slider.handleRect != null)
            {
                var handle = slider.handleRect.GetComponent<Image>();
                handle.sprite = LaundrySkin.CycleKnob;
                handle.preserveAspect = true;
                handle.color = Color.white;
                slider.handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 44);
                slider.handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 44);
            }
            var input = settingsPanel.Find(inputs[i]).GetComponent<TMP_InputField>();
            Place((RectTransform)input.transform, new Vector2(300, y), new Vector2(100, 64));
            Surface(input.GetComponent<Image>(), Color.white);
            foreach (TMP_Text text in input.GetComponentsInChildren<TMP_Text>(true)) CleanText(text, 28, Ink);
        }
        var back = settingsPanel.Find("Button").GetComponent<Button>();
        StyleButton(back, new Vector2(0, -195), new Vector2(360, 66), true);
        back.GetComponentInChildren<TMP_Text>(true).text = "메뉴로 돌아가기";
    }

    private void StyleGuide()
    {
        Place(guidePanel, Vector2.zero, new Vector2(820, 760));
        LaundrySkin.Surface(guidePanel.GetComponent<Image>(), LaundrySkin.Paper, Color.white);
        var title = guidePanel.Find("Text (TMP)").GetComponent<TMP_Text>();
        title.text = "옷 고르기 안내";
        var ribbon = LaundrySkin.Decoration(guidePanel, "Guide ribbon", LaundrySkin.Ribbon, new Vector2(0, 300), new Vector2(852, 90));
        ribbon.color = LaundrySkin.Teal;
        ribbon.transform.SetAsFirstSibling();
        PlaceText(title, new Vector2(0, 300), new Vector2(740, 60), 36);
        title.color = Color.white;
        BoxData[] clothes = FindAnyObjectByType<BoxManager>().boxDatas;
        for (int i = 0; i < 7; i++)
        {
            float y = 220 - i * 70;
            var label = guidePanel.Find("Text (TMP) (" + (i + 1) + ")").GetComponent<TMP_Text>();
            var icon = guidePanel.Find(i == 0 ? "Image" : "Image (" + i + ")").GetComponent<Image>();
            string clothingName = "";
            foreach (BoxData data in clothes) if (data.Icon == icon.sprite) { clothingName = data.BoxName; break; }
            label.text = FormatGuideRow(clothingName, label.text);
            PlaceText(label, new Vector2(55, y), new Vector2(590, 68), 26);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            Place(icon.rectTransform, new Vector2(-310, y), new Vector2(65, 65));
            icon.preserveAspect = true;
        }
        Text(guidePanel, "손님의 말에서 단서를 찾아보세요  ·  Tab 닫기", new Vector2(0, -320), new Vector2(750, 42), 21);
    }

    private static string FormatGuideRow(string clothingName, string text)
    {
        string prefix = "<b>" + clothingName + "</b>";
        string keywords = text.Trim().TrimStart(':', '：').Trim();
        while (keywords.StartsWith(prefix, System.StringComparison.Ordinal))
            keywords = keywords.Substring(prefix.Length).TrimStart(' ', '·');
        return prefix + "  ·  " + keywords;
    }

    private void StyleReport()
    {
        Place(reportPanel, Vector2.zero, new Vector2(790, 700));
        LaundrySkin.Surface(reportPanel.GetComponent<Image>(), LaundrySkin.Paper, Color.white);
        LaundrySkin.Decoration(reportPanel, "Receipt printer", LaundrySkin.ReceiptPrinter, new Vector2(0, 387), new Vector2(168, 112));
        Text(reportPanel, "오늘의 영업 정산서", new Vector2(0, 265), new Vector2(700, 52), 34);
        var divider = LaundrySkin.Decoration(reportPanel, "Receipt perforation", LaundrySkin.Perforation, new Vector2(0, -62), new Vector2(636, 4));
        divider.type = Image.Type.Tiled;
        divider.preserveAspect = false;
        string[] names = { "result", "completeOrders", "FallOrders", "TotalRevenue" };
        float[] positions = { 128, 52, -16, -108 };
        for (int i = 0; i < names.Length; i++)
            PlaceText(reportPanel.Find(names[i]).GetComponent<TMP_Text>(), new Vector2(0, positions[i]), new Vector2(710, 70), i == 3 ? 34 : 29);
        Transform win = reportPanel.Find("WinButton"), fail = reportPanel.Find("FallButton");
        Place((RectTransform)win, new Vector2(0, -235), new Vector2(680, 80));
        Place((RectTransform)fail, new Vector2(0, -235), new Vector2(680, 80));
        win.GetComponent<Image>().color = Color.clear;
        fail.GetComponent<Image>().color = Color.clear;
        Banner((RectTransform)win, "목표 달성", 431, 826, LaundrySkin.Teal);
        Banner((RectTransform)fail, "목표 미달", 431, 826, LaundrySkin.Rust);
        Button next = win.Find("KeepGoing").GetComponent<Button>();
        StyleButton(next, new Vector2(-170, 0), new Vector2(310, 72), true);
        next.GetComponentInChildren<TMP_Text>(true).text = "다음 날 시작";
        StyleButton(win.Find("Back").GetComponent<Button>(), new Vector2(170, 0), new Vector2(310, 72), false);
        StyleButton(fail.Find("Back").GetComponent<Button>(), new Vector2(170, 0), new Vector2(310, 72), false);
        // Failure previously offered only an exit; reuse the already-wired next-day button for a same-day retry.
        Button retry = FindButton(fail, "Retry day");
        if (retry == null) retry = Instantiate(next, fail);
        retry.name = "Retry day";
        StyleButton(retry, new Vector2(-170, 0), new Vector2(310, 72), true);
        retry.GetComponentInChildren<TMP_Text>(true).text = "같은 날 재도전";
    }

    private RectTransform WasherMenu(RectTransform parent, string display, string shortcut)
    {
        var cabinet = LaundrySkin.Decoration(parent, "Washing machine", LaundrySkin.WasherCabinet, Vector2.zero, new Vector2(768, 480));
        var rect = cabinet.rectTransform;
        rect.SetAsFirstSibling();
        var knob = LaundrySkin.Decoration(rect, "Animated wash cycle", LaundrySkin.CycleKnob, new Vector2(312, 184), new Vector2(68, 68));
        cycleKnob = knob.rectTransform;
        var light = LaundrySkin.Decoration(rect, "Wash cycle status light", LaundrySkin.CycleKnob, new Vector2(196, 184), new Vector2(18, 18));
        cycleLight = light;
        Text(rect, display, new Vector2(0, 180), new Vector2(312, 48), 30).color = Color.white;
        Text(rect, shortcut, new Vector2(164, -194), new Vector2(340, 32), 19);
        return rect;
    }
    private void StyleWasherButton(Button button, int index, bool primary)
    {
        StyleButton(button, new Vector2(164, 60 - index * 84), new Vector2(344, 72), primary);
        if (!washerButtons.Contains(button)) washerButtons.Add(button);
        selectedButtonScale = button.transform.localScale;
    }
    private void SetPauseSelection(int index)
    {
        if (washerButtons.Count == 0) return;
        selectedPauseIndex = Mathf.Clamp(index, 0, washerButtons.Count - 1);
        for (int i = 0; i < washerButtons.Count; i++)
            StyleButtonFace(washerButtons[i], i == selectedPauseIndex);
        if (Application.isPlaying && pausePanel != null && pausePanel.gameObject.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(washerButtons[selectedPauseIndex].gameObject);
    }
    internal void SelectPauseButton(int index) => SetPauseSelection(index);
    internal void SelectIntroButton(int index)
    {
        IntroManager manager = GetComponent<IntroManager>();
        if (manager != null) manager.SelectButton(index);
    }
    private void AddHoverSelection(Button button, int index, bool pause)
    {
        var hover = button.GetComponent<LaundryButtonHover>();
        if (hover == null) hover = button.gameObject.AddComponent<LaundryButtonHover>();
        hover.Configure(this, index, pause);
    }
    private static Button FindButton(Transform parent, string name)
    {
        foreach (Button button in parent.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;
        return null;
    }
    private void StyleButtonFace(Button button, bool selected)
    {
        if (button == null) return;
        var face = button.GetComponent<Image>();
        if (face != null)
            LaundrySkin.Surface(face, selected ? LaundrySkin.SelectedButton : LaundrySkin.Button, Color.white);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) CleanText(label, label.fontSize, Color.white);
    }
    private void Banner(RectTransform parent, string caption, float y, float width, Color color)
    {
        var ribbon = LaundrySkin.Decoration(parent, caption + " ribbon", LaundrySkin.Ribbon, new Vector2(0, y), new Vector2(width, 85));
        ribbon.color = color;
        Text(ribbon.rectTransform, caption, new Vector2(0, 4), new Vector2(width - 70, 66), 36).color = Color.white;
    }
    private TMP_Text Text(RectTransform parent, string content, Vector2 position, Vector2 size, float fontSize)
    {
        Transform existing = parent.Find(content);
        var go = existing != null ? existing.gameObject : new GameObject(content, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false); var text = go.GetComponent<TMP_Text>();
        text.text = content; PlaceText(text, position, size, fontSize); return text;
    }
    private void PlaceText(TMP_Text text, Vector2 position, Vector2 size, float fontSize)
    {
        Place(text.rectTransform, position, size); CleanText(text, fontSize, Ink);
    }
    private void CleanText(TMP_Text text, float size, Color color)
    {
        LaundrySkin.SetType(text, font);
        text.fontSize = size; text.enableAutoSizing = false; text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.45f);
        text.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);
        text.margin = Vector4.zero; text.alignment = TextAlignmentOptions.Center;
        text.UpdateMeshPadding();
        text.raycastTarget = false;
        foreach (var effect in text.GetComponents<BaseMeshEffect>()) effect.enabled = false;
    }
    private void StyleButton(Button button, Vector2 position, Vector2 size, bool primary)
    {
        Place((RectTransform)button.transform, position, size);
        LaundrySkin.Surface(button.GetComponent<Image>(), primary ? LaundrySkin.SelectedButton : LaundrySkin.Button, Color.white);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
        colors.pressedColor = new Color(0.82f, 0.86f, 0.83f); colors.selectedColor = Color.white;
        button.colors = colors;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        Place(text.rectTransform, Vector2.zero, size - new Vector2(24, 8));
        CleanText(text, 27, Ink);
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }
    private static void Surface(Image image, Color color)
    {
        image.sprite = null; image.type = Image.Type.Simple; image.color = color;
    }
}
