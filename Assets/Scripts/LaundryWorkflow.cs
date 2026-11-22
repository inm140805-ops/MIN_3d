using System;
using System.Text;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class LaundryWorkflow : MonoBehaviour
{
    [Serializable] public class Station
    {
        public string title;
        public LaundryLocation location;
        public int machine = -1;
        public Transform usePoint, drum;
        public TMP_Text status;
        public TMP_Text display;
        public GameObject contents;
        [NonSerialized] public float rotationSpeed;
    }
    public static LaundryWorkflow Current { get; private set; }
    [SerializeField] private Station[] stations;
    [SerializeField] private Transform truck, tailgate, parcel, parcelStart, parcelEnd, parcelUsePoint, supplyUsePoint;
    [SerializeField] private Transform[] arrivalRoute, departureRoute;
    [SerializeField] private GameObject closedParcel, openParcel, carriedParcel, carriedGarment, cargoRoot;
    [SerializeField] private TMP_Text deliveryText, jobsText, actionText, cargoText;
    [SerializeField] private TMP_Text parcelLabel;
    [SerializeField] private TMP_Text[] supplyLabels;
    [SerializeField] private GameObject deliveryPanel, jobsPanel, actionPanel;
    [SerializeField] private AudioSource notificationAudio;
    [SerializeField] private AudioClip arrivalBell;
    private ShopEconomy economy;
    private BoxManager boxes;
    private Player player;
    private Grab grab;
    private CoreLoopManager loop;
    private Transform[] stockBoxes;
    private int nextAction = -1, lastSecond = -1, handledFrame = -1;
    private string soundedDelivery;
    private Transform[] wheels;
    private Vector3 tailgateRestPosition;
    public bool HandledThisFrame => handledFrame == Time.frameCount;
    public void ClaimInteraction() { handledFrame = Time.frameCount; }
    public static float StepDrumSpeed(float current, bool working, float delta) => Mathf.MoveTowards(current, working ? 100 : 0, Mathf.Max(0, delta) * (working ? 70 : 140));
    public bool HasCargo => economy != null && economy.HasLaundryCargo;
    public bool NearWorkstation => nextAction >= 0;
    public bool BlocksLegacyInteraction => HasCargo || NearWorkstation || HandledThisFrame;
    public string HeldDescription
    {
        get
        {
            if (economy == null) return "";
            var ticket = economy.HeldTicket;
            if (ticket != null) return "#" + ticket.id + " " + ClothingName(ticket.clothing) + " " + ticket.quantity + "벌\n" + StageName(ticket.stage);
            var d = economy.Operations.delivery;
            return d != null && d.carrying > 0 ? CatalogName(d.catalog) + " ×" + d.carrying + "\n보관함에 넣기" : "";
        }
    }
    private void Awake()
    {
        Current = this; economy = FindAnyObjectByType<ShopEconomy>(); boxes = FindAnyObjectByType<BoxManager>();
        player = FindAnyObjectByType<Player>(); grab = player.GetComponent<Grab>(); loop = FindAnyObjectByType<CoreLoopManager>();
        stockBoxes = boxes.StoragePositions;
        wheels = System.Array.FindAll(truck.GetComponentsInChildren<Transform>(), t => t.name == "Wheel");
        tailgateRestPosition = tailgate.localPosition;
    }
    private void Start()
    {
        player.money = economy.Operations.dailySales; player.completeOrder = economy.Operations.dailyCompleted;
        economy.Changed += RefreshPanels; RefreshPanels();
        FindAnyObjectByType<GameManager>().RestoreLaundryCustomers();
    }
    private void OnDestroy()
    {
        if (economy != null) economy.Changed -= RefreshPanels;
        if (Current == this) Current = null;
    }
    public string ClothingName(int index) => index >= 0 && index < boxes.boxDatas.Length ? boxes.boxDatas[index].BoxName : "의류";
    public string CatalogName(int catalog) => catalog == economy.SupplySetCatalog ? "세탁 소모품 세트" : catalog < 3 ? ShopEconomy.SupplyNames[Mathf.Clamp(catalog, 0, 2)] : ClothingName(catalog - 3);
    public static string StageName(LaundryStage stage)
    {
        string[] names = { "세탁 전", "세탁 중", "건조 필요", "건조 중", "정리·포장 필요", "포장 중", "반환 준비", "반환 완료" };
        return names[(int)stage];
    }
    public static string StationStatus(LaundryLocation location, LaundryStage stage)
    {
        if (location == LaundryLocation.Washer && stage == LaundryStage.Wet) return "세탁 완료";
        if (location == LaundryLocation.Dryer && stage == LaundryStage.Dry) return "건조 완료";
        if (location == LaundryLocation.Table && stage == LaundryStage.Ready) return "포장 완료";
        return StageName(stage);
    }
    public static string MachineDisplay(LaundryTicket ticket, double now)
    {
        if (ticket == null) return "READY";
        if (ticket.stage == LaundryStage.Washing || ticket.stage == LaundryStage.Drying)
        { int seconds = Mathf.Max(0, (int)Math.Ceiling(ticket.readyAt - now)); return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"); }
        return ticket.stage == LaundryStage.Dirty ? "SUPPLY" : "DONE";
    }
    private void Update()
    {
        if (economy == null || player == null) return;
        double now = LaundryClock.Now; economy.AdvanceLaundry(now); UpdateTruck(now);
        bool carrying = HasCargo;
        player.grabClothes = carrying || grab.itemID >= 0;
        cargoRoot.SetActive(carrying);
        carriedGarment.SetActive(economy.HeldTicket != null); carriedParcel.SetActive(carrying && economy.HeldTicket == null);
        if (carrying) cargoText.text = economy.HeldTicket != null ? "#" + economy.HeldTicket.id : "×" + economy.Operations.delivery.carrying;
        foreach (var station in stations)
        {
            var t = economy.AtStation(station.location, station.machine);
            if (station.contents != null) station.contents.SetActive(station.location == LaundryLocation.Table && t != null);
            bool working = t != null && (t.stage == LaundryStage.Washing || t.stage == LaundryStage.Drying);
            station.rotationSpeed = StepDrumSpeed(station.rotationSpeed, working, Time.deltaTime);
            if (station.drum != null && station.rotationSpeed > 0) station.drum.Rotate(Vector3.forward, station.rotationSpeed * Time.deltaTime, Space.Self);
        }
        nextAction = -1;
        if (player.CanControl && grab.itemID < 0)
        {
            float closest = 1.25f * 1.25f;
            for (int i = 0; i < stations.Length; i++)
                if (economy.HeldTicket != null || (!HasCargo && economy.AtStation(stations[i].location, stations[i].machine) != null))
                    Consider(stations[i].usePoint.position, i, ref closest);
            Consider(supplyUsePoint.position, 100, ref closest);
            var d = economy.Operations.delivery;
            if (d != null && !d.Complete && now >= d.dueAt + 4) Consider(parcelUsePoint.position, 101, ref closest);
            if (d != null && d.carrying > 0 && !economy.IsSupplyCatalog(d.catalog))
                Consider(stockBoxes[d.catalog - 3].position + Vector3.left * 1.8f, 102, ref closest);
        }
        actionPanel.SetActive(nextAction >= 0 && player.CanControl);
        if (nextAction >= 0)
        {
            actionText.text = ActionCaption();
            if (GameInput.GetKeyDown(KeyCode.E)) { handledFrame = Time.frameCount; UseNearest(now); }
        }
        int second = (int)(now % 1000000);
        if (second != lastSecond) { lastSecond = second; RefreshPanels(); }
    }
    private void Consider(Vector3 p, int action, ref float closest)
    {
        p.y = player.transform.position.y; float distance = (p - player.transform.position).sqrMagnitude;
        if (distance < closest) { closest = distance; nextAction = action; }
    }
    private string ActionCaption()
    {
        if (nextAction == 100) return HasCargo ? "배송 물품 보관" : "세탁용품 보관함";
        if (nextAction == 102) return "배송 의류 보관함에 넣기";
        if (nextAction == 101) return economy.Operations.delivery.opened ? "배송 물품 꺼내기 (최대 3개)" : "배송 박스 열기";
        var s = stations[nextAction]; var t = economy.AtStation(s.location, s.machine);
        if (t != null) return s.title + " · " + StationStatus(s.location, t.stage) + (t.readyAt > LaundryClock.Now ? " " + Mathf.CeilToInt((float)(t.readyAt - LaundryClock.Now)) + "초" : " · 꺼내기");
        if (s.location == LaundryLocation.Table && economy.HeldTicket != null && economy.HeldTicket.stage != LaundryStage.Dry) return "정리대에 임시 보관";
        return s.title + " · 옷 넣기";
    }
    private void UseNearest(double now)
    {
        if (grab.itemID >= 0) { Notice("들고 있는 의류를 원래 보관함에 놓아 주세요."); return; }
        string message = "";
        if (nextAction < 100) { economy.UseStation(stations[nextAction].location, stations[nextAction].machine, now, out message); message = stations[nextAction].title + " · " + message; }
        else if (nextAction == 101)
        {
            var d = economy.Operations.delivery;
            if (!d.opened) message = economy.OpenDelivery(d.id, now) ? "박스를 열었어요. 물품을 꺼내 보관함으로 옮기세요." : "하역이 끝나면 열 수 있어요.";
            else message = economy.TakeDelivery(d.id, now) ? CatalogName(d.catalog) + " 운반 중 · 보관함에 넣어 주세요." : HasCargo ? "정리대에 옷을 놓고 손을 비워 주세요." : "박스에 남은 물품이 없어요.";
        }
        else
        {
            var d = economy.Operations.delivery;
            if (d == null || d.carrying == 0) message = "세제 " + economy.Supply(0) + " / 건조 시트 " + economy.Supply(1) + " / 포장재 " + economy.Supply(2);
            else if ((nextAction == 100 && economy.IsSupplyCatalog(d.catalog)) || (nextAction == 102 && !economy.IsSupplyCatalog(d.catalog)))
                message = economy.StoreDelivery(d.catalog) ? "보관 완료 · 실제 재고가 늘었어요." : "보관할 공간이 부족해요.";
            else message = "의류는 해당 옷 보관함에 넣어 주세요.";
        }
        Notice(message); RefreshPanels();
    }
    private static void Notice(string message) { if (ShopHUD.Current != null) ShopHUD.Current.ShowNotice(message); }
    public bool Accept(NPCUI customer, int clothing, int slot, out string message)
    {
        message = "손을 비운 뒤 의뢰를 접수해 주세요.";
        if (HasCargo || grab.itemID >= 0) return false;
        var t = economy.AcceptLaundry(clothing, 1, slot, LaundryClock.Now);
        if (t == null) return false;
        customer.BindLaundryTicket(t.id); message = "의뢰 #" + t.id + " 접수 · " + ClothingName(clothing) + " 1벌을 세탁기에 넣어 주세요.";
        handledFrame = Time.frameCount; return true;
    }
    public int ReturnTo(NPCUI customer)
    {
        if (!economy.CanReturnLaundry(customer.LaundryTicketId))
        {
            if (economy.HeldTicket != null || grab.itemID >= 0) economy.MarkLaundryMistake(customer.LaundryTicketId);
            return 0;
        }
        var ticket = economy.HeldTicket;
        int bonus = LaundryEvents.Current != null ? LaundryEvents.Current.CompleteOrder(boxes.boxDatas[ticket.clothing].Id,
            (float)(LaundryClock.Now - ticket.acceptedAt), !ticket.wrongAttempt) : 0;
        int amount = economy.ReturnLaundry(customer.LaundryTicketId, bonus);
        if (amount > 0) { player.money = economy.Operations.dailySales; player.completeOrder = economy.Operations.dailyCompleted; handledFrame = Time.frameCount; }
        return amount;
    }
    public string DeliveryStatus()
    {
        var d = economy.Operations.delivery;
        if (d == null) return "배송 없음";
        double now = LaundryClock.Now;
        if (d.Complete) return "배송 완료 · 재고 정리 완료";
        if (now < d.orderedAt + 1) return "주문 접수";
        if (now < d.dueAt) { int seconds = Mathf.Max(0, (int)Math.Ceiling(d.dueAt - now)); return "배송 중 · 도착까지 " + (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"); }
        if (now < d.dueAt + 4) return "트럭 도착 · 하역 중";
        if (now < d.dueAt + 6) return "물품 하역 완료";
        return "재고 정리 대기 · " + d.stored + " / " + d.quantity + (d.carrying > 0 ? " (운반 " + d.carrying + ")" : "");
    }
    public void RefreshPanels()
    {
        if (economy == null || deliveryText == null) return;
        bool visible = player != null && !loop.IsResultOpen && (ShopComputer.Current == null || !ShopComputer.Current.IsOpen) && Time.timeScale > 0;
        var d = economy.Operations.delivery;
        deliveryPanel.SetActive(visible && d != null && (!d.Complete || LaundryClock.Now < Math.Max(d.dueAt + 35, d.completedAt + 4)));
        deliveryText.text = DeliveryStatus() + (d != null ? "\n" + CatalogName(d.catalog) + " ×" + d.quantity : "");
        var text = new StringBuilder(); int active = 0;
        foreach (var t in economy.Operations.tickets)
        {
            if (t.stage == LaundryStage.Returned) continue; active++;
            text.Append("#").Append(t.id).Append("  ").Append(ClothingName(t.clothing)).Append(" ×").Append(t.quantity).Append("  ").Append(StageName(t.stage));
            if (t.readyAt > LaundryClock.Now && t.location != LaundryLocation.Player) text.Append(" ").Append(Mathf.CeilToInt((float)(t.readyAt - LaundryClock.Now))).Append("초");
            text.Append('\n');
        }
        jobsPanel.SetActive(visible && active > 0 && (ShopHUD.Current == null || !ShopHUD.Current.HasCollectionOrder)); jobsText.text = text.ToString().TrimEnd();
        for (int i = 0; i < supplyLabels.Length; i++) supplyLabels[i].text = ShopEconomy.SupplyNames[i] + " " + economy.Supply(i) + "/" + economy.Capacity;
        foreach (var s in stations)
        {
            var t = economy.AtStation(s.location, s.machine);
            s.status.text = s.title + "\n" + (t == null ? "사용 가능" : "#" + t.id + " " + StationStatus(s.location, t.stage) + (t.readyAt > LaundryClock.Now ? " " + Mathf.CeilToInt((float)(t.readyAt - LaundryClock.Now)) : ""));
            if (s.display != null) s.display.text = t != null && s.location == LaundryLocation.Dryer && t.stage == LaundryStage.Wet ? "SUPPLY" : MachineDisplay(t, LaundryClock.Now);
        }
    }
    private void UpdateTruck(double now)
    {
        var d = economy.Operations.delivery;
        float progress = d == null ? 0 : Mathf.Clamp01((float)((now - (d.dueAt - 16)) / 16));
        bool leaving = d != null && now >= d.dueAt + 9;
        if (leaving) progress = Mathf.Clamp01((float)((now - d.dueAt - 9) / 26));
        var route = leaving ? departureRoute : arrivalRoute;
        Vector3 target = PathPoint(route, progress), ahead = PathPoint(route, Mathf.Min(1, progress + .005f));
        Vector3 direction = ahead - target;
        if (direction.sqrMagnitude < .0001f && progress >= 1) direction = route[route.Length - 1].position - route[route.Length - 2].position;
        float travelled = Vector3.Distance(truck.position, target);
        truck.position = target;
        if (travelled < 5 && wheels != null) foreach (var wheel in wheels) wheel.Rotate(Vector3.up, travelled / .45f * Mathf.Rad2Deg, Space.Self);
        if (direction.sqrMagnitude > .0001f) truck.rotation = Quaternion.Slerp(truck.rotation, Quaternion.LookRotation(direction), Time.unscaledDeltaTime * 8);
        float age = d == null ? -1 : (float)(now - d.dueAt);
        // A real tail lift: unfold, lower with the parcel, clear the parcel, raise, then fold.
        float opening = age < 0 || age >= 9 ? 0 : age < 1 ? Mathf.SmoothStep(0, 1, age) : age < 8 ? 1 : Mathf.SmoothStep(1, 0, age - 8);
        float lowering = age < 2 || age >= 8 ? 0 : age < 3 ? Mathf.SmoothStep(0, 1, age - 2) : age < 7 ? 1 : Mathf.SmoothStep(1, 0, age - 7);
        tailgate.localRotation = Quaternion.Euler(-90 * opening, 0, 0);
        tailgate.localPosition = tailgateRestPosition + Vector3.down * (.83f * lowering);
        bool unloaded = d != null && now >= d.dueAt + 1 && !d.Complete;
        parcel.gameObject.SetActive(unloaded);
        if (unloaded)
        {
            Vector3 platform = tailgate.position - truck.forward * .75f + Vector3.up * .04f;
            if (age < 2) parcel.position = Vector3.Lerp(parcelStart.position, platform, Mathf.SmoothStep(0, 1, age - 1));
            else if (age < 3) parcel.position = platform;
            else parcel.position = Vector3.Lerp(platform, parcelEnd.position, Mathf.SmoothStep(0, 1, age - 3));
            closedParcel.SetActive(!d.opened); openParcel.SetActive(d.opened);
            if (parcelLabel != null) parcelLabel.text = CatalogName(d.catalog) + " ×" + d.quantity + "\n남은 수량 " + d.remaining;
            var contents = openParcel.transform.Find("Box contents");
            if (contents != null) contents.gameObject.SetActive(d.remaining > 0);
        }
        if (d != null && now >= d.dueAt && now < d.dueAt + 4 && soundedDelivery != d.id)
        { soundedDelivery = d.id; if (arrivalBell != null) notificationAudio.PlayOneShot(arrivalBell, .4f); Notice("배송 트럭이 도착했어요. 배송 구역에서 물품을 받아 주세요."); }
    }
    private static Vector3 PathPoint(Transform[] points, float t)
    {
        float length = 0; for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1].position, points[i].position);
        float distance = length * t;
        for (int i = 1; i < points.Length; i++) { float segment = Vector3.Distance(points[i - 1].position, points[i].position); if (distance <= segment) return Vector3.Lerp(points[i - 1].position, points[i].position, segment > 0 ? distance / segment : 0); distance -= segment; }
        return points[points.Length - 1].position;
    }
}
