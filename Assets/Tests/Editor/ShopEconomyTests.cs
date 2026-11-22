using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ShopEconomyTests
{
    private GameObject fixture;
    private Component shop;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private object Call(string name, params object[] args) => shop.GetType().GetMethod(name, Flags).Invoke(shop, args);
    private int Value(string name) => (int)shop.GetType().GetProperty(name).GetValue(shop);
    private int Stock(int index = 0) => (int)Call("Stock", index);
    private void Funds(int amount) => shop.GetType().GetField("funds", Flags).SetValue(shop, amount);
    [SetUp] public void SetUp()
    {
        fixture = new GameObject("Temporary stock test"); fixture.SetActive(false);
        shop = fixture.AddComponent(Type.GetType("ShopEconomy, Assembly-CSharp", true));
        Call("Initialize", 7, false);
    }
    [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(fixture); }

    [Test] public void Reservation_PreventsDoublePickupAndDuplicateReturn()
    {
        Assert.That(Call("TryTake", 0), Is.True); Assert.That(Stock(), Is.EqualTo(5));
        Assert.That(Call("TryTake", 1), Is.False);
        Assert.That(Call("ReturnItem", 1), Is.False);
        Assert.That(Call("ReturnItem", 0), Is.True); Assert.That(Stock(), Is.EqualTo(6));
        Assert.That(Call("ReturnItem", 0), Is.False); Assert.That(Value("Funds"), Is.EqualTo(6000));
    }
    [Test] public void Sale_ConsumesOneItemAndCreditsOnlyOnce()
    {
        Call("TryTake", 2); Assert.That(Call("CompleteSale", 2), Is.True);
        Assert.That(Value("Funds"), Is.EqualTo(7000)); Assert.That(Stock(2), Is.EqualTo(5));
        Assert.That(Call("CompleteSale", 2), Is.False); Assert.That(Call("ReturnItem", 2), Is.False);
        Assert.That(Value("Funds"), Is.EqualTo(7000));
    }
    [Test] public void Restock_ReservesSpaceForCarriedGarmentAndChargesExactCost()
    {
        Call("TryUpgrade", 0); Call("TryTake", 0);
        Assert.That(Call("RestockAmount", 0), Is.EqualTo(3));
        Assert.That(Call("TryRestock", 0), Is.True);
        Assert.That(Stock(), Is.EqualTo(8)); Assert.That(Value("Funds"), Is.EqualTo(1100));
        Assert.That(Call("TryRestock", 0), Is.False);
        Call("ReturnItem", 0); Assert.That(Stock(), Is.EqualTo(9));
    }
    [TestCase(0, 4000, 7500, "Capacity", 12)]
    [TestCase(1, 5000, 9000, "UnitCost", 200)]
    [TestCase(2, 6000, 11000, "OrderIncome", 1500)]
    public void Upgrades_ChargeBothLevelsAndStopAtMaximum(int type, int first, int second, string property, int maximum)
    {
        Funds(first - 1); Assert.That(Call("TryUpgrade", type), Is.False);
        Funds(first + second); Assert.That(Call("TryUpgrade", type), Is.True);
        Assert.That(Value("Funds"), Is.EqualTo(second));
        Assert.That(Call("TryUpgrade", type), Is.True); Assert.That(Value("Funds"), Is.Zero);
        Assert.That(Value(property), Is.EqualTo(maximum)); Assert.That(Call("TryUpgrade", type), Is.False);
    }
    [Test] public void SaveRoundTrip_RestoresHeldGarmentWithoutCreatingAnotherSale()
    {
        Call("TryUpgrade", 0); Call("TryTake", 3);
        var json = (string)Call("CaptureSave"); Call("Initialize", 7, false); Call("Restore", json);
        Assert.That(Stock(3), Is.EqualTo(6)); Assert.That(Value("Funds"), Is.EqualTo(2000));
        Assert.That(Value("Capacity"), Is.EqualTo(9)); Assert.That(Call("CompleteSale", 3), Is.False);
    }
    [Test] public void EmergencySupply_OnlyRescuesEmptyUnaffordableStock()
    {
        for (int i = 0; i < 6; i++) { Call("TryTake", 0); Call("CompleteSale", 0); }
        Funds(299); Assert.That(Call("EmergencyAvailable", 0), Is.True);
        Assert.That(Call("TryRestock", 0), Is.True); Assert.That(Stock(), Is.EqualTo(1));
        Assert.That(Value("Funds"), Is.EqualTo(299)); Assert.That(Call("EmergencyAvailable", 0), Is.False);
        Assert.That(Call("TryRestock", 0), Is.False);
    }
    [Test] public void InvalidIndicesAndMalformedSave_CannotSpendOrCorruptStock()
    {
        foreach (int i in new[] { -1, 7, int.MaxValue })
        {
            Assert.That(Call("TryTake", i), Is.False); Assert.That(Call("TryRestock", i), Is.False);
            Assert.That(Call("TryUpgrade", i), Is.False); Assert.That(Call("ReturnItem", i), Is.False);
        }
        Call("Restore", "{}"); Call("Restore", "not json");
        Assert.That(Value("Funds"), Is.EqualTo(6000)); Assert.That(Stock(), Is.EqualTo(6));
    }
}
