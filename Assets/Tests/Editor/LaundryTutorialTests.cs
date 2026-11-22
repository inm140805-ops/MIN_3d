using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

// Isolated state fixtures verify recovery instructions without touching the scene or player saves.
public class LaundryTutorialTests
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    GameObject root;
    Component economy,tutorial;
    TMP_Text body;
    Type locationType;
    object State=>economy.GetType().GetProperty("Operations").GetValue(economy);
    string Step=>(string)tutorial.GetType().GetProperty("StepKey").GetValue(tutorial);
    static object Field(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Flags).Invoke(o,args);
    Transform Point(string name){var g=new GameObject(name);g.transform.SetParent(root.transform);return g.transform;}
    object Location(string name)=>Enum.Parse(locationType,name);
    object Accept(int slot=0)=>Call(economy,"AcceptLaundry",0,1,slot,100d);
    bool Use(string place,double now=100,int machine=-1)=>(bool)Call(economy,"UseStation",Location(place),machine,now,"");
    [SetUp] public void Setup()
    {
        root=new GameObject("Isolated tutorial test");root.SetActive(false);
        economy=root.AddComponent(Type.GetType("ShopEconomy, Assembly-CSharp",true));Call(economy,"Initialize",7,false);
        tutorial=root.AddComponent(Type.GetType("LaundryTutorial, Assembly-CSharp",true));Set(tutorial,"economy",economy);
        var text=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(root.transform);body=text.GetComponent<TMP_Text>();
        Set(tutorial,"heading",body);Set(tutorial,"message",body);
        Set(tutorial,"terminalPoint",Point("Computer"));Set(tutorial,"supplyPoint",Point("Storage"));Set(tutorial,"deliveryPoint",Point("Parcel"));
        var stationType=Type.GetType("LaundryWorkflow+Station, Assembly-CSharp",true);locationType=Type.GetType("LaundryLocation, Assembly-CSharp",true);
        var stations=Array.CreateInstance(stationType,5);
        for(int i=0;i<5;i++){var s=Activator.CreateInstance(stationType);Set(s,"location",Location(i<3?"Washer":i==3?"Dryer":"Table"));Set(s,"machine",i<3?i:-1);Set(s,"title","Station "+i);Set(s,"usePoint",Point("Use "+i));stations.SetValue(s,i);}
        Set(tutorial,"stations",stations);
    }
    [TearDown] public void Cleanup()=>UnityEngine.Object.DestroyImmediate(root);
    [Test] public void Shortage_ParksGarmentThenOrdersWithoutChangingInventory()
    {
        ((int[])Field(State,"supplies"))[0]=0;var ticket=Accept();
        string before=(string)Call(economy,"CaptureSave");Call(tutorial,"HeldGuide",ticket);
        Assert.AreEqual("park-garment",Step);Assert.AreEqual(before,Call(economy,"CaptureSave"));
        Assert.IsTrue(Use("Washer",100,0));Call(tutorial,"StationGuide",ticket);
        Assert.AreEqual("order",Step);Assert.That(body.text,Does.Contain("꺼냈다가 다시"));
    }
    [Test] public void RefilledMachine_TellsPlayerToRetrieveAndReinsert()
    {
        ((int[])Field(State,"supplies"))[0]=0;var ticket=Accept();Use("Washer",100,0);
        ((int[])Field(State,"supplies"))[0]=2;Call(tutorial,"StationGuide",ticket);
        Assert.AreEqual("unload",Step);Assert.That(body.text,Does.Contain("다시 넣어"));
        Assert.IsTrue(Use("Washer",100,0));Call(tutorial,"HeldGuide",ticket);Assert.AreEqual("load-washer",Step);
    }
    [Test] public void BusyDryer_OffersTemporaryTableInsteadOfPretendingItCanDry()
    {
        Accept(0);Use("Washer",100,0);Use("Washer",112,0);Use("Dryer",112);
        var second=Accept(1);Use("Washer",100,1);Use("Washer",112,1);Call(tutorial,"HeldGuide",second);
        Assert.AreEqual("park-busy",Step);Assert.That(body.text,Does.Contain("임시 보관"));
    }
    [Test] public void RunningCycle_ExplainsWaitingWithoutCompletingTheGarment()
    {
        var ticket=Accept();Use("Washer",100,0);string before=(string)Call(economy,"CaptureSave");
        Call(tutorial,"StationGuide",ticket);Assert.AreEqual("wait-washing",Step);
        Assert.AreEqual(before,Call(economy,"CaptureSave"));Assert.That(body.text,Does.Contain("작동 중에는 꺼낼 수 없습니다"));
    }
    [Test] public void BusyDryerAndTable_CanParkWetGarmentInAnEmptyWasher()
    {
        Accept(0);Use("Washer",100,0);Use("Washer",112,0);Use("Dryer",112);
        Accept(1);Use("Table",112);
        var third=Accept(2);Use("Washer",100,1);Use("Washer",112,1);
        Call(tutorial,"HeldGuide",third);Assert.AreEqual("park-busy",Step);
        Assert.That(body.text,Does.Contain("Station 0"));
    }
    [Test] public void Delivery_OpenTakeAndStoreHaveDistinctInstructions()
    {
        Assert.IsTrue((bool)Call(economy,"OrderDelivery",0,3,0d,""));var delivery=Field(State,"delivery");string id=(string)Field(delivery,"id");
        Call(tutorial,"DeliveryGuide");Assert.AreEqual("open-parcel",Step);
        Call(economy,"OpenDelivery",id,64d);Call(tutorial,"DeliveryGuide");Assert.AreEqual("take-delivery",Step);
        Call(economy,"TakeDelivery",id,64d);Call(tutorial,"DeliveryGuide");Assert.AreEqual("store-delivery",Step);
        Assert.AreEqual(3,Call(economy,"Supply",0),"Guidance cannot grant stock merely by displaying a step");
    }
    [TestCase(7,7)] [TestCase(15,31)]
    public void DayReport_CompletesTutorialOnlyAfterAllFourLessons(int learned,int expected)
    {
        var loop=root.AddComponent(Type.GetType("CoreLoopManager, Assembly-CSharp",true));
        var computer=root.AddComponent(Type.GetType("ShopComputer, Assembly-CSharp",true));
        var report=Point("Report").gameObject;Set(loop,"reportFrame",report);
        Set(tutorial,"loop",loop);Set(tutorial,"computer",computer);
        var progress=Activator.CreateInstance(tutorial.GetType().GetNestedType("Progress",BindingFlags.NonPublic),true);
        Set(progress,"learned",learned);Set(tutorial,"progress",progress);
        var panel=new GameObject("Panel",typeof(RectTransform));panel.transform.SetParent(root.transform);
        Set(tutorial,"panel",panel);Set(tutorial,"panelRect",panel.GetComponent<RectTransform>());
        body.transform.SetParent(Point("Title"));Set(tutorial,"footer",body);
        Call(tutorial,"Tick");Assert.AreEqual("day-result",Step);Assert.AreEqual(expected,Field(progress,"learned"));
        Call(tutorial,"Tick");Assert.AreEqual(expected,Field(progress,"learned"),"Repeated report frames are idempotent");
    }
    [Test] public void DepartedCustomer_BetweenGuideTicksCannotBreakTheArrow()
    {
        Set(tutorial,"player",root.AddComponent(Type.GetType("Player, Assembly-CSharp",true)));
        var boxes=root.AddComponent(Type.GetType("BoxManager, Assembly-CSharp",true));Set(boxes,"boxs",new Transform[0]);Set(tutorial,"boxes",boxes);
        var npcType=Type.GetType("NPCUI, Assembly-CSharp",true);var gone=Point("Departed customer").gameObject.AddComponent(npcType);
        var customers=Array.CreateInstance(npcType,1);customers.SetValue(gone,0);Set(tutorial,"customers",customers);
        UnityEngine.Object.DestroyImmediate(gone.gameObject);Set(tutorial,"destination",new Vector3(1,0,0));
        Assert.AreEqual(new Vector3(0,.95f,0),Call(tutorial,"VisualPoint"));
    }
}
