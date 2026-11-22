using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class NPCUI : MonoBehaviour
{
    public enum VisitPurpose { Collection, DropOff }
    public VisitPurpose Purpose { get; private set; }
    public void ConfigureVisit(VisitPurpose purpose) { Purpose = HasPendingLaundry ? VisitPurpose.DropOff : purpose; }
    [SerializeField] private GameObject chatImage;
    [SerializeField] private TextMeshProUGUI chatTMP;
    [SerializeField] private GameObject body;
    [SerializeField] private GameObject laundryBundle;
    [SerializeField] private TMP_Text laundryTicketLabel;
    [SerializeField] private float sizeX;
    [SerializeField] private float sizeY;
    [SerializeField] private float sizeZ;
    [SerializeField] private Animator chatAnimator;
    [SerializeField] private Animator interactionAnim;
    [SerializeField] private GameObject moodFrame;
    [SerializeField] private GameObject buttonFrame;
    [SerializeField] private GameObject chatFrame;
    [SerializeField] private GameObject giveButton;
    [SerializeField] private Animator giveButtonAnim;
    private NPCQueue npcQueue;
    private NPCMovement movement;
    private NPCAppearance npcAppearance;
    [SerializeField] private Sprite[] moodSprite;
    [SerializeField] private Image moodImage;
    [SerializeField] private float moodTimer;
    [SerializeField] private BoxData[] boxDatas;
    private float maxTime = 150f;
    public bool goOut;
    [SerializeField] private Image moodBar;
    private NPCController controller;
    public OrderData[] orderDatas;
    [SerializeField] private bool cheakColl;
    private int orderDataID;
    private BoxManager boxManager;
    private Grab grab;
    public bool orderComplete;
    private NPCExit npcExit;
    private Player player;
    private ShopEconomy economy;
    private AudioSource feedbackAudio;
    private Coroutine feedbackCoroutine;
    private Coroutine revenuePopupCoroutine;
    private TextMeshProUGUI revenuePopup;
    private bool feedbackVisible;
    private static AudioClip successClip;
    private static AudioClip wrongClip;
    [SerializeField, Range(0f, 1f)] private float feedbackVolume = 0.52f;
    private ParticleSystem rewardBurst;
    private static Material rewardMaterial;
    private bool playerNearby;
    private int playerMask;
    private TextMeshProUGUI chatTitle;
    private float orderReadAt;
    private bool wrongAttempt;
    public bool IsWaitingForOrder => Application.isPlaying && controller != null && npcExit != null && controller.enterLine && !orderComplete && !goOut && !npcExit.isExiting;
    public int[] AcceptedClothing => orderDatas[orderDataID].CanID;
    public bool HasReadOrder => cheakColl;
    public float OrderReadTime => orderReadAt;
    public int ExpressSeconds => cheakColl && !wrongAttempt ? Mathf.Max(0, Mathf.CeilToInt((LaundryEvents.Current != null ? LaundryEvents.Current.ExpressDeadline : 25f) - (Time.time - orderReadAt))) : 0;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }
        movement = GetComponentInParent<NPCMovement>();
        npcQueue = GetComponentInParent<NPCQueue>();
        controller = GetComponentInParent<NPCController>();
        boxManager = GetComponentInParent<BoxManager>();
        grab = GameObject.Find("Player").GetComponent<Grab>();
        npcExit = GetComponentInParent<NPCExit>();
        player = GameObject.Find("Player").GetComponent<Player>();
        economy = FindAnyObjectByType<ShopEconomy>();
        playerMask = LayerMask.GetMask("Player");
        npcAppearance = GetComponentInParent<NPCAppearance>();
        feedbackAudio = gameObject.AddComponent<AudioSource>();
        feedbackAudio.playOnAwake = false;
        feedbackAudio.spatialBlend = 0f;
        if (successClip == null) successClip = CreateFeedbackClip("OrderSuccess", 660f, 990f);
        if (wrongClip == null) wrongClip = CreateFeedbackClip("OrderWrong", 330f, 220f);
        CreateRevenuePopup();
        chatTMP.rectTransform.localRotation = Quaternion.identity;
        StyleChatBubble();
        CreateRewardBurst();
    }
    

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (laundryBundle != null) laundryBundle.SetActive(LaundryWorkflow.Current != null && (orderComplete || (Purpose == VisitPurpose.DropOff && !HasPendingLaundry)));
        if (laundryTicketLabel != null)
        {
            laundryTicketLabel.gameObject.SetActive(!goOut && !npcExit.isExiting && !orderComplete);
            if (HasPendingLaundry)
            {
                var ticket = economy.Ticket(laundryTicketId);
                laundryTicketLabel.text = "#" + ticket.id + " · " + LaundryWorkflow.StageName(ticket.stage);
            }
            else laundryTicketLabel.text = Purpose == VisitPurpose.DropOff ? "세탁 접수" : "의류 수령";
        }
        if (goOut || npcExit.isExiting)
        {
            UIActive(false);
            giveButton.SetActive(false);
            chatFrame.SetActive(false);
            return;
        }
        playerNearby = Physics.CheckBox(body.transform.position, new Vector3(sizeX, sizeY, sizeZ), Quaternion.identity, playerMask);
        bool openedChatThisFrame = UIAnim();
        Patience();
        if (cheakColl && !openedChatThisFrame)
        {
            GiveButton();
        }

        if (orderComplete == true)
        {
            UIActive(false);
            chatFrame.SetActive(feedbackVisible);
            moodFrame.SetActive(feedbackVisible);
        }
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        OrderService();
    }

    private string laundryTicketId;
    public string LaundryTicketId => laundryTicketId;
    public bool HasPendingLaundry => economy != null && economy.Ticket(laundryTicketId) != null && economy.Ticket(laundryTicketId).stage != LaundryStage.Returned;
    public void BindLaundryTicket(string id) { laundryTicketId = id; cheakColl = true; Purpose = VisitPurpose.DropOff; }
    private string LaundryMessage()
    {
        var t = economy.Ticket(laundryTicketId);
        if (t == null) return "접수한 옷을 확인해 주세요.";
        if (economy.HeldTicket != null && economy.HeldTicket.id != t.id) return "제 의뢰는 #" + t.id + "예요. 다른 손님의 옷이에요.";
        return "의뢰 #" + t.id + " · " + boxDatas[t.clothing].BoxName + " " + t.quantity + "벌\n" +
            (t.stage == LaundryStage.Ready ? "제 옷을 가져다 주세요." : LaundryWorkflow.StageName(t.stage) + " · 세탁·건조·포장 후 돌려주세요.");
    }

    private void GiveButton()
    {
        if (LaundryWorkflow.Current != null && LaundryWorkflow.Current.HandledThisFrame) return;
        if (LaundryWorkflow.Current != null && !string.IsNullOrEmpty(laundryTicketId))
        {
            bool nearby = playerNearby && player.CanControl && !orderComplete;
            giveButton.SetActive(nearby);
            if (nearby && GameInput.GetKeyDown(KeyCode.E) && !LaundryWorkflow.Current.HandledThisFrame)
            {
                int amount = LaundryWorkflow.Current.ReturnTo(this);
                if (amount > 0)
                {
                    orderComplete = true; player.grabClothes = false;
                    var ticket = economy.Ticket(laundryTicketId);
                    npcAppearance.clothesID = boxDatas[ticket.clothing].Id;
                    StartCoroutine(npcExit.ExitCoroutine(2f));
                    giveButton.SetActive(false); moodImage.sprite = moodSprite[0];
                    ShowFeedback("깨끗하게 세탁해 주셔서 고마워요!", true);
                    ShowRevenuePopup(amount); PlayRewardBurst();
                }
                else ShowFeedback(LaundryMessage(), false);
            }
            return;
        }
        if (orderComplete || !player.CanControl)
            return;

        if (cheakColl)
        {
            if (playerNearby && cheakColl)
            {
                giveButton.SetActive(true);
                SetHover(giveButtonAnim, true);

                if (GameInput.GetKeyDown(KeyCode.E) && cheakColl)
                {
                    if (LaundryWorkflow.Current != null) LaundryWorkflow.Current.ClaimInteraction();
                    if (grab.itemID < 0 || grab.itemID >= boxDatas.Length)
                    {
                        if (ShopHUD.Current != null) ShopHUD.Current.ShowOrder(this, orderDatas[orderDataID]);
                        return;
                    }

                    for (int i = 0; i < orderDatas[orderDataID].CanID.Length; i++)
                    {
                        if (boxDatas[grab.itemID].Id == orderDatas[orderDataID].CanID[i])
                        {
                            if (economy != null && !economy.CanCompleteCollection(grab.itemID)) return;
                            int bonus = LaundryEvents.Current != null
                                ? LaundryEvents.Current.CompleteOrder(boxDatas[grab.itemID].Id, Time.time - orderReadAt, !wrongAttempt) : 0;
                            int amount = economy != null ? economy.CompleteCollection(grab.itemID, bonus) : 5000 + bonus;
                            if (amount == 0) return;
                            npcAppearance.clothesID = boxDatas[grab.itemID].Id;
                            StartCoroutine(npcExit.ExitCoroutine(2f));
                            orderComplete = true;
                            player.grabClothes = false;
                            grab.SetID(-1);
                            if (economy != null && LaundryWorkflow.Current != null)
                            { player.completeOrder = economy.Operations.dailyCompleted; player.money = economy.Operations.dailySales; }
                            else { player.completeOrder += 1f; player.money += amount; }
                            giveButton.SetActive(false);
                            moodImage.sprite = moodSprite[0];
                            ShowFeedback(bonus > 0 ? "고마워요! 특별 보너스 +" + bonus.ToString("N0") : "고마워요!", true);
                            ShowRevenuePopup(5000f + bonus);
                            PlayRewardBurst();
                            break;
                        }
                    }
                    if (!orderComplete)
                    {
                        wrongAttempt = true;
                        if (LaundryEvents.Current != null) LaundryEvents.Current.MissOrder();
                        ShowFeedback("주문과 맞지 않아요. 다시 골라 주세요.", false);
                    }
                }
            }
            else if (!playerNearby && cheakColl)
            {
                giveButton.SetActive(false);
                SetHover(giveButtonAnim, false);
            }
        }
    }

    private bool UIAnim()
    {
        if (LaundryWorkflow.Current != null && LaundryWorkflow.Current.HandledThisFrame) return false;
        if (player.CanControl && GameInput.GetKeyDown(KeyCode.E) && !cheakColl && controller.enterLine && playerNearby)
        {
            if (LaundryWorkflow.Current != null && Purpose == VisitPurpose.DropOff)
            {
                if (LaundryWorkflow.Current.HandledThisFrame) return false;
                int clothing = System.Array.FindIndex(boxDatas, b => System.Array.IndexOf(orderDatas[orderDataID].CanID, b.Id) >= 0);
                string message;
                if (!LaundryWorkflow.Current.Accept(this, clothing, npcQueue.lineTarget, out message)) { ShowFeedback(message, false); return true; }
                chatTMP.text = FormatBubbleText(message);
                if (ShopHUD.Current != null) ShopHUD.Current.ShowNotice(message);
            }
            else if (LaundryWorkflow.Current != null) LaundryWorkflow.Current.ClaimInteraction();
            buttonFrame.SetActive(false);
            chatFrame.SetActive(true);
            cheakColl = true;
            orderReadAt = Time.time;
            if (ShopHUD.Current != null) ShopHUD.Current.ShowOrder(this, orderDatas[orderDataID]);

            chatAnimator.Play("Chat", 0, 0f);
            return true;
        }

        if (controller.enterLine)
        {

            if (playerNearby && npcQueue.isQueuing)
            {
                UIActive(true);
                SetHover(interactionAnim, true);

            }
            else
            {
                UIActive(false);
                SetHover(interactionAnim, false);
            }
        }

        return false;
    }

    private void OrderService()
    {
        if (HasPendingLaundry)
        {
            chatTMP.text = FormatBubbleText(LaundryMessage());
            return;
        }
        var manager = FindAnyObjectByType<GameManager>();
        int dataIndex = manager != null ? manager.NextOrderIndex(orderDatas.Length) : Random.Range(0, orderDatas.Length);
        controller.orderDataID = orderDatas[dataIndex].Id;
        chatTMP.text = FormatBubbleText(RequestMessage(dataIndex));
        if (chatTitle != null) chatTitle.text = "손님 주문";

        orderDataID = dataIndex;
    }

    private string RequestMessage(int index)
    {
        if (LaundryWorkflow.Current == null) return orderDatas[index].Chat;
        int clothing = System.Array.FindIndex(boxDatas, b => System.Array.IndexOf(orderDatas[index].CanID, b.Id) >= 0);
        string name = clothing >= 0 ? boxDatas[clothing].BoxName : "옷";
        return Purpose == VisitPurpose.DropOff ? name + " 세탁을 맡기러 왔어요." : "맡겨 둔 옷을 찾으러 왔어요.\n" + orderDatas[index].Chat;
    }

    private void ShowFeedback(string message, bool success)
    {
        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        chatTMP.text = FormatBubbleText(message);
        if (chatTitle != null) chatTitle.text = success ? "주문 완료" : "옷을 다시 골라 주세요";
        if (ShopHUD.Current != null) ShopHUD.Current.ShowNotice(message);
        chatFrame.SetActive(true);
        feedbackVisible = true;
        feedbackAudio.PlayOneShot(success ? successClip : wrongClip, feedbackVolume);
        feedbackCoroutine = StartCoroutine(HideFeedback());
    }

    private void CreateRevenuePopup()
    {
        Canvas worldCanvas = GetComponentInChildren<Canvas>(true);
        revenuePopup = Instantiate(chatTMP, worldCanvas.transform);
        revenuePopup.name = "RevenuePopup";
        revenuePopup.text = "";
        revenuePopup.enableAutoSizing = false;
        revenuePopup.margin = Vector4.zero;
        revenuePopup.textWrappingMode = TextWrappingModes.NoWrap;
        revenuePopup.overflowMode = TextOverflowModes.Overflow;
        revenuePopup.fontSize = 1f;
        revenuePopup.fontStyle = FontStyles.Bold;
        revenuePopup.alignment = TextAlignmentOptions.Center;
        revenuePopup.raycastTarget = false;
        revenuePopup.color = Color.white;
        revenuePopup.enableVertexGradient = true;
        revenuePopup.colorGradient = new VertexGradient(
            new Color(1f, 0.98f, 0.69f), new Color(1f, 0.91f, 0.46f),
            new Color(1f, 0.61f, 0.29f), new Color(1f, 0.72f, 0.35f));
        RectTransform popupRect = revenuePopup.rectTransform;
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.sizeDelta = new Vector2(3f, 1f);
        popupRect.localScale = Vector3.one;
        revenuePopup.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.16f);
        revenuePopup.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.18f, 0.20f, 0.26f, 1f));
        revenuePopup.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
        revenuePopup.fontMaterial.DisableKeyword("UNDERLAY_ON");
        // The popup follows the camera but keeps its own scale animation.
        if (revenuePopup.GetComponent<BillboardUi>() == null)
            revenuePopup.gameObject.AddComponent<BillboardUi>();
        revenuePopup.gameObject.SetActive(false);
    }

    private void ShowRevenuePopup(float amount)
    {
        if (revenuePopupCoroutine != null)
            StopCoroutine(revenuePopupCoroutine);

        revenuePopup.text = $"+{amount:N0}";
        revenuePopup.alpha = 1f;
        revenuePopup.rectTransform.anchoredPosition = new Vector2(-2.15f, 1.8f);
        revenuePopup.rectTransform.localScale = Vector3.one;
        revenuePopup.gameObject.SetActive(true);
        revenuePopupCoroutine = StartCoroutine(AnimateRevenuePopup());
    }

    private IEnumerator AnimateRevenuePopup()
    {
        RectTransform popupRect = revenuePopup.rectTransform;
        Vector2 startPosition = new Vector2(-2.15f, 1.8f);
        float duration = 1.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float eased = 1f - (1f - progress) * (1f - progress);
            popupRect.anchoredPosition = startPosition + Vector2.up * (0.65f * eased);
            popupRect.localScale = Vector3.one * (0.32f + 0.08f * Mathf.Sin(progress * Mathf.PI));
            revenuePopup.alpha = Mathf.Clamp01((1f - progress) * 2f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        revenuePopup.gameObject.SetActive(false);
        revenuePopupCoroutine = null;
    }

    private IEnumerator HideFeedback()
    {
        yield return new WaitForSeconds(1.3f);
        feedbackVisible = false;
        chatFrame.SetActive(false);
        if (!orderComplete)
            chatTMP.text = FormatBubbleText(HasPendingLaundry ? LaundryMessage() : RequestMessage(orderDataID));
        feedbackCoroutine = null;
    }

    private static string FormatBubbleText(string message)
    {
        // Break at spaces so a short Korean ending is not stranded on its own line.
        return "<nobr>" + message.Replace("\\n", "\n").Replace(" ", "</nobr> <nobr>").Replace("\n", "</nobr>\n<nobr>") + "</nobr>";
    }

    private static AudioClip CreateFeedbackClip(string clipName, float startPitch, float endPitch)
    {
        const int sampleRate = 22050;
        int sampleCount = Mathf.RoundToInt(sampleRate * 0.16f);
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = (float)i / sampleCount;
            float pitch = Mathf.Lerp(startPitch, endPitch, progress);
            phase += pitch * 2f * Mathf.PI / sampleRate;
            float fadeIn = Mathf.Clamp01(i / (sampleRate * 0.005f));
            float fadeOut = Mathf.Clamp01((sampleCount - i) / (sampleRate * 0.005f));
            samples[i] = Mathf.Sin(phase) * (1f - progress) * fadeIn * fadeOut;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void UIActive(bool active)
    {

        moodFrame.SetActive(active);
        buttonFrame.SetActive(active && !cheakColl);
    }

    private void Patience()
    {
        if (HasPendingLaundry)
        {
            var ticket = economy.Ticket(laundryTicketId);
            float wait = (float)(LaundryClock.Now - ticket.acceptedAt);
            moodBar.fillAmount = Mathf.Clamp01(wait / 240f);
            moodImage.sprite = moodSprite[wait > 180 ? 2 : wait > 90 ? 1 : 0];
            return;
        }
        if (!movement.inLine || !controller.enterLine || orderComplete || goOut || npcExit.isExiting)
            return;

        moodTimer += Time.deltaTime;
        moodBar.fillAmount = Mathf.Clamp01(moodTimer / maxTime);

        if (moodTimer >= 100f)
        {
            moodImage.sprite = moodSprite[2];
        }
        else if (moodTimer >= 50f)
        {
            moodImage.sprite = moodSprite[1];
        }
        else
        {
            moodImage.sprite = moodSprite[0];
        }

        if (moodTimer >= maxTime)
        {
            goOut = true;
            if (LaundryEvents.Current != null) LaundryEvents.Current.MissOrder();
            player.fallOrder += 1f;
        }

    }

    private static void SetHover(Animator animator, bool active)
    {
        if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            animator.SetBool("bHoverLoop", active);
    }

    private void StyleChatBubble()
    {
        Transform mainFrame = chatFrame.transform.Find("MainFrame");
        if (mainFrame == null) return;
        var background = mainFrame.GetComponent<Image>();
        if (background != null) background.enabled = false;
        var bubble = chatFrame.GetComponent<Image>();
        if (bubble != null)
        {
            bubble.sprite = Resources.Load<Sprite>("LaundryPixel/ChatBubble");
            bubble.type = Image.Type.Simple; bubble.color = Color.white;
            bubble.rectTransform.sizeDelta = new Vector2(3f, 1.2f);
        }
        StylePrompt(buttonFrame);
        StylePrompt(giveButton);

        RectTransform frame = (RectTransform)mainFrame;
        frame.sizeDelta = new Vector2(2.7f, 1f);
        frame.anchoredPosition = new Vector2(0, .08f);
        Transform existingBand = frame.Find("Laundry order header");
        var bandObject = existingBand != null ? existingBand.gameObject : new GameObject("Laundry order header", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var band = bandObject.GetComponent<Image>();
        band.rectTransform.SetParent(frame, false);
        band.rectTransform.anchorMin = band.rectTransform.anchorMax = band.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        band.rectTransform.anchoredPosition = new Vector2(0, 0.46f);
        band.rectTransform.sizeDelta = new Vector2(2.6f, 0.22f);
        LaundrySkin.Surface(band, LaundrySkin.Ribbon, LaundrySkin.Teal);
        band.raycastTarget = false;
        // The screen ticket already has a heading; keep the world bubble clear of the timer and the customer's head.
        bandObject.SetActive(false);

        Transform existingTitle = frame.Find("Laundry order title");
        var titleObject = existingTitle != null ? existingTitle.gameObject : new GameObject("Laundry order title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        chatTitle = titleObject.GetComponent<TextMeshProUGUI>();
        chatTitle.transform.SetParent(frame, false);
        chatTitle.rectTransform.anchorMin = chatTitle.rectTransform.anchorMax = chatTitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        chatTitle.rectTransform.anchoredPosition = new Vector2(0, 0.46f);
        chatTitle.rectTransform.sizeDelta = new Vector2(2.4f, 0.18f);
        LaundrySkin.SetType(chatTitle, chatTMP.font);
        chatTitle.text = "손님 주문";
        chatTitle.fontSize = 0.17f;
        chatTitle.alignment = TextAlignmentOptions.Center;
        chatTitle.color = Color.white;
        chatTitle.fontStyle = FontStyles.Bold;
        chatTitle.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        chatTitle.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
        chatTitle.raycastTarget = false;
        titleObject.SetActive(false);

        RectTransform message = chatTMP.rectTransform;
        message.SetAsLastSibling();
        message.anchorMin = message.anchorMax = message.pivot = new Vector2(0.5f, 0.5f);
        message.anchoredPosition = Vector2.zero;
        message.sizeDelta = new Vector2(2.42f, 0.86f);
        LaundrySkin.SetType(chatTMP, chatTMP.font);
        chatTMP.fontSize = 0.19f;
        chatTMP.enableAutoSizing = true;
        chatTMP.fontSizeMin = 0.17f;
        chatTMP.fontSizeMax = 0.19f;
        chatTMP.fontStyle = FontStyles.Bold;
        chatTMP.color = Color.white;
        chatTMP.alignment = TextAlignmentOptions.Center;
        chatTMP.margin = new Vector4(0.04f, 0.01f, 0.04f, 0.01f);
        chatTMP.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        chatTMP.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.45f);
        chatTMP.fontMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
        chatTMP.raycastTarget = false;
    }

    private static void StylePrompt(GameObject prompt)
    {
        LaundrySkin.StylePrompt(prompt);
    }

    private void CreateRewardBurst()
    {
        if (rewardMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            rewardMaterial = new Material(shader) { name = "Laundry reward sparkles" };
        }
        var burstObject = new GameObject("Reward Sparkles");
        burstObject.transform.SetParent(transform, false);
        rewardBurst = burstObject.AddComponent<ParticleSystem>();
        rewardBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = rewardBurst.main;
        main.playOnAwake = false; main.loop = false; main.startLifetime = 0.6f;
        main.startSize = 0.065f; main.maxParticles = 12; main.gravityModifier = 0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = rewardBurst.emission; emission.enabled = false;
        var shape = rewardBurst.shape; shape.enabled = false;
        var size = rewardBurst.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
        var alpha = new Gradient();
        alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        var color = rewardBurst.colorOverLifetime; color.enabled = true; color.color = alpha;
        rewardBurst.GetComponent<ParticleSystemRenderer>().sharedMaterial = rewardMaterial;
    }

    private void PlayRewardBurst()
    {
        if (rewardBurst == null) return;
        rewardBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        rewardBurst.Play();
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            var emit = new ParticleSystem.EmitParams();
            emit.position = body.transform.position + Vector3.up * 0.8f;
            emit.velocity = new Vector3(Mathf.Cos(angle), 0.55f + 0.3f * Mathf.Sin(angle), Mathf.Sin(angle)) * 0.8f;
            emit.startColor = i % 2 == 0 ? new Color32(241, 181, 72, 255) : new Color32(102, 205, 184, 255);
            rewardBurst.Emit(emit, 1);
        }
    }
}


