#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Scene-aware sequential bot. Positions and order state are read; only keyboard/mouse events act on the game.
public class LaundryPlaytestScenario : MonoBehaviour
{
    private LaundryPlaytestDriver driver;
    private Player player;
    private Grab grab;
    private CoreLoopManager loop;
    private BoxManager boxes;
    private Transform[] shelves;
    private bool failed;
    private string mode;
    private int completedDays;
    private LaundryTutorial tutorial;
    private string savedTutorial;
    private readonly System.Collections.Generic.HashSet<string> tutorialSteps = new System.Collections.Generic.HashSet<string>();
    private void Update()
    {
        if(mode!="tutorial-flow" || !tutorial || !tutorial.IsGuiding || string.IsNullOrEmpty(tutorial.StepKey))return;
        if(tutorialSteps.Add(tutorial.StepKey))LaundryPlaytestDriver.LogEvent("tutorial-step",tutorial.StepKey);
    }
    private readonly System.Collections.Generic.HashSet<int> variedClothes = new System.Collections.Generic.HashSet<int>();

    public void Run(string scenario)
    {
        mode = scenario; driver = GetComponent<LaundryPlaytestDriver>();
        player = FindAnyObjectByType<Player>(); grab = player.GetComponent<Grab>();
        loop = FindAnyObjectByType<CoreLoopManager>(); boxes = FindAnyObjectByType<BoxManager>();
        shelves = Read<Transform[]>(boxes, "boxs");
        if (mode == "variety-day" || mode == "careful-day" || mode == "popular-day")
        {
            // Deterministic offer selection only; movement, deadlines, deliveries and rewards still use normal gameplay.
            var previous = mode == "variety-day" ? LaundryEvents.Offer.Careful : mode == "careful-day" ? LaundryEvents.Offer.Popular : LaundryEvents.Offer.Express;
            typeof(LaundryEvents).GetField("previousOffer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(LaundryEvents.Current, previous);
            LaundryPlaytestDriver.LogEvent("fixture", "First rotating offer selected for " + mode + "; no money, inventory or position changes");
        }
        StartCoroutine(Play());
    }
    private static T Read<T>(object owner, string field)
    {
        return (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
    private void Check(bool condition, string message)
    {
        LaundryPlaytestDriver.LogEvent("assertion", (condition ? "PASS: " : "FAIL: ") + message);
        if (!condition) { failed = true; LaundryPlaytestDriver.FinishSession(message, false); }
    }
    private IEnumerator Play()
    {
        yield return new WaitForSecondsRealtime(2f);
        if(mode=="tutorial-flow")
        {
            tutorial=FindAnyObjectByType<LaundryTutorial>();savedTutorial=PlayerPrefs.GetString("Laundry.Tutorial.v1","");
            Check(tutorial && tutorial.IsGuiding && tutorial.StepKey=="open","First run starts the authored tutorial at the opening sign");if(failed)yield break;
            Check(tutorial.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget),"Tutorial never blocks gameplay or computer clicks");if(failed)yield break;
            var pointer=tutorial.GetComponentInChildren<LaundryTutorialArrow>(true);
            Check(pointer && pointer.canvasRenderer && pointer.rectTransform.rect.width>0,"Objective arrow has a real CanvasRenderer and nonzero geometry");if(failed)yield break;
            driver.Capture("tutorial-first-shift");yield return new WaitForSecondsRealtime(.3f);
            driver.Tap("F1");yield return new WaitForSecondsRealtime(.3f);Check(!tutorial.IsGuiding,"F1 hides the tutorial");if(failed)yield break;
            driver.Tap("F1");yield return new WaitForSecondsRealtime(.3f);Check(tutorial.IsGuiding,"F1 restores the contextual tutorial");if(failed)yield break;
            yield return FollowTutorial("open");if(failed)yield break;
        }
        if (mode == "laundry-cycle" || mode == "mixed-visits" || mode=="tutorial-flow" || mode=="carton-review")
        {
            yield return TestLaundryCycle();
            if (!failed && mode != "laundry-cycle") yield return TestCollectionVisit();
            if (!failed) yield return new WaitForSecondsRealtime(.3f); // Flush the final gameplay screenshot before stopping.
            if(!failed && mode=="tutorial-flow")
            {
                string[] required={"open","computer-order","computer-close","wait-delivery","open-parcel","take-delivery","store-delivery","accept","load-washer","wait-washing","unload","load-dryer","wait-drying","load-table","wait-folding","return-laundry","read-collection","take-collection","wrong-clothes","return-collection"};
                string missing=string.Join(", ",required.Where(s=>!tutorialSteps.Contains(s)));
                Check(missing.Length==0,"Contextual arrows cover the real delivery and both customer workflows. Missing: "+missing);if(failed)yield break;
                Check((tutorial.Learned&15)==15,"All four operating lessons are recorded from actual completed actions");if(failed)yield break;
                Check(PlayerPrefs.GetString("Laundry.Tutorial.v1","")==savedTutorial,"QA does not alter the player's saved tutorial progress");if(failed)yield break;
            }
            if (!failed) LaundryPlaytestDriver.FinishSession("Delivery and customer wash-dry-fold-return cycle passed via keyboard/mouse", true);
            yield break;
        }
        if (mode == "computer-stock")
        {
            yield return TestComputer(); if (failed) yield break;
        }
        if (mode == "controls-regression")
        {
            driver.Tap("Escape"); yield return new WaitForSecondsRealtime(0.3f);
            Check(Time.timeScale == 0, "Escape pauses the game"); if (failed) yield break;
            Vector3 before = player.transform.position;
            driver.Drive(1, 1, 90, 0, 0.7f); yield return new WaitForSecondsRealtime(1f);
            Check(Vector3.Distance(before, player.transform.position) < 0.05f, "Pause blocks player motion"); if (failed) yield break;
            driver.Capture("pause");
            var gui = FindAnyObjectByType<GUIManager>();
            var pause = Read<GameObject>(gui, "escPanal");
            var settings = Read<GameObject>(gui, "settingPanal");
            var style = FindAnyObjectByType<ShopMenuStyle>();
            var stopButton = pause.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Stop");
            driver.Hover((RectTransform)stopButton.transform); yield return new WaitForSecondsRealtime(0.3f);
            Check(Read<int>(style, "selectedPauseIndex") == 2 && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == stopButton.gameObject,
                "Mouse hover synchronizes the yellow highlight and actual keyboard selection"); if (failed) yield break;
            Check(stopButton.GetComponent<UnityEngine.UI.Image>().sprite == Resources.Load<Sprite>("LaundrySkin/HoneyKey"),
                "Hovered pause button uses the yellow selected face"); if (failed) yield break;
            driver.Tap("UpArrow"); yield return new WaitForSecondsRealtime(0.3f);
            Check(Read<int>(style, "selectedPauseIndex") == 1 && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "Setting",
                "One up-arrow press selects exactly the highlighted settings button"); if (failed) yield break;
            driver.Tap("Enter"); yield return new WaitForSecondsRealtime(0.4f);
            Check(settings.activeSelf && !pause.activeSelf && Time.timeScale == 0, "Enter activates the highlighted settings button"); if (failed) yield break;
            driver.Click((RectTransform)settings.GetComponentInChildren<UnityEngine.UI.Button>().transform); yield return new WaitForSecondsRealtime(0.3f);
            Check(pause.activeSelf && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name == "Setting",
                "Returning from settings restores a valid pause selection"); if (failed) yield break;
            var settingsButton = pause.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Setting");
            driver.Click((RectTransform)settingsButton.transform); yield return new WaitForSecondsRealtime(0.4f);
            Check(settings.activeSelf && !pause.activeSelf && Time.timeScale == 0, "Mouse opens settings while paused"); if (failed) yield break;
            driver.Capture("settings");
            driver.Click((RectTransform)settings.GetComponentInChildren<UnityEngine.UI.Button>().transform); yield return new WaitForSecondsRealtime(0.3f);
            Check(pause.activeSelf && !settings.activeSelf, "Mouse returns from settings to pause menu"); if (failed) yield break;
            driver.Tap("Escape"); yield return new WaitForSecondsRealtime(0.3f);
            Check(Time.timeScale == 1, "Escape resumes the game"); if (failed) yield break;
            driver.Tap("Tab"); yield return new WaitForSecondsRealtime(0.3f);
            var guide = Read<UnityEngine.UI.Image>(FindAnyObjectByType<GUIManager>(), "OrderMenu");
            Check(guide.gameObject.activeSelf, "Tab opens clothing guide"); if (failed) yield break;
            driver.Capture("guide"); driver.Tap("Tab"); yield return new WaitForSecondsRealtime(0.3f);
            Check(!guide.gameObject.activeSelf, "Tab closes clothing guide"); if (failed) yield break;
        }

        Transform entrance = Read<Transform>(loop, "openOBJ");
        Vector3 openPosition = new Vector3(6.1f, player.transform.position.y, entrance.position.z + 1.1f);
        yield return Move(openPosition); if (failed) yield break;
        yield return Look(180); driver.Tap("E"); yield return new WaitForSecondsRealtime(0.25f);
        Check(loop.isOpen && loop.timerStart, "E opens the shop and starts the timer"); if (failed) yield break;
        driver.Capture("shop-open");
        if (mode == "closing-rush")
        {
            float untilOffer = Time.realtimeSinceStartup + 165f;
            while (loop.isOpen && Time.realtimeSinceStartup < untilOffer && LaundryEvents.Current.ActiveOffer != LaundryEvents.Offer.Closing)
                yield return null;
            Check(LaundryEvents.Current.ActiveOffer == LaundryEvents.Offer.Closing, "A closing offer starts naturally below target after its warning");
            if (failed) yield break;
            driver.Capture("closing-offer");
        }
        bool fullDay = mode == "normal-day" || mode == "day-reset" || mode == "variety-day" || mode == "careful-day" || mode == "popular-day" || mode == "two-days";
        int wantedDeliveries = fullDay ? Mathf.CeilToInt(loop.needMoney / 5000f) : 2;
        if (mode == "closing-rush") wantedDeliveries = 1;
        bool testedWrong = false;
        for (int delivery = 0; delivery < wantedDeliveries; delivery++)
        {
            int featuredClothing = -1;
            if (mode == "popular-day" && LaundryEvents.Current.ActiveOffer == LaundryEvents.Offer.Popular)
                foreach (BoxData data in boxes.boxDatas)
                    if (LaundryEvents.Current.Status.StartsWith(data.BoxName + " 인기 시간", System.StringComparison.Ordinal)) featuredClothing = data.Id;
            NPCUI customer = null; float wait = Time.realtimeSinceStartup + 65;
            while (customer == null && Time.realtimeSinceStartup < wait && loop.isOpen)
            {
                customer = FindObjectsByType<NPCUI>(FindObjectsSortMode.None)
                    .Where(n => n.Purpose == NPCUI.VisitPurpose.Collection && !n.orderComplete && !n.goOut && !n.GetComponent<NPCExit>().isExiting && n.GetComponent<NPCController>().enterLine)
                    .OrderBy(n => (mode == "variety-day" && n.AcceptedClothing.Any(id => !variedClothes.Contains(id))) || (featuredClothing >= 0 && n.AcceptedClothing.Contains(featuredClothing)) ? 0 : 1)
                    .ThenBy(n => mode == "closing-rush" ? Read<float>(n, "moodTimer") : n.GetComponent<NPCQueue>().lineTarget).FirstOrDefault();
                yield return null;
            }
            Check(customer != null, "A customer reached a service position"); if (failed) yield break;
            Vector3 service = new Vector3(4.8f, player.transform.position.y, customer.transform.position.z);
            yield return Move(new Vector3(6.9f, 0, service.z)); if (failed) yield break;
            yield return Move(service); if (failed) yield break;
            yield return Look(270); driver.Tap("E"); yield return new WaitForSecondsRealtime(0.5f);
            Check(Read<bool>(customer, "cheakColl"), "E reads the selected customer's request"); if (failed) yield break;
            if (delivery == 0) driver.Capture("order-ticket");
            OrderData order = customer.orderDatas.First(o => o.Id == customer.GetComponent<NPCController>().orderDataID);
            int correct = System.Array.FindIndex(boxes.boxDatas, data => order.CanID.Contains(data.Id));
            if (featuredClothing >= 0 && order.CanID.Contains(featuredClothing))
                correct = System.Array.FindIndex(boxes.boxDatas, data => data.Id == featuredClothing);
            if (mode == "variety-day" && LaundryEvents.Current.ActiveOffer == LaundryEvents.Offer.Variety)
            {
                int unused = System.Array.FindIndex(boxes.boxDatas, data => order.CanID.Contains(data.Id) && !variedClothes.Contains(data.Id));
                if (unused >= 0) correct = unused;
            }
            Check(correct >= 0, "Order has at least one available clothing match"); if (failed) yield break;

            if (mode == "controls-regression" && !testedWrong)
            {
                int wrong = System.Array.FindIndex(boxes.boxDatas, data => !order.CanID.Contains(data.Id));
                if (wrong >= 0)
                {
                    yield return PickUp(wrong); if (failed) yield break;
                    yield return ReturnTo(service); if (failed) yield break;
                    float oldMoney = player.money; driver.Tap("E"); yield return new WaitForSecondsRealtime(0.3f);
                    Check(!customer.orderComplete && player.money == oldMoney && grab.itemID == wrong, "Wrong delivery preserves the item and money"); if (failed) yield break;
                    driver.Capture("wrong-order");
                    yield return Shelf(wrong); if (failed) yield break;
                    driver.Tap("E"); yield return new WaitForSecondsRealtime(0.25f);
                    Check(grab.itemID == -1, "E returns the wrong item to its shelf"); if (failed) yield break;
                    testedWrong = true;
                }
            }
            yield return PickUp(correct); if (failed) yield break;
            yield return ReturnTo(service); if (failed) yield break;
            float moneyBefore = player.money, countBefore = player.completeOrder;
            var shop = FindAnyObjectByType<ShopEconomy>();
            int fundsBefore = shop != null ? shop.Funds : 0;
            int bonusBefore = LaundryEvents.Current != null ? LaundryEvents.Current.BonusEarned : 0;
            bool varietyActive = LaundryEvents.Current != null && LaundryEvents.Current.ActiveOffer == LaundryEvents.Offer.Variety;
            driver.Tap("E"); yield return new WaitForSecondsRealtime(0.35f);
            int bonus = LaundryEvents.Current != null ? LaundryEvents.Current.BonusEarned - bonusBefore : 0;
            Check(customer.orderComplete && player.money == moneyBefore + 5000 + bonus && player.completeOrder == countBefore + 1 && grab.itemID == -1,
                "Correct delivery awards base 5000 plus event bonus " + bonus + ", counts once and empties hands"); if (failed) yield break;
            if (varietyActive) variedClothes.Add(boxes.boxDatas[correct].Id);
            // Press again while the completed customer is still visible.
            driver.Tap("E"); yield return new WaitForSecondsRealtime(0.25f);
            Check(player.money == moneyBefore + 5000 + bonus, "Repeated E cannot duplicate a completed order reward"); if (failed) yield break;
            if (mode == "computer-stock")
            {
                Check(shop.Funds == fundsBefore + shop.OrderIncome, "Delivery credits operating funds exactly once, separately from daily sales"); if (failed) yield break;
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }
        if (mode == "closing-rush")
        {
            Check(LaundryEvents.Current.BonusEarned == 2000 && player.money == 7000, "Late delivery earns the base reward plus the closing bonus");
            if (failed) yield break;
            float untilResult = Time.realtimeSinceStartup + 45f;
            while (!loop.IsResultOpen && Time.realtimeSinceStartup < untilResult) yield return null;
            Check(loop.IsResultOpen && !loop.isOpen && LaundryEvents.Current.ActiveOffer == LaundryEvents.Offer.None,
                "Timeout closes the shop and clears the active offer"); if (failed) yield break;
            driver.Capture("closing-result");
            var retry = Read<GameObject>(loop, "fallButtons").GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "Retry day");
            driver.Click((RectTransform)retry.transform); yield return new WaitForSecondsRealtime(0.4f);
            Check(!loop.IsResultOpen && loop.DayNumber == 1 && loop.needMoney == 30000 && player.money == 0,
                "Failed-day retry preserves the day and target"); if (failed) yield break;
        }
        if (fullDay)
        {
            if (mode == "variety-day")
            {
                Check(LaundryEvents.Current.BonusEarned >= 3000, "Three distinct valid deliveries earn the Variety reward through normal input");
                if (failed) yield break;
            }
            if (mode == "careful-day" || mode == "popular-day")
            {
                int expected = mode == "careful-day" ? 2500 : 1500;
                Check(LaundryEvents.Current.BonusEarned >= expected, "The selected offer pays its reward through normal deliveries");
                if (failed) yield break;
            }
            yield return Move(new Vector3(6.9f, 0, player.transform.position.z)); if (failed) yield break;
            yield return Move(openPosition); if (failed) yield break;
            yield return Look(180); driver.Tap("E"); yield return new WaitForSecondsRealtime(0.4f);
            Check(loop.IsResultOpen && !loop.isOpen && player.money >= loop.needMoney, "E closes a successful day"); if (failed) yield break;
            driver.Capture("successful-day");
            Vector3 before = player.transform.position; driver.Tap("E"); driver.Drive(1, 1, 45, 0, 0.5f);
            yield return new WaitForSecondsRealtime(0.8f);
            Check(!loop.isOpen && Vector3.Distance(before, player.transform.position) < 0.05f, "Results screen blocks reopening and movement"); if (failed) yield break;
            if (mode == "two-days" && completedDays == 0)
            {
                completedDays++;
                var nextDay = Read<GameObject>(loop, "winButtons").GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "KeepGoing");
                driver.Click((RectTransform)nextDay.transform); yield return new WaitForSecondsRealtime(0.4f);
                Check(!loop.IsResultOpen && loop.DayNumber == 2 && loop.needMoney == 35000 && player.money == 0,
                    "Two-day run advances to a fresh day with one additional required order"); if (failed) yield break;
                yield return Play();
                yield break;
            }
            if (mode == "day-reset")
            {
                var next = Read<GameObject>(loop, "winButtons").GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "KeepGoing");
                driver.Click((RectTransform)next.transform); yield return new WaitForSecondsRealtime(0.4f);
                Check(!loop.IsResultOpen && !loop.isOpen && loop.DayNumber == 2 && loop.needMoney == 35000 && player.money == 0 && grab.itemID == -1,
                    "Next day resets money/inventory and raises the target by only one base order"); if (failed) yield break;
                driver.Tap("E"); yield return new WaitForSecondsRealtime(0.4f);
                Check(loop.isOpen && LaundryEvents.Current.BonusEarned == 0 && LaundryEvents.Current.ActiveOffer == LaundryEvents.Offer.None,
                    "New opening clears the previous day's event and bonus total"); if (failed) yield break;
                yield return new WaitForSecondsRealtime(39f);
                string offerBeforePause = LaundryEvents.Current.Status;
                Check(offerBeforePause.Length > 0, "Second day starts a fresh offer"); if (failed) yield break;
                driver.Tap("Escape"); yield return new WaitForSecondsRealtime(0.3f);
                float secondsBefore = loop.SecondsRemaining;
                offerBeforePause = LaundryEvents.Current.Status;
                yield return new WaitForSecondsRealtime(1.2f);
                Check(Time.timeScale == 0 && loop.SecondsRemaining == secondsBefore && LaundryEvents.Current.Status == offerBeforePause,
                    "Pause freezes both the day clock and offer countdown"); if (failed) yield break;
                var style = FindAnyObjectByType<ShopMenuStyle>();
                driver.Tap("UpArrow"); yield return new WaitForSecondsRealtime(0.15f);
                driver.Tap("UpArrow"); yield return new WaitForSecondsRealtime(0.15f);
                driver.Tap("DownArrow"); yield return new WaitForSecondsRealtime(0.3f);
                Check(Read<int>(style, "selectedPauseIndex") == 1, "Down arrow selects the second pause button"); if (failed) yield break;
                driver.Tap("UpArrow"); yield return new WaitForSecondsRealtime(0.3f);
                Check(Read<int>(style, "selectedPauseIndex") == 0, "Up arrow selects the first pause button"); if (failed) yield break;
                driver.Tap("Escape"); yield return new WaitForSecondsRealtime(0.4f);
                Check(Time.timeScale == 1, "Resume restores gameplay after day transition"); if (failed) yield break;
                driver.Capture("second-day-offer");
            }
        }
        yield return new WaitForSecondsRealtime(0.5f);
        LaundryPlaytestDriver.FinishSession(mode + ": all input-driven assertions passed", true);
    }
    private IEnumerator TestLaundryCycle()
    {
        var shop = FindAnyObjectByType<ShopEconomy>(); var terminal = ShopComputer.Current;
        if(mode=="tutorial-flow")yield return Move(new Vector3(6.9f,0,12.65f));
        else {yield return Move(new Vector3(6.9f, 0, 27)); yield return Move(new Vector3(6.9f, 0, 14.4f));}
        yield return Move(new Vector3(8.75f, 0, 12.7f)); if (failed) yield break;
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.4f);
        Check(terminal.IsOpen, "E opens the physical order computer"); if (failed) yield break;
        driver.Click((RectTransform)Read<UnityEngine.UI.Button>(terminal, "restockButton").transform); yield return new WaitForSecondsRealtime(.3f);
        var delivery = shop.Operations.delivery;
        LaundryPlaytestDriver.LogEvent("order-state", "Funds=" + shop.Funds + "; detergent=" + shop.Supply(0) + "; delivery=" + JsonUtility.ToJson(delivery) + "; notice=" + Read<TMPro.TMP_Text>(terminal, "noticeText").text);
        if(mode!="carton-review")driver.Capture("delivery-order"); yield return new WaitForSecondsRealtime(.2f);
        Check(delivery != null && delivery.quantity == 3 && shop.Funds == 5400 && shop.Supply(0) == 3 && delivery.dueAt - delivery.orderedAt == 60, "Order charges 600 once and schedules delivery exactly 60 seconds later"); if (failed) yield break;
        driver.Tap("Escape"); yield return new WaitForSecondsRealtime(.3f);
        yield return Move(new Vector3(6.45f, 0, 12.7f)); yield return Move(new Vector3(6.45f, 0, 7.6f)); yield return Move(new Vector3(-11.5f, 0, 7.6f)); yield return Move(new Vector3(-11.5f, 0, 8.6f)); yield return Move(new Vector3(-9.8f, 0, 8.6f)); if (failed) yield break;
        int liftSamples = 0;
        while (LaundryClock.Now < delivery.dueAt + 4.1)
        {
            if (LaundryClock.Now >= delivery.dueAt + 1.1)
            {
                var parcel = Read<Transform>(LaundryWorkflow.Current, "parcel");
                var gate = Read<Transform>(LaundryWorkflow.Current, "tailgate").GetComponentInChildren<Renderer>();
                if (parcel.gameObject.activeInHierarchy)
                {
                    if (parcel.GetComponent<Collider>().bounds.Intersects(gate.bounds))
                    { Check(false, "Tail lift intersects the delivery parcel during unloading"); yield break; }
                    liftSamples++;
                }
            }
            yield return null;
        }
        Check(liftSamples > 0, "Tail lift lowers the parcel without intersecting it across runtime frames"); if (failed) yield break;
        var truck = Read<Transform>(LaundryWorkflow.Current, "truck");
        Check(Vector3.Distance(truck.position, new Vector3(-5, .29f, 5.8f)) < .1f, "Truck has reached the delivery bay"); if (failed) yield break;
        yield return Move(new Vector3(-9.8f, 0, 8.6f)); if (failed) yield break;
        yield return Look(180, 25); if(mode!="carton-review")driver.Capture("truck-unloaded"); yield return new WaitForSecondsRealtime(.2f);
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f); Check(delivery.opened && shop.Supply(0) == 3, "Opening the parcel does not grant stock"); if (failed) yield break;
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f); Check(delivery.carrying == 3, "E takes three physical supplies"); if (failed) yield break;
        driver.Capture("delivery-carry");
        if(mode=="tutorial-flow")yield return FollowTutorial("store-delivery");
        else {yield return Move(new Vector3(-11.5f, 0, 8.6f)); yield return Move(new Vector3(-11.5f, 0, 3)); yield return Move(new Vector3(6.45f, 0, 3)); yield return Move(new Vector3(6.45f, 0, 12.7f)); yield return Move(new Vector3(6.9f, 0, 12.7f)); yield return Move(new Vector3(6.9f, 0, 15)); yield return Move(new Vector3(12.1f, 0, 15)); yield return Move(new Vector3(12.1f, 0, 13.6f));} if (failed) yield break;
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f); Check(delivery.Complete && shop.Supply(0) == 6, "Storage deposit increases inventory exactly once"); if (failed) yield break;
        yield return Move(new Vector3(12.1f, 0, 15)); yield return Move(new Vector3(6.9f, 0, 15)); yield return Move(new Vector3(6.9f, 0, 12.65f)); yield return Move(new Vector3(6.1f, 0, 12.65f));
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.4f); Check(loop.isOpen, "Shop opens normally after a delivery"); if (failed) yield break;
        NPCUI customer = null; float until = Time.realtimeSinceStartup + 80;
        while (!customer && Time.realtimeSinceStartup < until) { customer = FindObjectsByType<NPCUI>(FindObjectsSortMode.None).FirstOrDefault(n => n.IsWaitingForOrder && n.Purpose == NPCUI.VisitPurpose.DropOff); yield return null; }
        Check(customer != null, "Customer arrives via the normal NPC route"); if (failed) yield break;
        Vector3 service = new Vector3(4.8f, 0, customer.transform.position.z); yield return ReturnTo(service); if (failed) yield break;
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.4f); var ticket = shop.HeldTicket;
        Check(ticket != null && customer.LaundryTicketId == ticket.id, "NPC deposits its uniquely numbered garment"); if (failed) yield break;
        if(mode=="carton-review"){driver.Capture("carton-held-laundry");yield return new WaitForSecondsRealtime(.3f);}
        yield return Move(new Vector3(6.9f, 0, service.z)); yield return Move(new Vector3(6.9f, 0, 27)); yield return Move(new Vector3(5.15f, 0, 28.4f));
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f); Check(ticket.stage == LaundryStage.Washing && shop.Supply(0) == 5, "Washer consumes one detergent and starts a real cycle"); if (failed) yield break;
        yield return Look(0, 25);
        var washer = Read<LaundryWorkflow.Station[]>(LaundryWorkflow.Current, "stations")[0];
        Quaternion before = washer.drum.localRotation;
        yield return new WaitForSecondsRealtime(2f);
        Check(Quaternion.Angle(before, washer.drum.localRotation) > 20 && washer.contents == null && washer.display.text != "READY", "Only the inner drum rotates and its display counts down during washing"); if (failed) yield break;
        if(mode!="carton-review")driver.Capture("washer-running");
        if(mode=="tutorial-flow")
        {
            var point=(Vector3)typeof(LaundryTutorial).GetMethod("VisualPoint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tutorial,null);
            Check(Camera.main.WorldToScreenPoint(point).z>0,"At a washer the arrow points at its visible drum, not behind the player");if(failed)yield break;
        }
        while (ticket.stage == LaundryStage.Washing) yield return null;
        if(mode=="tutorial-flow"){yield return new WaitForSecondsRealtime(.3f);Check(tutorial.StepKey=="unload","Completed washer changes the guide to retrieve the garment");if(failed)yield break;}
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f);
        yield return Move(new Vector3(6.9f, 0, 27)); yield return Move(new Vector3(6.9f, 0, 15)); yield return Move(new Vector3(12.1f, 0, 15)); yield return Move(new Vector3(12.4f, 0, 12));
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f); Check(ticket.stage == LaundryStage.Drying, "Wet garment enters the dryer"); if (failed) yield break;
        yield return Look(180, 25); if(mode!="carton-review")driver.Capture("dryer-running");
        while (ticket.stage == LaundryStage.Drying) yield return null;
        if(mode=="tutorial-flow")yield return new WaitForSecondsRealtime(.3f);
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f);
        yield return Move(new Vector3(12.1f, 0, 15)); yield return Move(new Vector3(6.65f, 0, 14.4f)); driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f);
        Check(ticket.stage == LaundryStage.Folding, "Dry garment is folded and packaged"); if (failed) yield break;
        if(mode=="carton-review"){yield return Look(270,25);driver.Capture("carton-on-table");yield return new WaitForSecondsRealtime(.3f);}
        while (ticket.stage == LaundryStage.Folding) yield return null;
        if(mode=="tutorial-flow")yield return new WaitForSecondsRealtime(.3f);
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f); yield return ReturnTo(service); if (failed) yield break;
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.4f);
        Check(customer.orderComplete && ticket.stage == LaundryStage.Returned && player.money >= 5000 && shop.Funds == 6400 && !shop.HasLaundryCargo, "Correct customer receives the finished clothes and payment happens once");
        driver.Capture("laundry-return-complete");
    }

    private IEnumerator TestCollectionVisit()
    {
        NPCUI customer=null;float until=Time.realtimeSinceStartup+60;
        while(!customer&&Time.realtimeSinceStartup<until){customer=FindObjectsByType<NPCUI>(FindObjectsSortMode.None).FirstOrDefault(n=>n.IsWaitingForOrder&&n.Purpose==NPCUI.VisitPurpose.Collection);yield return null;}
        Check(customer!=null,"A collection customer coexists with laundry deposit visitors");if(failed)yield break;
        Vector3 service=new Vector3(4.8f,0,customer.transform.position.z);yield return ReturnTo(service);if(failed)yield break;
        driver.Tap("E");yield return new WaitForSecondsRealtime(.35f);
        if(mode=="tutorial-flow"){Check(tutorial.StepKey=="take-collection","Reading a different customer's request immediately retargets the tutorial to their shelf");if(failed)yield break;}
        var shop=FindAnyObjectByType<ShopEconomy>();Check(shop.HeldTicket==null,"Collection request does not create a new dirty garment");if(failed)yield break;
        int item=System.Array.FindIndex(boxes.boxDatas,b=>customer.AcceptedClothing.Contains(b.Id));
        int wrong=System.Array.FindIndex(boxes.boxDatas,b=>!customer.AcceptedClothing.Contains(b.Id));
        float sales=player.money;int funds=shop.Funds;
        if(wrong>=0){yield return PickUp(wrong);yield return ReturnTo(service);if(failed)yield break;driver.Tap("E");yield return new WaitForSecondsRealtime(.3f);Check(!customer.orderComplete&&player.money==sales&&grab.itemID==wrong,"Wrong collection keeps garment and money intact");if(failed)yield break;yield return Shelf(wrong);driver.Tap("E");yield return new WaitForSecondsRealtime(.3f);}
        yield return PickUp(item);yield return ReturnTo(service);if(failed)yield break;
        if(mode=="carton-review"){driver.Capture("carton-collection");yield return new WaitForSecondsRealtime(.3f);}
        driver.Tap("E");yield return new WaitForSecondsRealtime(.35f);
        Check(customer.orderComplete&&grab.itemID==-1&&player.money>=sales+5000&&shop.Funds==funds+shop.OrderIncome&&shop.Operations.dailySales==(int)player.money,"Existing clothing collection completes without overwriting laundry income");if(failed)yield break;
        sales=player.money;driver.Tap("E");yield return new WaitForSecondsRealtime(.25f);Check(player.money==sales,"Repeated collection interaction cannot duplicate payment");
        driver.Capture("mixed-collection-complete");
    }

    private IEnumerator TestComputer()
    {
        var terminal = FindAnyObjectByType<ShopComputer>(); var shop = FindAnyObjectByType<ShopEconomy>();
        yield return Move(new Vector3(6.9f, 0, 12.7f)); if (failed) yield break;
        yield return Move(Read<Transform>(terminal, "usePosition").position); if (failed) yield break;
        yield return Look(90); driver.Capture("computer-world"); yield return new WaitForSecondsRealtime(.3f);
        driver.Tap("E"); yield return new WaitForSecondsRealtime(.35f);
        Check(terminal.IsOpen && Time.timeScale == 0, "E opens the real computer and pauses the world"); if (failed) yield break;
        Vector3 before = player.transform.position; float clock = Read<float>(loop, "totalTime");
        driver.Drive(1, 1, 0, 0, .5f); yield return new WaitForSecondsRealtime(.7f);
        Check(Vector3.Distance(before, player.transform.position) < .05f && Read<float>(loop, "totalTime") == clock, "Computer blocks movement and freezes the shop clock"); if (failed) yield break;
        driver.Capture("computer-initial");
        var upgrades = Read<UnityEngine.UI.Button[]>(terminal, "upgradeButtons");
        driver.Click((RectTransform)upgrades[0].transform); yield return new WaitForSecondsRealtime(.3f);
        Check(shop.Capacity == 9 && shop.Funds == 2000 && player.money == 0, "Mouse purchases storage expansion without changing daily sales"); if (failed) yield break;
        driver.Click((RectTransform)Read<UnityEngine.UI.Button>(terminal, "restockButton").transform); yield return new WaitForSecondsRealtime(.3f);
        Check(shop.Stock(0) == 9 && shop.Funds == 1100, "Mouse restocks exactly three garments for 900 operating funds"); if (failed) yield break;
        Check(!Read<UnityEngine.UI.Button>(terminal, "restockButton").interactable && !upgrades[0].interactable, "Full stock and unaffordable upgrades disable their purchase buttons"); if (failed) yield break;
        driver.Capture("computer-purchased");
        driver.Tap("Escape"); yield return new WaitForSecondsRealtime(.3f);
        Check(!terminal.IsOpen && Time.timeScale == 1 && !FindAnyObjectByType<GUIManager>().esc, "Escape closes the computer without opening the pause menu"); if (failed) yield break;
        yield return PickUp(0); if (failed) yield break;
        Check(shop.Stock(0) == 8, "Physical box pickup removes one garment from stock"); if (failed) yield break;
        driver.Capture("pixel-interaction"); driver.Tap("E"); yield return new WaitForSecondsRealtime(.3f);
        Check(shop.Stock(0) == 9 && grab.itemID == -1, "Returning the garment restores inventory without awarding money"); if (failed) yield break;
        yield return Move(new Vector3(6.9f, 0, player.transform.position.z));
    }
    private IEnumerator Shelf(int item)
    {
        yield return Move(new Vector3(6.9f, 0, player.transform.position.z)); if (failed) yield break;
        yield return Move(new Vector3(6.9f, 0, shelves[item].position.z)); if (failed) yield break;
        yield return Move(new Vector3(11f, 0, shelves[item].position.z)); if (failed) yield break;
        yield return Look(90);
    }
    private IEnumerator PickUp(int item)
    {
        yield return Shelf(item); if (failed) yield break;
        driver.Tap("E"); yield return new WaitForSecondsRealtime(0.25f);
        Check(grab.itemID == item, "E picks up shelf item " + item);
    }
    private IEnumerator ReturnTo(Vector3 service)
    {
        yield return Move(new Vector3(6.9f, 0, player.transform.position.z)); if (failed) yield break;
        yield return Move(new Vector3(6.9f, 0, service.z)); if (failed) yield break;
        yield return Move(service); if (failed) yield break;
        yield return Look(270);
    }
    private IEnumerator Move(Vector3 destination)
    {
        if (failed) yield break;
        // Cardinal camera axes keep input-driven walking inside the measured aisle,
        // instead of zigzagging into doorposts after looking at a machine or truck.
        yield return Look(0); if (failed) yield break;
        LaundryPlaytestDriver.LogEvent("route", "Walk via input to " + destination);
        float until = Time.realtimeSinceStartup + 12;
        while (Time.realtimeSinceStartup < until)
        {
            Vector3 delta = destination - player.transform.position; delta.y = 0;
            // Digital keys at the QA 30 FPS cap can move ~0.32m per sampled input.
            // Accept that input quantum; actual E interactions still verify every destination.
            if (delta.magnitude < 0.35f) { driver.Drive(0, 0, 0, 0, 0, false); yield return new WaitForSecondsRealtime(0.12f); yield break; }
            if (loop.IsResultOpen) { Check(false, "Day ended before route completed"); yield break; }
            Vector3 forward = Camera.main.transform.forward; forward.y = 0; forward.Normalize();
            Vector3 right = Camera.main.transform.right; right.y = 0; right.Normalize();
            float h = Vector3.Dot(delta, right), v = Vector3.Dot(delta, forward);
            // Finish one axis at a time to avoid overshoot at digital-input diagonals.
            if (Mathf.Abs(h) > Mathf.Abs(v)) driver.Drive(Mathf.Sign(h), 0, 0, 0, 0.15f, false);
            else driver.Drive(0, Mathf.Sign(v), 0, 0, 0.15f, false);
            yield return null;
        }
        driver.Drive(0, 0, 0, 0, 0, false); driver.Capture("route-blocked");
        Check(false, "Movement route blocked near " + player.transform.position + "; destination " + destination);
    }
    private IEnumerator FollowTutorial(string step)
    {
        float until=Time.realtimeSinceStartup+60;
        while(Time.realtimeSinceStartup<until && tutorial.StepKey==step && tutorial.Destination.HasValue)
        {
            Vector3 delta=tutorial.Destination.Value-player.transform.position;delta.y=0;
            bool storageReady=step=="store-delivery" && Read<int>(LaundryWorkflow.Current,"nextAction")==100;
            if(delta.magnitude<.3f || storageReady){Check(true,"Following tutorial arrows reaches "+step+" through walkable space");yield break;}
            yield return WalkTowardArrow(Read<Vector3>(tutorial,"arrowPoint"));if(failed)yield break;
            yield return new WaitForSecondsRealtime(.2f);
        }
        Check(false,"Tutorial arrow route did not reach "+step);
    }
    private IEnumerator WalkTowardArrow(Vector3 point)
    {
        Vector3 initial=point-player.transform.position;initial.y=0;
        yield return Look(Mathf.Atan2(initial.x,initial.z)*Mathf.Rad2Deg);if(failed)yield break;
        LaundryPlaytestDriver.LogEvent("route","Walk straight toward tutorial arrow "+point);
        float until=Time.realtimeSinceStartup+15;
        while(Time.realtimeSinceStartup<until)
        {
            Vector3 delta=point-player.transform.position;delta.y=0;
            bool storageReady=tutorial.StepKey=="store-delivery" && Read<int>(LaundryWorkflow.Current,"nextAction")==100;
            if(delta.magnitude<.25f || storageReady){driver.Drive(0,0,0,0,0,false);yield break;}
            float turn=Mathf.DeltaAngle(player.transform.eulerAngles.y,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
            driver.Drive(0,Mathf.Abs(turn)<5?1:0,Mathf.Clamp(turn,-20,20),0,.15f,false);
            yield return null;
        }
        driver.Drive(0,0,0,0,0,false);Check(false,"Arrow path blocked near "+player.transform.position+" toward "+point);
    }
    private IEnumerator Look(float angle, float pitch = 0)
    {
        float until = Time.realtimeSinceStartup + 2;
        while (Time.realtimeSinceStartup < until)
        {
            float delta = Mathf.DeltaAngle(player.transform.eulerAngles.y, angle);
            float pitchDelta = pitch - Read<float>(player, "xRotation");
            if (Mathf.Abs(delta) < 0.5f && Mathf.Abs(pitchDelta) < .5f) yield break;
            driver.Drive(0, 0, Mathf.Clamp(delta, -30, 30), Mathf.Clamp(pitchDelta, -20, 20), 0, false);
            yield return new WaitForEndOfFrame();
        }
        Check(false, "Camera turn did not converge");
    }
}
#endif
