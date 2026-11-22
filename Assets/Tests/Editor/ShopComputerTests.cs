using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEditor;

public class ShopComputerTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private GameObject fixture, panel;
    private Component terminal, economy, loop, gui;
    private float previousScale;
    private CursorLockMode cursorMode;
    private bool cursorVisible;
    private ScriptableObject[] clothing;
    private Component workflow;
    private static Type Kind(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    private GameObject Child(string name) { var g = new GameObject(name, typeof(RectTransform)); g.transform.SetParent(fixture.transform); return g; }
    private TMP_Text Label(string name) => Child(name).AddComponent<TextMeshProUGUI>();
    private UnityEngine.UI.Button Button(string name) { var g = Child(name); g.AddComponent<UnityEngine.UI.Image>(); return g.AddComponent<UnityEngine.UI.Button>(); }
    [SetUp] public void SetUp()
    {
        previousScale = Time.timeScale; cursorMode = Cursor.lockState; cursorVisible = Cursor.visible;
        fixture = new GameObject("Computer behavior fixture"); fixture.SetActive(false);
        terminal = fixture.AddComponent(Kind("ShopComputer")); economy = fixture.AddComponent(Kind("ShopEconomy"));
        loop = fixture.AddComponent(Kind("CoreLoopManager")); gui = fixture.AddComponent(Kind("GUIManager"));
        Call(economy, "Initialize", 7, false);
        panel = Child("Panel"); panel.SetActive(false);
        var report = Child("Report"); report.SetActive(false); Set(loop, "reportFrame", report);
        Set(gui, "OrderMenu", Child("Order guide").AddComponent<UnityEngine.UI.Image>());
        Set(terminal, "economy", economy); Set(terminal, "loop", loop); Set(terminal, "gui", gui);
        var boxes = fixture.AddComponent(Kind("BoxManager"));
        var boxData = Array.CreateInstance(Kind("BoxData"), 7); clothing = new ScriptableObject[7];
        for (int i = 0; i < 7; i++) { clothing[i] = ScriptableObject.CreateInstance(Kind("BoxData")); Set(clothing[i], "boxName", "Test clothing " + i); boxData.SetValue(clothing[i], i); }
        Set(boxes, "boxDatas", boxData); Set(terminal, "boxes", boxes);
        Set(terminal, "panel", panel); Set(terminal, "prompt", Child("Prompt")); Set(terminal, "window", panel.GetComponent<RectTransform>());
        foreach (var name in new[] { "fundsText", "infoText", "noticeText", "restockText" }) Set(terminal, name, Label(name));
        Set(terminal, "stockCounts", Enumerable.Range(0, 7).Select(i => Label("Count")).ToArray());
        Set(terminal, "stockButtons", Enumerable.Range(0, 7).Select(i => Button("Stock")).ToArray());
        Set(terminal, "upgradeDescriptions", Enumerable.Range(0, 3).Select(i => Label("Description")).ToArray());
        Set(terminal, "upgradePrices", Enumerable.Range(0, 3).Select(i => Label("Price")).ToArray());
        Set(terminal, "upgradeButtons", Enumerable.Range(0, 3).Select(i => Button("Upgrade")).ToArray());
        Set(terminal, "restockButton", Button("Restock"));
    }
    [TearDown] public void TearDown()
    {
        if (workflow != null) Kind("LaundryWorkflow").GetProperty("Current").GetSetMethod(true).Invoke(null, new object[] { null });
        Call(terminal, "Close"); UnityEngine.Object.DestroyImmediate(fixture);
        foreach (var data in clothing) UnityEngine.Object.DestroyImmediate(data);
        Time.timeScale = previousScale; Cursor.lockState = cursorMode; Cursor.visible = cursorVisible;
    }
    [Test] public void OpenAndClose_RespectPauseOwnershipAndCloseTheOrderGuide()
    {
        Time.timeScale = .5f; Call(terminal, "Open");
        Assert.That(panel.activeSelf, Is.True); Assert.That(Time.timeScale, Is.Zero);
        var guide = (UnityEngine.UI.Image)gui.GetType().GetField("OrderMenu", Flags).GetValue(gui);
        Assert.That(guide.gameObject.activeSelf, Is.False);
        Call(terminal, "Close"); Assert.That(panel.activeSelf, Is.False); Assert.That(Time.timeScale, Is.EqualTo(.5f));
        Assert.That(Kind("ShopComputer").GetProperty("ClosedThisFrame").GetValue(null), Is.True);
    }
    [Test] public void ControllerPurchase_UpdatesStockLabelsAndRejectsCallsAfterClosing()
    {
        Time.timeScale = 1; Call(terminal, "Open"); Call(terminal, "UpgradeCapacity"); Call(terminal, "RestockSelected");
        Assert.That(Call(economy, "Stock", 0), Is.EqualTo(9));
        Assert.That(economy.GetType().GetProperty("Funds").GetValue(economy), Is.EqualTo(1100));
        var counts = (TMP_Text[])terminal.GetType().GetField("stockCounts", Flags).GetValue(terminal);
        Assert.That(counts[0].text, Is.EqualTo("9 / 9"));
        var restock = (UnityEngine.UI.Button)terminal.GetType().GetField("restockButton", Flags).GetValue(terminal);
        Assert.That(restock.interactable, Is.False);
        Call(terminal, "Close"); Call(terminal, "UpgradeSupply"); Call(terminal, "SelectStock", 1); Call(terminal, "RestockSelected");
        Assert.That(economy.GetType().GetProperty("Funds").GetValue(economy), Is.EqualTo(1100));
    }
    [Test] public void ExistingPauseOrDayResult_PreventsOpeningAnotherModal()
    {
        Time.timeScale = 0; Call(terminal, "Open"); Assert.That(panel.activeSelf, Is.False);
        Time.timeScale = 1; Set(gui, "esc", true); Call(terminal, "Open"); Assert.That(panel.activeSelf, Is.False);
        Set(gui, "esc", false); ((GameObject)loop.GetType().GetField("reportFrame", Flags).GetValue(loop)).SetActive(true);
        Call(terminal, "Open"); Assert.That(panel.activeSelf, Is.False);
    }
    [Test] public void InsufficientFunds_DisablesPurchaseButtonsAndKeepsBalances()
    {
        Time.timeScale = 1; Call(terminal, "Open"); Call(terminal, "UpgradeService");
        var buttons = (UnityEngine.UI.Button[])terminal.GetType().GetField("upgradeButtons", Flags).GetValue(terminal);
        Assert.That(buttons.All(b => !b.interactable), Is.True);
        Call(terminal, "UpgradeCapacity"); Call(terminal, "UpgradeSupply");
        Assert.That(economy.GetType().GetProperty("Funds").GetValue(economy), Is.EqualTo(0));
        Assert.That(economy.GetType().GetProperty("Capacity").GetValue(economy), Is.EqualTo(6));
    }
    private void EnableLaundry()
    {
        workflow=fixture.AddComponent(Kind("LaundryWorkflow")); Set(workflow,"economy",economy);
        Set(workflow,"boxes",terminal.GetType().GetField("boxes",Flags).GetValue(terminal));
        Kind("LaundryWorkflow").GetProperty("Current").GetSetMethod(true).Invoke(null,new object[]{workflow});
        Set(terminal,"quantityText",Label("Quantity"));
    }
    [Test] public void DeliveryUI_QuantityAndTabsPlaceAnOrderInsteadOfInstantStock()
    {
        EnableLaundry(); Time.timeScale=1; Call(terminal,"Open");
        var buttons=(UnityEngine.UI.Button[])terminal.GetType().GetField("stockButtons",Flags).GetValue(terminal);
        Assert.AreEqual(4,buttons.Count(b=>b.gameObject.activeSelf));
        Call(terminal,"DecreaseQuantity"); Call(terminal,"SelectStock",3); Call(terminal,"RestockSelected");
        Assert.AreEqual(5040,economy.GetType().GetProperty("Funds").GetValue(economy));
        Assert.AreEqual(3,Call(economy,"Supply",0));
        var state=economy.GetType().GetProperty("Operations").GetValue(economy);var delivery=state.GetType().GetField("delivery").GetValue(state);
        Assert.AreEqual(10,delivery.GetType().GetField("catalog").GetValue(delivery));Assert.AreEqual(2,delivery.GetType().GetField("quantity").GetValue(delivery));
        Call(terminal,"RestockSelected");Assert.AreEqual(5040,economy.GetType().GetProperty("Funds").GetValue(economy));
        Call(terminal,"SelectClothing");Assert.AreEqual(7,buttons.Count(b=>b.gameObject.activeSelf));
        var restock=(UnityEngine.UI.Button)terminal.GetType().GetField("restockButton",Flags).GetValue(terminal);Assert.IsFalse(restock.interactable);
    }
    [Test] public void DeliveryUI_ClampsQuantityAndDoesNotOfferFreeBundles()
    {
        EnableLaundry(); Time.timeScale=1; Call(terminal,"Open");
        for(int i=0;i<20;i++)Call(terminal,"IncreaseQuantity");Assert.AreEqual(6,terminal.GetType().GetField("quantity",Flags).GetValue(terminal));
        for(int i=0;i<20;i++)Call(terminal,"DecreaseQuantity");Assert.AreEqual(1,terminal.GetType().GetField("quantity",Flags).GetValue(terminal));
        Set(economy,"funds",0);Call(terminal,"SelectStock",3);
        var restock=(UnityEngine.UI.Button)terminal.GetType().GetField("restockButton",Flags).GetValue(terminal);Assert.IsFalse(restock.interactable);
    }
}
