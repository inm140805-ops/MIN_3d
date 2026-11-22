using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LaundryOperationsTests
{
    GameObject fixture;
    Component economy;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    object Call(string method, params object[] args) => economy.GetType().GetMethod(method, Flags).Invoke(economy, args);
    object Property(string name) => economy.GetType().GetProperty(name).GetValue(economy);
    static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    object State => Property("Operations");
    object Delivery => Field(State, "delivery");
    string Id(object ticket) => (string)Field(ticket, "id");
    object Location(string name) => Enum.Parse(Type.GetType("LaundryLocation, Assembly-CSharp", true), name);
    bool Order(int item, int count, double now = 100) => (bool)Call("OrderDelivery", item, count, now, "");
    bool Station(string name, double now, int machine = -1) => (bool)Call("UseStation", Location(name), machine, now, "");
    int Supply(int i) => (int)Call("Supply", i);
    int Funds => (int)Property("Funds");
    object Accept(int item = 0, int slot = 0, double now = 100) => Call("AcceptLaundry", item, 1, slot, now);
    void Reload() { string json = (string)Call("CaptureSave"); Call("Initialize", 7, false); Call("Restore", json); }
    [SetUp] public void Setup()
    {
        fixture = new GameObject("Isolated laundry operations test"); fixture.SetActive(false);
        economy = fixture.AddComponent(Type.GetType("ShopEconomy, Assembly-CSharp", true)); Call("Initialize", 7, false);
    }
    [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(fixture); }

    [Test] public void Order_ChargesOnceAndSetsExactlySixtySeconds()
    {
        Assert.IsTrue(Order(0, 3)); Assert.AreEqual(5400, Funds);
        Assert.AreEqual(160d, Field(Delivery, "dueAt")); Assert.AreEqual(3, Supply(0));
        Assert.IsFalse(Order(1, 2)); Assert.AreEqual(5400, Funds);
        Assert.IsFalse((bool)Call("OpenDelivery", Id(Delivery), 159.999));
        Assert.IsFalse((bool)Call("OpenDelivery", Id(Delivery), 163.999));
        Assert.IsTrue((bool)Call("OpenDelivery", Id(Delivery), 164d));
        Assert.AreEqual(3, Supply(0), "Opening cannot increase stock");
    }
    [Test] public void PhysicalStock_ConservesCountsAndRejectsDuplicateActions()
    {
        ((int[])Field(State, "supplies"))[0] = 0;
        Assert.IsTrue(Order(0, 5)); string id = Id(Delivery);
        Assert.IsTrue((bool)Call("OpenDelivery", id, 164d));
        Assert.IsFalse((bool)Call("OpenDelivery", id, 164d));
        Assert.IsTrue((bool)Call("TakeDelivery", id, 164d));
        Assert.AreEqual(3, Field(Delivery, "carrying")); Assert.AreEqual(2, Field(Delivery, "remaining"));
        Assert.IsFalse((bool)Call("TakeDelivery", id, 164d)); Assert.IsFalse((bool)Call("StoreDelivery", 1));
        Assert.IsTrue((bool)Call("StoreDelivery", 0)); Assert.AreEqual(3, Supply(0)); Assert.IsFalse((bool)Call("StoreDelivery", 0));
        Assert.IsTrue((bool)Call("TakeDelivery", id, 164d)); Assert.IsTrue((bool)Call("StoreDelivery", 0));
        Assert.AreEqual(5, Supply(0)); Assert.AreEqual(5, Field(Delivery, "stored"));
        Assert.IsFalse((bool)Call("TakeDelivery", id, 164d)); Assert.IsFalse(Order(1, 1, 194.99)); Assert.IsTrue(Order(1, 1, 195));
    }
    [TestCase(101d, false)] [TestCase(164d, true)]
    public void Reload_KeepsDueTimePaymentAndCarriedBoxes(double now, bool opened)
    {
        Order(1, 3); string id = Id(Delivery);
        if (opened) { Call("OpenDelivery", id, now); Call("TakeDelivery", id, now); }
        Reload(); Assert.AreEqual(id, Id(Delivery)); Assert.AreEqual(160d, Field(Delivery, "dueAt")); Assert.AreEqual(5640, Funds);
        Assert.AreEqual(opened ? 3 : 0, Field(Delivery, "carrying")); Assert.AreEqual(3, Supply(1));
        if (opened) { Assert.IsTrue((bool)Call("StoreDelivery", 1)); Reload(); Assert.AreEqual(6, Supply(1)); Assert.IsFalse((bool)Call("StoreDelivery", 1)); }
    }
    [Test] public void LaundryCycle_RequiresAllStationsCorrectTicketAndPaysOnlyOnce()
    {
        var ticket = Accept(); string id = Id(ticket);
        Assert.IsFalse((bool)Call("CanReturnLaundry", id)); Assert.IsFalse(Station("Dryer", 100));
        Assert.IsTrue(Station("Washer", 100, 0)); Assert.AreEqual(2, Supply(0)); Assert.IsFalse(Station("Washer", 111.99, 0));
        Assert.IsTrue(Station("Washer", 112, 0)); Assert.IsTrue(Station("Washer", 112, 1));
        Assert.AreEqual(2, Supply(0)); Assert.IsTrue(Station("Washer",112,1));
        Assert.IsTrue(Station("Dryer", 112)); Assert.AreEqual(2, Supply(1)); Assert.IsFalse(Station("Dryer", 119.99));
        Assert.IsTrue(Station("Dryer", 120)); Assert.IsTrue(Station("Table", 120)); Assert.AreEqual(2, Supply(2));
        Assert.IsFalse(Station("Table", 122.99)); Assert.IsTrue(Station("Table", 123));
        Assert.IsFalse((bool)Call("CanReturnLaundry", "wrong")); Assert.AreEqual(0, Call("ReturnLaundry", "wrong", 0));
        Assert.AreEqual(5000, Call("ReturnLaundry", id, 0)); Assert.AreEqual(7000, Funds); Assert.AreEqual(0, Call("ReturnLaundry", id, 0));
        Assert.AreEqual(5000, Field(State, "dailySales")); Assert.AreEqual(1, Field(State, "dailyCompleted")); Assert.AreEqual(6, Call("Stock", 0));
    }
    [Test] public void Reload_ResumesStationAndTicketWithoutReconsumingSupplies()
    {
        string id = Id(Accept()); Station("Washer", 100, 2); Reload();
        Assert.AreEqual(2, Supply(0)); Assert.IsFalse(Station("Washer", 111, 2)); Assert.IsTrue(Station("Washer", 112, 2));
        Reload(); Assert.AreEqual(id, Id(Property("HeldTicket"))); Assert.AreEqual("Wet", Field(Property("HeldTicket"), "stage").ToString()); Assert.AreEqual(2, Supply(0));
    }
    [Test] public void Shortage_CanPutClothesDownReceiveDeliveryAndResume()
    {
        ((int[])Field(State, "supplies"))[0] = 0; string id = Id(Accept());
        Assert.IsTrue(Station("Washer", 100, 0)); Assert.IsTrue(Station("Washer",100,0)); Assert.IsTrue(Station("Table", 100));
        Assert.IsTrue(Order(0, 3)); string delivery = Id(Delivery);
        Call("OpenDelivery", delivery, 164d); Assert.IsTrue((bool)Call("TakeDelivery", delivery, 164d)); Assert.IsTrue((bool)Call("StoreDelivery", 0));
        Assert.IsTrue(Station("Table", 164)); Assert.AreEqual(id, Id(Property("HeldTicket"))); Assert.IsTrue(Station("Washer", 164, 0)); Assert.AreEqual(2, Supply(0));
    }
    [Test] public void PackagingShortage_DoesNotTrapCargoInPlayersHands()
    {
        ((int[])Field(State, "supplies"))[2] = 0;
        Accept(); Station("Washer", 100, 0); Station("Washer", 112, 0); Station("Dryer", 112); Station("Dryer", 120);
        Assert.IsTrue(Station("Table", 120)); Assert.IsNull(Property("HeldTicket"));
        Assert.AreEqual("Dry", Field(Call("AtStation", Location("Table"), -1), "stage").ToString());
        Order(2, 3, 120); string id = Id(Delivery); Call("OpenDelivery", id, 184d); Call("TakeDelivery", id, 184d); Call("StoreDelivery", 2);
        Station("Table", 184); Station("Table", 184); Station("Table", 187);
        Assert.IsTrue((bool)Call("CanReturnLaundry", Id(Property("HeldTicket"))));
    }
    [Test] public void ThreeCustomers_HaveUniqueTicketsAndNoStationOrHandDuplication()
    {
        string a = Id(Accept(0, 0)); Assert.IsNull(Accept(1, 1)); Station("Washer", 100, 0);
        Assert.IsNull(Accept(1, 0)); string b = Id(Accept(1, 1)); Assert.AreNotEqual(a, b);
        Assert.IsFalse(Station("Washer", 100, 0)); Station("Washer", 100, 1);
        string c = Id(Accept(2, 2)); Assert.AreNotEqual(b, c); Station("Washer", 100, 2); Reload();
        Assert.AreEqual(0, Supply(0)); Assert.IsNotNull(Call("Ticket", a)); Assert.IsNotNull(Call("Ticket", b)); Assert.IsNotNull(Call("Ticket", c));
    }
    [Test] public void LegacyCarriedStock_CannotBeCombinedWithNewCargo()
    {
        Assert.IsTrue((bool)Call("TryTake", 0)); Assert.IsNull(Accept());
        Order(0, 1); string id = Id(Delivery); Call("OpenDelivery", id, 164d);
        Assert.IsFalse((bool)Call("TakeDelivery", id, 164d)); Call("ReturnItem", 0);
        Assert.IsTrue((bool)Call("TakeDelivery", id, 164d)); Assert.IsFalse((bool)Call("TryTake", 0));
    }
    [Test] public void InvalidCommands_DoNotSpendOrAdvanceTime()
    {
        Assert.IsFalse(Order(-1, 1)); Assert.IsFalse(Order(11, 1)); Assert.IsFalse(Order(0, 0)); Assert.IsFalse(Order(0, 4)); Assert.IsFalse(Order(0, 1, double.NaN));
        Assert.AreEqual(6000, Funds); Accept(); Station("Washer", 100, 0); Call("AdvanceLaundry", double.NaN);
        Assert.AreEqual("Washing", Field(Call("AtStation", Location("Washer"), 0), "stage").ToString());
    }
    [Test] public void EmergencyCredit_StillRequiresSixtySecondPhysicalDelivery()
    {
        Set(economy, "funds", 0); ((int[])Field(State, "supplies"))[0] = 0;
        Assert.IsFalse(Order(0, 2)); Assert.IsTrue(Order(0, 1)); Assert.AreEqual(0, Funds); Assert.AreEqual(0, Supply(0));
        string id = Id(Delivery); Assert.IsFalse((bool)Call("OpenDelivery", id, 159d));
        Call("OpenDelivery", id, 164d); Call("TakeDelivery", id, 164d); Call("StoreDelivery", 0); Assert.AreEqual(1, Supply(0));
    }
    [Test] public void SupplySets_DeliverOneOfEachPerSetAndReserveEachShelfsCapacity()
    {
        Assert.IsTrue(Order(10, 3)); Assert.AreEqual(4560, Funds);
        for(int i=0;i<3;i++)Assert.AreEqual(0,Call("CatalogSpace",i));
        string id=Id(Delivery);Call("OpenDelivery",id,164d);Call("TakeDelivery",id,164d);Reload();
        Assert.AreEqual(3,Supply(0)); Assert.IsTrue((bool)Call("StoreDelivery",10));
        for(int i=0;i<3;i++)Assert.AreEqual(6,Supply(i));
        Assert.IsFalse((bool)Call("StoreDelivery",10));Reload();Assert.IsFalse((bool)Call("StoreDelivery",10));
        Assert.IsFalse(Order(10,1,195));
    }
    [Test] public void FullTableAndEmptySupplies_CannotTrapAThirdCustomersClothesInHand()
    {
        ((int[])Field(State,"supplies"))[0]=0;
        Accept(0,0);Assert.IsTrue(Station("Table",100));
        Accept(1,1);Assert.IsTrue(Station("Washer",100,0));
        Accept(2,2);Assert.IsTrue(Station("Washer",100,1));Assert.IsFalse((bool)Property("HasLaundryCargo"));
        Assert.IsTrue(Order(0,3));string id=Id(Delivery);Call("OpenDelivery",id,164d);
        Assert.IsTrue((bool)Call("TakeDelivery",id,164d));Assert.IsTrue((bool)Call("StoreDelivery",0));
        Assert.IsTrue(Station("Washer",164,1));Assert.IsTrue(Station("Washer",164,1));
        Assert.AreEqual("Washing",Field(Call("AtStation",Location("Washer"),1),"stage").ToString());
    }
    [Test] public void CollectionAndLaundry_SharePersistedSalesWithoutOverwritingOrDoublePaying()
    {
        Assert.IsTrue((bool)Call("TryTake",2));Assert.AreEqual(0,Call("CompleteCollection",1,0));
        Assert.AreEqual(6500,Call("CompleteCollection",2,1500));Assert.AreEqual(0,Call("CompleteCollection",2,1500));
        string id=Id(Accept());Station("Washer",100,0);Station("Washer",112,0);Station("Dryer",112);Station("Dryer",120);Station("Table",120);Station("Table",123);
        Assert.AreEqual(5000,Call("ReturnLaundry",id,0));Reload();
        Assert.AreEqual(11500,Field(State,"dailySales"));Assert.AreEqual(2,Field(State,"dailyCompleted"));Assert.AreEqual(8000,Funds);
        Assert.AreEqual(5,Call("Stock",2));Assert.AreEqual(0,Call("CompleteCollection",2,0));Assert.AreEqual(0,Call("ReturnLaundry",id,0));
    }
    [Test] public void VisitorMix_AlwaysIncludesBothTypesWithoutChangingRestoredTicketPurpose()
    {
        var manager=fixture.AddComponent(Type.GetType("GameManager, Assembly-CSharp",true));
        var next=manager.GetType().GetMethod("NextVisitPurpose");int collections=0,deposits=0;
        for(int i=0;i<40;i++){var purpose=next.Invoke(manager,null).ToString();if(purpose=="Collection")collections++;else deposits++;Assert.LessOrEqual(Math.Abs(collections-deposits),1);}
        Assert.AreEqual(20,collections);Assert.AreEqual(20,deposits);
        var ui=fixture.AddComponent(Type.GetType("NPCUI, Assembly-CSharp",true));
        string id=Id(Accept());Set(ui,"economy",economy);
        ui.GetType().GetMethod("BindLaundryTicket").Invoke(ui,new object[]{id});
        var purposeType=ui.GetType().GetNestedType("VisitPurpose");
        ui.GetType().GetMethod("ConfigureVisit").Invoke(ui,new[]{Enum.Parse(purposeType,"Collection")});
        Assert.AreEqual("DropOff",ui.GetType().GetProperty("Purpose").GetValue(ui).ToString());
    }
    [Test] public void DrumMotion_AcceleratesDeceleratesAndRespectsPauseAtDifferentFrameRates()
    {
        var step=Type.GetType("LaundryWorkflow, Assembly-CSharp",true).GetMethod("StepDrumSpeed");
        foreach(int rate in new[]{30,60,120})
        {
            float speed=0;
            for(int i=0;i<rate*2;i++){float next=(float)step.Invoke(null,new object[]{speed,true,1f/rate});Assert.GreaterOrEqual(next,speed);Assert.LessOrEqual(next,100);speed=next;}
            Assert.AreEqual(100,speed);Assert.AreEqual(speed,step.Invoke(null,new object[]{speed,false,0f}));
            for(int i=0;i<rate;i++){float next=(float)step.Invoke(null,new object[]{speed,false,1f/rate});Assert.LessOrEqual(next,speed);Assert.GreaterOrEqual(next,0);speed=next;}
            Assert.AreEqual(0,speed);
        }
    }
    [Test] public void MachineDisplay_MatchesActualWorkDeadlineAndCompletion()
    {
        var workflow=Type.GetType("LaundryWorkflow, Assembly-CSharp",true);var display=workflow.GetMethod("MachineDisplay");
        Assert.AreEqual("READY",display.Invoke(null,new object[]{null,100d}));
        object ticket=Accept();Station("Washer",100,0);
        Assert.AreEqual("00:12",display.Invoke(null,new[]{ticket,(object)100d}));
        Assert.AreEqual("00:01",display.Invoke(null,new[]{ticket,(object)111.1d}));
        Call("AdvanceLaundry",112d);Assert.AreEqual("DONE",display.Invoke(null,new[]{ticket,(object)112d}));
        var status=workflow.GetMethod("StationStatus");Assert.AreEqual("세탁 완료",status.Invoke(null,new[]{Location("Washer"),Field(ticket,"stage")}));
    }
}
