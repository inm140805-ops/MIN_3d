using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class LaundrySkin
{
    internal static readonly Color Ink = new Color32(36, 44, 56, 255);
    internal static readonly Color Cream = new Color32(255, 250, 232, 255);
    internal static readonly Color Teal = new Color32(51, 117, 106, 255);
    internal static readonly Color Rust = new Color32(165, 78, 59, 255);
    internal static Sprite Board => Load("Enamel");
    internal static Sprite Paper => Load("Receipt");
    internal static Sprite Button => Load("PeachKey");
    internal static Sprite SelectedButton => Load("HoneyKey");
    internal static Sprite Ribbon => Load("Ribbon");
    internal static Sprite WasherCabinet => Load("WasherCabinet");
    internal static Sprite CycleKnob => Load("CycleKnob");
    internal static Sprite ReceiptPrinter => Load("ReceiptPrinter");
    internal static Sprite Perforation => Load("Perforation");
    private static TMP_FontAsset pixelFont;

    internal static void SetType(TMP_Text text, TMP_FontAsset fallback)
    {
        if (pixelFont == null)
            pixelFont = Resources.Load<TMP_FontAsset>("LaundrySkin/Galmuri11 SDF");
        var face = pixelFont != null ? pixelFont : fallback;
        if (face != null)
        {
            text.font = face;
            text.fontSharedMaterial = face.material;
        }
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        text.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
    }

    private static Sprite Load(string name) => Resources.Load<Sprite>("LaundrySkin/" + name);

    internal static void StylePrompt(GameObject prompt)
    {
        var image = prompt.GetComponentInChildren<Image>(true);
        var pixel = Resources.Load<Sprite>("LaundryPixel/Interact");
        if (image != null && pixel != null)
        {
            image.sprite = pixel; image.type = Image.Type.Simple; image.color = Color.white;
            image.rectTransform.sizeDelta = new Vector2(1.65f, .55f);
            foreach (Image child in image.GetComponentsInChildren<Image>(true))
                if (child != image) child.enabled = false;
        }
        foreach (TMP_Text label in prompt.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.text.Trim().Equals("E", System.StringComparison.OrdinalIgnoreCase)) { label.gameObject.SetActive(false); continue; }
            SetType(label, label.font);
            label.rectTransform.anchoredPosition = new Vector2(.23f, 0);
            label.rectTransform.sizeDelta = new Vector2(1.08f, .35f);
            label.fontSize = .16f; label.enableAutoSizing = true; label.fontSizeMin = .14f; label.fontSizeMax = .16f;
            label.alignment = TextAlignmentOptions.Center; label.margin = Vector4.zero;
            label.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            label.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            label.raycastTarget = false;
        }
    }

    internal static void Surface(Image image, Sprite sprite, Color tint)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = tint;
    }

    internal static Image Decoration(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        var image = go.GetComponent<Image>();
        var rect = image.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        image.sprite = sprite;
        bool isIllustration = sprite != null && sprite.border == Vector4.zero;
        image.type = isIllustration ? Image.Type.Simple : Image.Type.Sliced;
        image.preserveAspect = isIllustration;
        image.raycastTarget = false;
        return image;
    }
}
