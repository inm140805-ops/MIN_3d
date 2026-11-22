using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A small layer around the existing HUD. The original menu, guide and result buttons stay wired.
public class ShopHUD : MonoBehaviour
{
    public static ShopHUD Current { get; private set; }
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text dayText;
    private Player player;
    private Grab grab;
    private CoreLoopManager loop;
    private GUIManager gui;
    private BoxManager boxes;
    [SerializeField] private TMP_Text statusText, heldText, ticketText;
    [SerializeField] private Image heldIcon;
    [SerializeField] private RectTransform progress;
    [SerializeField] private GameObject ticket, root;
    private NPCUI selectedOrder;
    public bool HasCollectionOrder => selectedOrder != null && selectedOrder.Purpose == NPCUI.VisitPurpose.Collection && !selectedOrder.orderComplete && !selectedOrder.goOut;
    private string orderBody;
    private float displayedProgress, noticeUntil;
    private string notice;
    private int previousItem = -1;
    private AudioSource audioSource;
    private AudioClip pickupClip;
    private AudioClip offerClip;
    private LaundryEvents.Offer lastOffer;
    private Vector3 statusScale;
    private float offerPulse;
    private Vector3 moneyScale;
    private float previousMoney, moneyPulse;
    private static readonly Color Ink = LaundrySkin.Ink;
    private static readonly Color Mint = LaundrySkin.Teal;
    private static readonly Color Gold = new Color32(241, 181, 72, 255);
    private static readonly Color Coral = new Color32(186, 73, 73, 255);

    private void Awake()
    {
        player = FindAnyObjectByType<Player>();
        if (player != null) grab = player.GetComponent<Grab>();
        loop = FindAnyObjectByType<CoreLoopManager>();
        gui = GetComponent<GUIManager>();
        boxes = FindAnyObjectByType<BoxManager>();
        if (!root || !moneyText || !timerText || !dayText || !ticket || !heldIcon || !progress || !statusText || !ticketText || !heldText || !grab || !loop || !gui || !boxes)
        {
            enabled = false;
            return;
        }
        Current = this;
        moneyScale = moneyText.transform.localScale;
        statusScale = statusText.transform.localScale;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0f;
        pickupClip = CreatePickupClip();
        offerClip = CreateOfferClip();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        bool visible = !loop.IsResultOpen && !gui.esc && (ShopComputer.Current == null || !ShopComputer.Current.IsOpen);
        root.SetActive(visible);
        // Existing labels can be hidden by their original parent until opening; the panels own them now.
        if (selectedOrder == null || selectedOrder.Purpose == NPCUI.VisitPurpose.DropOff || selectedOrder.orderComplete || selectedOrder.goOut || !loop.isOpen)
        {
            ticket.SetActive(false);
            selectedOrder = null;
        }
        else
        {
            ticket.SetActive(true);
            bool express = LaundryEvents.Current != null && LaundryEvents.Current.ActiveOffer == LaundryEvents.Offer.Express;
            ticketText.text = orderBody + (express ? "\n<size=85%>" +
                (selectedOrder.ExpressSeconds > 0 ? "추가 보상 기회 · " + selectedOrder.ExpressSeconds + "초" : "일반 보상 +5,000") + "</size>" : "");
        }
        if (grab.itemID != previousItem)
        {
            if (grab.itemID >= 0 && grab.itemID < boxes.boxDatas.Length)
            {
                ShowNotice(boxes.boxDatas[grab.itemID].BoxName + "를 집었어요");
                audioSource.PlayOneShot(pickupClip, 0.24f);
            }
            previousItem = grab.itemID;
        }
        heldText.text = grab.itemID >= 0 && grab.itemID < boxes.boxDatas.Length ? boxes.boxDatas[grab.itemID].BoxName : "빈손";
        if (LaundryWorkflow.Current != null)
        {
            bool cargo = LaundryWorkflow.Current.HasCargo;
            if (cargo) heldText.text = LaundryWorkflow.Current.HeldDescription;
            heldText.fontSize = cargo ? 28 : 36;
        }
        bool hasItem = grab.itemID >= 0 && grab.itemID < boxes.boxDatas.Length;
        heldIcon.gameObject.SetActive(hasItem);
        if (hasItem) heldIcon.sprite = boxes.boxDatas[grab.itemID].Icon;
        heldText.rectTransform.sizeDelta = new Vector2(hasItem ? 350 : 470, 60);
        float target = loop.needMoney > 0 ? Mathf.Clamp01(player.money / loop.needMoney) : 0;
        displayedProgress = Mathf.MoveTowards(displayedProgress, target, Time.unscaledDeltaTime * 2f);
        progress.sizeDelta = new Vector2(400f * displayedProgress, 8f);
        progress.GetComponent<UnityEngine.UI.Image>().color = target >= 1 ? Gold : new Color32(188, 224, 182, 255);
        if (player.money > previousMoney) moneyPulse = 0.3f;
        previousMoney = player.money;
        moneyPulse = Mathf.Max(0, moneyPulse - Time.unscaledDeltaTime);
        moneyText.transform.localScale = moneyScale * (1f + 0.07f * Mathf.Sin(moneyPulse / 0.3f * Mathf.PI));
        timerText.color = loop.isOpen && loop.SecondsRemaining <= 30 ? Coral : Color.white;
        dayText.text = "DAY " + loop.DayNumber.ToString("0");
        var activeOffer = LaundryEvents.Current != null ? LaundryEvents.Current.ActiveOffer : LaundryEvents.Offer.None;
        if (activeOffer != lastOffer && activeOffer != LaundryEvents.Offer.None)
        {
            offerPulse = 0.4f;
            audioSource.PlayOneShot(offerClip, 0.28f);
        }
        lastOffer = activeOffer;
        offerPulse = Mathf.Max(0f, offerPulse - Time.deltaTime);
        statusText.transform.localScale = statusScale * (1f + 0.06f * Mathf.Sin(offerPulse / 0.4f * Mathf.PI));
        statusText.text = Time.unscaledTime < noticeUntil ? notice : !loop.isOpen ? "영업 준비" :
            LaundryEvents.Current != null && LaundryEvents.Current.Status.Length > 0 ? LaundryEvents.Current.Status :
            target >= 1 ? "오늘 목표 달성 · 마감 가능" :
            loop.SecondsRemaining <= 30 ? "마감이 다가와요 · 기다리는 손님부터 확인하세요" :
            "";
    }

    public void ShowOrder(NPCUI owner, OrderData order)
    {
        if (owner.Purpose == NPCUI.VisitPurpose.DropOff) { selectedOrder = null; ticket.SetActive(false); return; }
        selectedOrder = owner;
        orderBody = order.Chat.Replace("\\n", "\n");
        ticketText.text = orderBody;
        ticket.SetActive(true);
    }
    public void ShowNotice(string message) { notice = message; noticeUntil = Time.unscaledTime + 2f; }
    private void OnDestroy()
    {
        if (Current == this) Current = null;
        if (pickupClip != null) Destroy(pickupClip);
        if (offerClip != null) Destroy(offerClip);
    }

    private AudioClip CreateOfferClip()
    {
        const int rate = 22050;
        int count = (int)(rate * 0.36f);
        var samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float time = (float)i / rate;
            float noteTime = time < 0.18f ? time : time - 0.18f;
            float frequency = time < 0.18f ? 740f : 988f;
            float envelope = Mathf.Clamp01(noteTime / 0.008f) * Mathf.Clamp01((0.16f - noteTime) / 0.08f);
            samples[i] = Mathf.Sin(noteTime * frequency * Mathf.PI * 2f) * envelope * 0.65f;
        }
        var clip = AudioClip.Create("Shop offer bell", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreatePickupClip()
    {
        const int rate = 22050; int count = (int)(rate * 0.09f); var samples = new float[count]; float phase = 0;
        for (int i = 0; i < count; i++) {
            float t = (float)i / count; phase += Mathf.Lerp(740, 1100, t) * 2 * Mathf.PI / rate;
            samples[i] = Mathf.Sin(phase) * (1 - t) * Mathf.Clamp01(i / (rate * 0.005f)) * Mathf.Clamp01((count - i) / (rate * 0.005f));
        }
        var clip = AudioClip.Create("Clothing pickup", count, 1, rate, false); clip.SetData(samples, 0); return clip;
    }
}
