#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Input and observation only. This driver never changes money, orders, transforms or time scale.
public class LaundryPlaytestDriver : MonoBehaviour
{
    public static Action<string, string> LogEvent;
    public static Func<string, string> RequestCapture;
    public static Action<string, bool> FinishSession;
    private Player player;
    private Grab grab;
    private CoreLoopManager loop;
    private float horizontal, vertical, yaw, pitch;
    private float commandUntil;
    private Key tap;
    private bool tapPending;
    private bool clickPending;
    private Vector2 pointerPosition;
    private float lastMoney;
    private int frames;
    private float frameSeconds, worstFrame;
    private string lastCapture;

    private void Awake()
    {
        player = FindAnyObjectByType<Player>();
        grab = player != null ? player.GetComponent<Grab>() : null;
        loop = FindAnyObjectByType<CoreLoopManager>();
        pointerPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        InputSystem.onBeforeUpdate += QueueInput;
        LogEvent("driver", "Virtual keyboard/mouse connected; no gameplay state bypass");
        StartCoroutine(InitialCapture());
    }
    private IEnumerator InitialCapture() { yield return new WaitForSecondsRealtime(1f); Capture("baseline"); }
    private void OnDestroy() { InputSystem.onBeforeUpdate -= QueueInput; }

    public void Drive(float h, float v, float yawDegrees, float pitchDegrees, float seconds, bool log = true)
    {
        horizontal = Mathf.Clamp(h, -1, 1); vertical = Mathf.Clamp(v, -1, 1);
        yaw = Mathf.Clamp(yawDegrees, -180, 180); pitch = Mathf.Clamp(pitchDegrees, -60, 60);
        commandUntil = Time.realtimeSinceStartup + Mathf.Clamp(seconds, 0, 10);
        if (log) LogEvent("input", string.Format("move {0},{1}; look {2},{3}; duration {4}", h, v, yaw, pitch, seconds));
    }
    public void Tap(string key)
    {
        if (!Enum.TryParse(key, true, out tap)) throw new ArgumentException("Unknown key");
        tapPending = true;
        LogEvent("input", "tap " + key);
    }
    public void Click(RectTransform target)
    {
        Hover(target);
        StartCoroutine(ClickAfterHover());
        LogEvent("input", "Virtual mouse click " + target.name + " at " + pointerPosition);
    }
    private IEnumerator ClickAfterHover()
    {
        // Let the UI module process pointer entry before the press, including the first click after focus.
        yield return new WaitForSecondsRealtime(.1f);
        clickPending = true;
    }
    public void Hover(RectTransform target)
    {
        Canvas canvas = target.GetComponentInParent<Canvas>();
        pointerPosition = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            target.TransformPoint(target.rect.center));
        LogEvent("input", "Virtual mouse hover " + target.name + " at " + pointerPosition);
    }
    private void QueueInput()
    {
        if (InputState.currentUpdateType != InputUpdateType.Dynamic || GameInput.TestKeyboard == null) return;
        var keys = new List<Key>();
        if (Time.realtimeSinceStartup < commandUntil) {
            if (horizontal > 0.1f) keys.Add(Key.D); if (horizontal < -0.1f) keys.Add(Key.A);
            if (vertical > 0.1f) keys.Add(Key.W); if (vertical < -0.1f) keys.Add(Key.S);
        }
        if (tapPending) { keys.Add(tap); tapPending = false; }
        InputSystem.QueueStateEvent(GameInput.TestKeyboard, new KeyboardState(keys.ToArray()));
        float denominator = Mathf.Max(0.001f, player != null ? player.LookScale : 1f);
        InputSystem.QueueStateEvent(GameInput.TestMouse, new MouseState {
            position = pointerPosition, buttons = clickPending ? (ushort)1 : (ushort)0,
            delta = new Vector2(yaw / denominator, -pitch / denominator) });
        clickPending = false;
        yaw = pitch = 0;
    }
    private void LateUpdate()
    {
        frames++; frameSeconds += Time.unscaledDeltaTime; worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
        if (player != null)
        {
            if (player.money > lastMoney) { StartCoroutine(CaptureReward()); LogEvent("reward", "Money changed through gameplay: " + lastMoney + " -> " + player.money); }
            lastMoney = player.money;
        }
    }
    private IEnumerator CaptureReward()
    {
        yield return new WaitForSecondsRealtime(0.08f);
        foreach (var text in FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
        {
            if (text.name != "RevenuePopup" || !text.gameObject.activeInHierarchy) continue;
            var gradient = text.colorGradient;
            var particles = text.GetComponentInParent<NPCUI>().GetComponentInChildren<ParticleSystem>();
            LogEvent("reward-visual", "Popup=" + text.text + "; gradient=" + text.enableVertexGradient +
                "; top=" + ColorUtility.ToHtmlStringRGBA(gradient.topLeft) + "; bottom=" + ColorUtility.ToHtmlStringRGBA(gradient.bottomLeft) +
                "; scale=" + text.transform.localScale + "; particles=" + (particles != null ? particles.particleCount : 0));
            foreach (var source in text.GetComponentInParent<NPCUI>().GetComponents<AudioSource>())
            {
                var samples = new float[256]; source.GetOutputData(samples, 0);
                float peak = 0; foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                LogEvent("reward-audio", "AudioSource isPlaying=" + source.isPlaying + "; volume=" + source.volume + "; outputPeak=" + peak);
            }
        }
        Capture("revenue");
    }
    public string Snapshot(bool reset = false)
    {
        var state = new State {
            frame = Time.frameCount, seconds = Time.realtimeSinceStartup, position = player != null ? player.transform.position : Vector3.zero,
            yaw = player != null ? player.transform.eulerAngles.y : 0, item = grab != null ? grab.itemID : -1, money = player != null ? player.money : 0,
            completed = player != null ? player.completeOrder : 0, failed = player != null ? player.fallOrder : 0, open = loop != null && loop.isOpen,
            report = loop != null && loop.IsResultOpen, npcCount = FindObjectsByType<NPCUI>(FindObjectsSortMode.None).Length,
            fps = frameSeconds > 0 ? frames / frameSeconds : 0, maxFrameMs = worstFrame * 1000,
            screenshot = lastCapture
        };
        if (reset) { frames = 0; frameSeconds = 0; worstFrame = 0; }
        return JsonUtility.ToJson(state);
    }
    [Serializable] private class State {
        public int frame, item, npcCount; public float seconds, yaw, money, completed, failed, fps, maxFrameMs;
        public Vector3 position; public bool open, report; public string screenshot;
    }
    public void Capture(string label) { StartCoroutine(CaptureFrame(label)); }
    private IEnumerator CaptureFrame(string label)
    {
        string path = RequestCapture(label);
        if (path == null) yield break;
        // Yield naturally to the end of a frame; never invoke a nested PlayerLoop.
        yield return new WaitForEndOfFrame();
        if (!Application.isPlaying) yield break;
        Texture2D source = null, result = null;
        RenderTexture resized = null;
        RenderTexture previous = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        try {
            source = ScreenCapture.CaptureScreenshotAsTexture();
            float scale = Mathf.Min(1f, 1280f / source.width, 720f / source.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            if (width == source.width && height == source.height)
                File.WriteAllBytes(path, source.EncodeToPNG());
            else
            {
                // ScreenCapture already contains display-space colors; avoid a second sRGB conversion while resizing.
                resized = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                GL.sRGBWrite = false;
                Graphics.Blit(source, resized); RenderTexture.active = resized;
                result = new Texture2D(width, height, TextureFormat.RGB24, false);
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0); result.Apply();
                File.WriteAllBytes(path, result.EncodeToPNG());
            }
            lastCapture = path; LogEvent("screenshot", path);
        }
        finally {
            RenderTexture.active = previous;
            GL.sRGBWrite = previousSrgbWrite;
            if (resized != null) RenderTexture.ReleaseTemporary(resized);
            if (source != null) Destroy(source); if (result != null) Destroy(result);
        }
    }
}
#endif
