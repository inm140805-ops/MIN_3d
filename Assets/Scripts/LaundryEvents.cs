using UnityEngine;
using System.Collections.Generic;

// One shop-wide offer at a time. Offers only add rewards to valid orders.
public class LaundryEvents : MonoBehaviour
{
    public enum Offer { None, Express, Popular, Careful, Closing, Variety }
    private static readonly Offer[] rotation = { Offer.Express, Offer.Popular, Offer.Careful, Offer.Variety };
    private readonly HashSet<int> deliveredStyles = new HashSet<int>();
    public static LaundryEvents Current { get; private set; }
    [SerializeField] private float firstOfferDelay = 30f;
    [SerializeField] private float warningDuration = 8f;
    [SerializeField] private float offerDuration = 36f;
    [SerializeField] private float restDuration = 26f;
    [SerializeField] private int dailyBonusLimit = 10000;
    [SerializeField] private float expressDeadline = 25f;
    public float ExpressDeadline => expressDeadline;
    private CoreLoopManager loop;
    private BoxManager boxes;
    private Player player;
    private Offer offer;
    private Offer previousOffer;
    private float phaseRemaining;
    private float activeDuration;
    private bool closingOfferUsed;
    private bool wasOpen, warning;
    private int popularId = -1, cleanOrders, bonusEarned, lastSeconds = -1;
    private string popularName, status = "";
    public Offer ActiveOffer => warning ? Offer.None : offer;
    public int BonusEarned => bonusEarned;
    public string Status => status;

    private void Awake()
    {
        Current = this;
        loop = GetComponent<CoreLoopManager>();
        boxes = FindAnyObjectByType<BoxManager>();
        player = FindAnyObjectByType<Player>();
        activeDuration = offerDuration;
    }

    private void OnEnable() { Current = this; }

    private void Update()
    {
        bool open = loop != null && loop.isOpen && !loop.IsResultOpen;
        if (!open)
        {
            if (wasOpen) ClearOffer();
            wasOpen = false;
            return;
        }
        if (!wasOpen)
        {
            bonusEarned = 0;
            closingOfferUsed = false;
            activeDuration = offerDuration;
            phaseRemaining = firstOfferDelay;
            ClearOffer();
            wasOpen = true;
        }
        if (Time.timeScale == 0f) return;
        TryStartClosingOffer();
        phaseRemaining -= Time.deltaTime;
        if (phaseRemaining <= 0f)
        {
            if (offer == Offer.None)
            {
                // Leave enough time to understand and use the whole offer.
                if (loop.SecondsRemaining < warningDuration + offerDuration + 5f || bonusEarned >= dailyBonusLimit)
                    return;
                BeginOffer();
            }
            else if (warning)
            {
                warning = false;
                phaseRemaining = activeDuration;
            }
            else
            {
                ClearOffer();
                phaseRemaining = restDuration;
            }
        }
        int seconds = Mathf.CeilToInt(phaseRemaining);
        if (seconds != lastSeconds)
        {
            lastSeconds = seconds;
            RefreshStatus();
        }
    }

    private void BeginOffer()
    {
        // Rotate after a random opening offer so consecutive offers never repeat.
        int previousIndex = System.Array.IndexOf(rotation, previousOffer);
        offer = rotation[previousIndex < 0 ? Random.Range(0, rotation.Length) : (previousIndex + 1) % rotation.Length];
        deliveredStyles.Clear();
        popularId = -1;
        if (offer == Offer.Popular)
        {
            NPCUI[] customers = FindObjectsByType<NPCUI>(FindObjectsSortMode.None);
            foreach (NPCUI customer in customers)
            {
                if (!customer.IsWaitingForOrder) continue;
                int[] accepted = customer.AcceptedClothing;
                if (accepted.Length == 0) continue;
                popularId = accepted[Random.Range(0, accepted.Length)];
                break;
            }
            if (popularId < 0) offer = previousOffer == Offer.Express ? Offer.Careful : Offer.Express;
            else
            {
                popularName = "인기 의류";
                if (boxes != null)
                    foreach (BoxData data in boxes.boxDatas)
                        if (data != null && data.Id == popularId) { popularName = data.BoxName; break; }
            }
        }
        previousOffer = offer;
        activeDuration = offerDuration;
        cleanOrders = 0;
        warning = true;
        phaseRemaining = warningDuration;
        lastSeconds = -1;
        RefreshStatus();
    }

    private bool TryStartClosingOffer()
    {
        if (closingOfferUsed || offer != Offer.None || loop == null || !loop.isOpen || loop.IsResultOpen ||
            player == null || player.money >= loop.needMoney || bonusEarned >= dailyBonusLimit ||
            loop.SecondsRemaining > 45f || loop.SecondsRemaining < 25f)
            return false;
        closingOfferUsed = true;
        offer = Offer.Closing;
        warning = true;
        phaseRemaining = 5f;
        activeDuration = Mathf.Min(30f, loop.SecondsRemaining - 6f);
        lastSeconds = -1;
        RefreshStatus();
        return true;
    }

    private void RefreshStatus()
    {
        if (offer == Offer.None) { status = ""; return; }
        string timer = Mathf.Max(0, Mathf.CeilToInt(phaseRemaining)).ToString();
        string heading = warning ? timer + "초 뒤 시작" : timer + "초 남음";
        int remainingBonus = Mathf.Max(0, dailyBonusLimit - bonusEarned);
        switch (offer)
        {
            case Offer.Express:
                status = "빠른 응대 · " + heading + "\n접수 후 " + expressDeadline.ToString("0") + "초 내 첫 시도 성공 +" + Mathf.Min(1500, remainingBonus).ToString("N0");
                break;
            case Offer.Popular:
                status = popularName + " 인기 시간 · " + heading + "\n해당 옷을 원하는 손님에게 전달하면 +" + Mathf.Min(1500, remainingBonus).ToString("N0");
                break;
            case Offer.Careful:
                status = "정확한 응대 · " + heading + "\n실수 없이 3명 응대 " + cleanOrders + "/3 · +" + Mathf.Min(2500, remainingBonus).ToString("N0");
                break;
            case Offer.Closing:
                status = "마감 특급 · " + heading + "\n마지막 기회! 주문에 맞는 옷 전달 +" + Mathf.Min(2000, remainingBonus).ToString("N0");
                break;
            case Offer.Variety:
                status = "코디 챌린지 · " + heading + "\n서로 다른 옷 " + deliveredStyles.Count + "/3종 전달 · +" + Mathf.Min(3000, remainingBonus).ToString("N0");
                break;
        }
    }

    public int CompleteOrder(int clothingId, float responseSeconds, bool firstTry)
    {
        if (!wasOpen || loop == null || !loop.isOpen || loop.IsResultOpen || Time.timeScale == 0f ||
            phaseRemaining <= 0f || warning || offer == Offer.None)
            return 0;
        int bonus = 0;
        switch (offer)
        {
            case Offer.Express:
                if (responseSeconds >= 0f && responseSeconds <= expressDeadline && firstTry) bonus = 1500;
                break;
            case Offer.Popular:
                if (clothingId == popularId) bonus = 1500;
                break;
            case Offer.Careful:
                cleanOrders = firstTry ? cleanOrders + 1 : 0;
                if (cleanOrders >= 3) { bonus = 2500; cleanOrders = 0; }
                RefreshStatus();
                break;
            case Offer.Closing:
                bonus = 2000;
                break;
            case Offer.Variety:
                deliveredStyles.Add(clothingId);
                if (deliveredStyles.Count >= 3) { bonus = 3000; deliveredStyles.Clear(); }
                break;
        }
        bonus = Mathf.Clamp(bonus, 0, dailyBonusLimit - bonusEarned);
        bonusEarned += bonus;
        if (bonusEarned >= dailyBonusLimit) ClearOffer();
        else RefreshStatus();
        return bonus;
    }

    public void MissOrder()
    {
        if (ActiveOffer != Offer.Careful) return;
        cleanOrders = 0;
        RefreshStatus();
    }

    private void ClearOffer()
    {
        offer = Offer.None;
        warning = false;
        cleanOrders = 0;
        deliveredStyles.Clear();
        lastSeconds = -1;
        status = "";
    }

    private void OnDisable()
    {
        ClearOffer();
        wasOpen = false;
        if (Current == this) Current = null;
    }
}
