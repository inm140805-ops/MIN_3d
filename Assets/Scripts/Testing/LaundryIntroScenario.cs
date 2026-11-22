#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Actual UI pointer events only; never invokes a menu callback directly.
public class LaundryIntroScenario : MonoBehaviour
{
    private static T Read<T>(IntroManager intro, string name)
    {
        return (T)typeof(IntroManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(intro);
    }
    private bool Check(bool condition, string message)
    {
        LaundryPlaytestDriver.LogEvent("assertion", (condition ? "PASS: " : "FAIL: ") + message);
        if (!condition) LaundryPlaytestDriver.FinishSession(message, false);
        return condition;
    }
    private IEnumerator Start()
    {
        var driver = GetComponent<LaundryPlaytestDriver>();
        yield return new WaitForSecondsRealtime(2f);
        var intro = FindAnyObjectByType<IntroManager>();
        if (!Check(intro != null && Cursor.visible && Cursor.lockState == CursorLockMode.None, "Intro exposes an unlocked mouse pointer")) yield break;
        var settings = Read<GameObject>(intro, "setting");
        var main = Read<GameObject>(intro, "mainIntro");
        driver.Click((RectTransform)Read<Button>(intro, "settingButton").transform);
        yield return new WaitForSecondsRealtime(0.5f);
        if (!Check(settings.activeSelf && !main.activeSelf, "Mouse opens intro settings")) yield break;
        driver.Capture("intro-settings");
        driver.Click((RectTransform)settings.GetComponentInChildren<Button>().transform);
        yield return new WaitForSecondsRealtime(0.5f);
        if (!Check(main.activeSelf && !settings.activeSelf && Cursor.visible, "Mouse returns to intro menu")) yield break;
        driver.Click((RectTransform)Read<Button>(intro, "startButton").transform);
        float until = Time.realtimeSinceStartup + 15f;
        while (SceneManager.GetActiveScene().name != "SampleScene" && Time.realtimeSinceStartup < until) yield return null;
        yield return new WaitForSecondsRealtime(1f);
        if (!Check(SceneManager.GetActiveScene().name == "SampleScene" && FindAnyObjectByType<CoreLoopManager>() != null,
            "Mouse Start loads the gameplay scene")) yield break;
        driver.Capture("intro-to-game");
        yield return new WaitForSecondsRealtime(0.5f);
        LaundryPlaytestDriver.FinishSession("intro-controls: menu/settings/start mouse flow passed", true);
    }
}
#endif
