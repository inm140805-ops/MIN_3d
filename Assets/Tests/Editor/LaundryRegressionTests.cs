using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

// Uses Assembly-CSharp through reflection so production scripts need no assembly restructuring.
public class LaundryRegressionTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private CursorLockMode cursorMode;
    private bool cursorVisible;
    private float timeScale;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [SetUp] public void SetUp() { cursorMode = Cursor.lockState; cursorVisible = Cursor.visible; timeScale = Time.timeScale; }
    [TearDown] public void TearDown()
    {
        foreach (GameObject go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        objects.Clear(); Cursor.lockState = cursorMode; Cursor.visible = cursorVisible;
        if (Time.timeScale != timeScale) Time.timeScale = timeScale;
    }
    private GameObject Inactive(string name)
    {
        var go = new GameObject(name); go.SetActive(false); objects.Add(go); return go;
    }
    private Component Add(GameObject go, string name) { return go.AddComponent(Type.GetType(name + ", Assembly-CSharp", true)); }
    private static void Set(object obj, string field, object value) { obj.GetType().GetField(field, Fields).SetValue(obj, value); }
    private static T Get<T>(object obj, string field) { return (T)obj.GetType().GetField(field, Fields).GetValue(obj); }
    private static object Call(object obj, string method, params object[] args) { return obj.GetType().GetMethod(method, Fields).Invoke(obj, args); }
    private Component MakeLoop(out Component player)
    {
        var playerObject = Inactive("QA fixture player"); player = Add(playerObject, "Player"); Add(playerObject, "Grab");
        var loop = Add(Inactive("QA fixture loop"), "CoreLoopManager"); Set(loop, "player", player);
        Set(loop, "needMoney", 30000f); Set(loop, "dayCount", 1f); Set(loop, "dayDuration", 180f);
        foreach (string name in new[] { "reportFrame", "openButton", "closeButton", "wearingText", "winButtons", "fallButtons" }) Set(loop, name, Inactive(name));
        foreach (string name in new[] { "timer", "completeOrders", "fallOrders", "TotalRevenue", "result" }) {
            var go = Inactive(name); var text = go.AddComponent<TextMeshProUGUI>(); Set(loop, name, text);
        }
        return loop;
    }

    private Component MakeOffer(string kind, out Component loop)
    {
        Component player; loop = MakeLoop(out player);
        var offers = Add(loop.gameObject, "LaundryEvents");
        Set(offers, "loop", loop); Set(offers, "wasOpen", true); Set(loop, "isOpen", true);
        Set(offers, "player", player); Set(offers, "activeDuration", 36f);
        Set(offers, "phaseRemaining", 36f); Set(loop, "totalTime", 120f);
        Set(offers, "offer", Enum.Parse(offers.GetType().GetNestedType("Offer"), kind));
        Time.timeScale = 1;
        return offers;
    }

    [Test] public void ExpressBonus_RequiresFirstTryAndTwentyFiveSecondDeadline()
    {
        Component loop; var offers = MakeOffer("Express", out loop);
        Assert.That(Call(offers, "CompleteOrder", 0, 25f, true), Is.EqualTo(1500));
        Assert.That(Call(offers, "CompleteOrder", 0, 25.01f, true), Is.EqualTo(0));
        Assert.That(Call(offers, "CompleteOrder", 0, 2f, false), Is.EqualTo(0));
        Assert.That(Call(offers, "CompleteOrder", 0, -1f, true), Is.EqualTo(0));
    }

    [Test] public void Offers_DoNotPayDuringWarningPauseClosureOrExpiration()
    {
        Component loop; var offers = MakeOffer("Express", out loop);
        Set(offers, "warning", true); Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(0));
        Set(offers, "warning", false); Time.timeScale = 0;
        Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(0));
        Time.timeScale = 1; Set(loop, "isOpen", false);
        Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(0));
        Set(loop, "isOpen", true); Set(offers, "phaseRemaining", 0f);
        Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(0));
        Set(offers, "phaseRemaining", 10f); Get<GameObject>(loop, "reportFrame").SetActive(true);
        Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(0));
    }

    [Test] public void PopularBonus_MatchesClothingIdentityAndRespectsDailyCap()
    {
        Component loop; var offers = MakeOffer("Popular", out loop); Set(offers, "popularId", 7);
        Assert.That(Call(offers, "CompleteOrder", 6, 1f, true), Is.EqualTo(0));
        Assert.That(Call(offers, "CompleteOrder", 7, 90f, false), Is.EqualTo(1500));
        Set(offers, "bonusEarned", 9500);
        Call(offers, "RefreshStatus");
        Assert.That(Get<string>(offers, "status"), Does.EndWith("+500"), "Advertise the actual reward remaining under the daily cap");
        Assert.That(Call(offers, "CompleteOrder", 7, 1f, true), Is.EqualTo(500));
        Assert.That(Call(offers, "CompleteOrder", 7, 1f, true), Is.EqualTo(0));
        Assert.That(Get<int>(offers, "bonusEarned"), Is.EqualTo(10000));
    }

    [Test] public void CarefulBonus_ResetsOnMistakeAndPaysEveryThreeCleanOrders()
    {
        Component loop; var offers = MakeOffer("Careful", out loop);
        Call(offers, "CompleteOrder", 0, 1f, true); Call(offers, "CompleteOrder", 0, 1f, true);
        Call(offers, "MissOrder");
        Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(0));
        Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(0));
        Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(2500));
        Assert.That(Call(offers, "CompleteOrder", 0, 1f, true), Is.EqualTo(0));
        Call(offers, "CompleteOrder", 0, 1f, false);
        Assert.That(Get<int>(offers, "cleanOrders"), Is.Zero);
    }

    [Test] public void VarietyBonus_CountsDistinctDeliveredClothesAndClearsAtTheEndOfTheOffer()
    {
        Component loop; var offers = MakeOffer("Variety", out loop);
        Assert.That(Call(offers, "CompleteOrder", 0, 40f, true), Is.EqualTo(0));
        Assert.That(Call(offers, "CompleteOrder", 0, 40f, true), Is.EqualTo(0));
        Assert.That(Get<string>(offers, "status"), Does.Contain("1/3종"));
        Assert.That(Call(offers, "CompleteOrder", 4, 40f, false), Is.EqualTo(0));
        Assert.That(Call(offers, "CompleteOrder", 6, 40f, true), Is.EqualTo(3000));
        Assert.That(Get<string>(offers, "status"), Does.Contain("0/3종"));
        Call(offers, "CompleteOrder", 1, 10f, true);
        Call(offers, "ClearOffer");
        Assert.That(Get<HashSet<int>>(offers, "deliveredStyles"), Is.Empty);
    }

    [Test] public void OfferLifecycle_WarnsThenExpiresAndResetsNextOpening()
    {
        Component loop; var offers = MakeOffer("Express", out loop);
        Set(offers, "warning", true); Set(offers, "phaseRemaining", -1f); Call(offers, "Update");
        Assert.That(Get<bool>(offers, "warning"), Is.False);
        Assert.That(Get<float>(offers, "phaseRemaining"), Is.EqualTo(36f));
        Set(offers, "phaseRemaining", -1f); Call(offers, "Update");
        Assert.That(Get<object>(offers, "offer").ToString(), Is.EqualTo("None"));
        Set(offers, "bonusEarned", 5000); Set(loop, "isOpen", false); Call(offers, "Update");
        Assert.That(Get<string>(offers, "status"), Is.Empty);
        Set(loop, "isOpen", true); Call(offers, "Update");
        Assert.That(Get<int>(offers, "bonusEarned"), Is.Zero);
        Assert.That(Get<float>(offers, "phaseRemaining"), Is.GreaterThan(0f));
    }

    [Test] public void ClosingOffer_OnlyStartsOnceBelowTargetWithoutInterruptingAnotherOffer()
    {
        Component loop; var offers = MakeOffer("Express", out loop); Set(loop, "totalTime", 40f);
        Assert.That(Call(offers, "TryStartClosingOffer"), Is.False);
        Call(offers, "ClearOffer");
        var player = Get<Component>(loop, "player"); Set(player, "money", 30000f);
        Assert.That(Call(offers, "TryStartClosingOffer"), Is.False);
        Set(player, "money", 0f); Set(loop, "totalTime", 24f);
        Assert.That(Call(offers, "TryStartClosingOffer"), Is.False, "Leave enough time to respond");
        Set(loop, "totalTime", 40f);
        Assert.That(Call(offers, "TryStartClosingOffer"), Is.True);
        Assert.That(Get<bool>(offers, "warning"), Is.True);
        Assert.That(Call(offers, "CompleteOrder", 0, 80f, true), Is.EqualTo(0));
        Set(offers, "warning", false);
        Assert.That(Call(offers, "CompleteOrder", 0, 80f, false), Is.EqualTo(2000), "Corrected orders still qualify for the comeback offer");
        Call(offers, "ClearOffer");
        Assert.That(Call(offers, "TryStartClosingOffer"), Is.False);
    }

    [Test] public void FailedDay_RetriesSameTargetAndDay()
    {
        Component player; var loop = MakeLoop(out player); Set(player, "money", 0f);
        Call(loop, "gameEndFuntion", false); Call(loop, "AgainButton");
        Assert.That(Get<float>(loop, "needMoney"), Is.EqualTo(30000f));
        Assert.That(Get<float>(loop, "dayCount"), Is.EqualTo(1f));
        Assert.That(Get<float>(loop, "totalTime"), Is.EqualTo(180f));
        Assert.That(Get<bool>(loop, "isOpen"), Is.False);
    }

    [Test] public void SuccessfulDay_AddsOneOrderAndResetsInventory()
    {
        Component player; var loop = MakeLoop(out player); Set(player, "money", 45000f); Set(player, "grabClothes", true);
        var grab = player.GetComponent(Type.GetType("Grab, Assembly-CSharp", true)); Set(grab, "itemID", 4);
        var offers = Add(loop.gameObject, "LaundryEvents"); Set(offers, "bonusEarned", 3000);
        Call(loop, "gameEndFuntion", true);
        Assert.That(Get<TMP_Text>(loop, "TotalRevenue").text, Does.Contain("3,000"));
        Assert.That(Get<float>(player, "money"), Is.EqualTo(45000f), "Showing a bonus breakdown must not pay it twice");
        Call(loop, "AgainButton");
        Assert.That(Get<float>(loop, "needMoney"), Is.EqualTo(35000f), "Extra revenue must not create an unpredictable difficulty jump");
        Assert.That(Get<float>(loop, "dayCount"), Is.EqualTo(2f));
        Assert.That(Get<float>(loop, "totalTime"), Is.EqualTo(180f));
        Assert.That(Get<float>(player, "money"), Is.Zero);
        Assert.That(Get<int>(grab, "itemID"), Is.EqualTo(-1));
    }

    [Test] public void MultiDayProgression_CapsTheTargetAndDoesNotRaiseItOnFailedRetries()
    {
        Component player; var loop = MakeLoop(out player);
        float[] expectedTargets = { 35000, 40000, 45000, 50000, 55000, 60000, 65000, 70000, 75000, 75000, 75000, 75000 };
        for (int day = 0; day < expectedTargets.Length; day++)
        {
            Set(player, "money", Get<float>(loop, "needMoney") + 10000f);
            Call(loop, "gameEndFuntion", false); Call(loop, "AgainButton");
            Assert.That(Get<float>(loop, "needMoney"), Is.EqualTo(expectedTargets[day]));
            Assert.That(Get<float>(loop, "dayCount"), Is.EqualTo(day + 2));
            Assert.That(Get<float>(player, "money"), Is.Zero);
            // An unsuccessful retry between successful days must not skip a day or increase the target.
            Call(loop, "gameEndFuntion", false); Call(loop, "AgainButton");
            Assert.That(Get<float>(loop, "needMoney"), Is.EqualTo(expectedTargets[day]));
            Assert.That(Get<float>(loop, "dayCount"), Is.EqualTo(day + 2));
        }
    }

    [Test] public void TimeoutWithTargetMet_IsSuccess_AndResultCannotBeOverwritten()
    {
        Component player; var loop = MakeLoop(out player); Set(player, "money", 30000f);
        Call(loop, "gameEndFuntion", false);
        Assert.That(Get<bool>(loop, "daySucceeded"), Is.True);
        Set(player, "money", 0f); Call(loop, "gameEndFuntion", false);
        Assert.That(Get<bool>(loop, "daySucceeded"), Is.True, "End-of-day presentation must be idempotent");
        Assert.That(Get<bool>(loop, "timerEnd"), Is.False);
        Assert.That(Get<bool>(loop, "timerStart"), Is.False);
    }

    [Test] public void AgainOutsideResult_DoesNotResetActiveDay()
    {
        Component player; var loop = MakeLoop(out player); Set(player, "money", 5000f); Set(loop, "isOpen", true);
        Call(loop, "AgainButton");
        Assert.That(Get<float>(player, "money"), Is.EqualTo(5000f));
        Assert.That(Get<bool>(loop, "isOpen"), Is.True);
    }

    [Test] public void OrderDeck_CoversEveryRequestBeforeRepeatingAndAvoidsBoundaryDuplicates()
    {
        var manager = Add(Inactive("QA orders"), "GameManager");
        var randomState = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(103);
            int previous = -1;
            for (int round = 0; round < 5; round++)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < 15; i++)
                {
                    int next = (int)Call(manager, "NextOrderIndex", 15);
                    Assert.That(next, Is.InRange(0, 14));
                    Assert.That(next, Is.Not.EqualTo(previous));
                    Assert.That(seen.Add(next), Is.True);
                    previous = next;
                }
            }
            Assert.That(Call(manager, "NextOrderIndex", 1), Is.EqualTo(0));
            Assert.That((int)Call(manager, "NextOrderIndex", 3), Is.InRange(0, 2));
        }
        finally { UnityEngine.Random.state = randomState; }
    }

    [Test] public void QueueCapacity_RecoversWhenAnySlotIsReleased()
    {
        var manager = Add(Inactive("QA queue"), "GameManager"); var slots = new[] { true, true, true };
        Set(manager, "line", slots); Call(manager, "CheakFull"); Assert.That(Get<bool>(manager, "fullLine"), Is.True);
        slots[1] = false; Call(manager, "CheakFull"); Assert.That(Get<bool>(manager, "fullLine"), Is.False);
        Set(manager, "line", new bool[0]); Call(manager, "CheakFull"); Assert.That(Get<bool>(manager, "fullLine"), Is.False);
    }

    [Test] public void PatienceTimeout_CountsFailureOnlyOnce_AndCompletedOrderNeverFails()
    {
        var go = Inactive("QA customer"); var ui = Add(go, "NPCUI"); var movement = Add(go, "NPCMovement");
        var controller = Add(go, "NPCController"); var exit = Add(go, "NPCExit"); var player = Add(Inactive("QA player"), "Player");
        Set(ui, "movement", movement); Set(ui, "controller", controller); Set(ui, "npcExit", exit); Set(ui, "player", player);
        Set(movement, "inLine", true); Set(controller, "enterLine", true); Set(ui, "moodTimer", 150f);
        Set(ui, "moodBar", Inactive("QA bar").AddComponent<UnityEngine.UI.Image>());
        Set(ui, "moodImage", Inactive("QA face").AddComponent<UnityEngine.UI.Image>()); Set(ui, "moodSprite", new Sprite[3]);
        for (int i = 0; i < 5; i++) Call(ui, "Patience");
        Assert.That(Get<float>(player, "fallOrder"), Is.EqualTo(1f));
        Set(ui, "goOut", false); Set(ui, "orderComplete", true); Call(ui, "Patience");
        Assert.That(Get<float>(player, "fallOrder"), Is.EqualTo(1f));
    }

    [Test] public void ResultScreen_BlocksPlayerControl()
    {
        Component player; var loop = MakeLoop(out player); Set(player, "coreLoopManager", loop);
        Call(loop, "gameEndFuntion", false);
        Assert.That((bool)player.GetType().GetProperty("CanControl").GetValue(player), Is.False);
    }

    [Test] public void FailedDayMenu_OffersRetryWithTheExistingDayResetCallback()
    {
        Component player; var loop = MakeLoop(out player);
        GameObject report = Get<GameObject>(loop, "reportFrame"); report.AddComponent<UnityEngine.UI.Image>();
        foreach (string field in new[] { "result", "completeOrders", "fallOrders", "TotalRevenue" }) {
            TMP_Text text = Get<TMP_Text>(loop, field); text.name = field == "fallOrders" ? "FallOrders" : field;
            text.transform.SetParent(report.transform, false);
        }
        GameObject win = Get<GameObject>(loop, "winButtons"), fail = Get<GameObject>(loop, "fallButtons");
        win.name = "WinButton"; fail.name = "FallButton";
        foreach (GameObject panel in new[] { win, fail }) { panel.AddComponent<UnityEngine.UI.Image>(); panel.transform.SetParent(report.transform, false); }
        var next = MakeButton(win.transform, "KeepGoing"); MakeButton(win.transform, "Back"); MakeButton(fail.transform, "Back");
        var callback = (UnityEngine.Events.UnityAction)Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), loop, "AgainButton");
        UnityEditor.Events.UnityEventTools.AddPersistentListener(next.onClick, callback);
        var style = Add(Inactive("QA menu style"), "ShopMenuStyle");
        Set(style, "reportPanel", report.GetComponent<RectTransform>());
        Set(style, "font", UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Paperlogy-7Bold SDF 1.asset"));
        Call(style, "StyleReport");
        var retry = fail.transform.Find("Retry day").GetComponent<UnityEngine.UI.Button>();
        Assert.That(retry.onClick.GetPersistentEventCount(), Is.EqualTo(1));
        Assert.That(retry.onClick.GetPersistentMethodName(0), Is.EqualTo("AgainButton"));
        Assert.That(retry.onClick.GetPersistentTarget(0), Is.SameAs(loop));
        Assert.That(fail.transform.Find("Back"), Is.Not.Null, "The original exit must remain available");
    }

    private UnityEngine.UI.Button MakeButton(Transform parent, string name)
    {
        var go = Inactive(name); go.AddComponent<UnityEngine.UI.Image>(); go.transform.SetParent(parent, false);
        var button = go.AddComponent<UnityEngine.UI.Button>();
        var label = Inactive("Text"); label.AddComponent<TextMeshProUGUI>(); label.transform.SetParent(go.transform, false);
        return button;
    }

    [Test] public void RewardPopup_DoesNotInheritTinyAutoSizeOrSingleColorFromChat()
    {
        GameObject customer = Inactive("QA reward customer"); Component ui = Add(customer, "NPCUI");
        GameObject canvasObject = Inactive("QA reward canvas"); canvasObject.AddComponent<Canvas>();
        canvasObject.transform.SetParent(customer.transform, false);
        var source = Inactive("Chat source").AddComponent<TextMeshProUGUI>();
        source.transform.SetParent(canvasObject.transform, false);
        source.font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Paperlogy-7Bold SDF 1.asset");
        source.enableAutoSizing = true; source.fontSizeMin = source.fontSizeMax = 0.2f;
        source.margin = Vector4.one * 4f;
        Set(ui, "chatTMP", source); Call(ui, "CreateRevenuePopup");
        TMP_Text popup = Get<TMP_Text>(ui, "revenuePopup");
        Assert.That(popup.enableAutoSizing, Is.False);
        Assert.That(popup.fontSize, Is.EqualTo(1f));
        Assert.That(popup.margin, Is.EqualTo(Vector4.zero));
        Assert.That(popup.enableVertexGradient, Is.True);
        Assert.That(popup.colorGradient.topLeft, Is.Not.EqualTo(popup.colorGradient.bottomLeft));
        Assert.That(popup.fontMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth), Is.EqualTo(0.16f).Within(0.001f));
        Assert.That(popup.GetComponent(Type.GetType("BillboardUi, Assembly-CSharp", true)), Is.Not.Null);
    }

    [Test] public void GuideRestyling_DoesNotDuplicateClothingNamesAndRepairsOldDuplicates()
    {
        var type = Type.GetType("ShopMenuStyle, Assembly-CSharp", true);
        var format = type.GetMethod("FormatGuideRow", BindingFlags.Static | BindingFlags.NonPublic);
        string text = "<b>티셔츠</b>  ·  <b>티셔츠</b>  ·  긴급, 맑음, 더위";
        for (int i = 0; i < 20; i++) text = (string)format.Invoke(null, new object[] { "티셔츠", text });
        Assert.That(text, Is.EqualTo("<b>티셔츠</b>  ·  긴급, 맑음, 더위"));
    }

    [Test] public void IntroInitialization_DoesNotRequireAnActiveGameplayPlayer()
    {
        GameObject go = Inactive("QA intro"); Component intro = Add(go, "IntroManager");
        Set(intro, "startButton", MakeButton(go.transform, "StartButton"));
        Set(intro, "settingButton", MakeButton(go.transform, "SettingButton"));
        Set(intro, "exitButton", MakeButton(go.transform, "ExitButton"));
        Assert.DoesNotThrow(() => Call(intro, "Awake"));
        Assert.That(Get<Animator[]>(intro, "buttonAnimators").Length, Is.EqualTo(3));
    }
}
