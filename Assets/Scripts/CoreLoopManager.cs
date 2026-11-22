using UnityEngine;
using UnityEngine.UIElements;
using TMPro;
using System.Collections;
using UnityEngine.UI;

public class CoreLoopManager : MonoBehaviour
{
    [SerializeField] private Transform openOBJ;
    [SerializeField] private float openOBJsizeX;
    [SerializeField] private float openOBJsizeY;
    [SerializeField] private float openOBJsizeZ;
    [SerializeField] private GameObject openButton;
    [SerializeField] private GameObject closeButton;
    [SerializeField] private TextMeshProUGUI timer;
    [SerializeField] private GameObject timerOBJ;
    [SerializeField] private GameObject wearingText;
    [SerializeField] public GameObject reportFrame;
    [SerializeField] private TextMeshProUGUI completeOrders;
    [SerializeField] private TextMeshProUGUI fallOrders;
    [SerializeField] private TextMeshProUGUI TotalRevenue;
    [SerializeField] private TextMeshProUGUI result;
    [SerializeField] private GameObject winButtons;
    [SerializeField] private GameObject fallButtons;
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private GameObject dayOBJ;

    private bool timerEnd;
    private Player player;
    private Animator openAnimator;
    private Animator closeAnimator;
    private int playerMask;
    public bool isOpen;
    private float dayCount = 1f;
    [SerializeField] private float dayDuration = 180f;
    private float totalTime;
    private bool daySucceeded;
    private float coundtFloat;
    public bool timerStart;
    public float needMoney = 50000f;
    public float SecondsRemaining => totalTime;
    public float DayDuration => dayDuration;
    public float DayNumber => dayCount;
    public bool IsResultOpen => reportFrame != null && reportFrame.activeSelf;

    private void Awake()
    {
        if (openButton != null) openAnimator = openButton.GetComponent<Animator>();
        if (closeButton != null) closeAnimator = closeButton.GetComponent<Animator>();
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null) player = playerObject.GetComponent<Player>();
        playerMask = LayerMask.GetMask("PlayerBody");
        totalTime = dayDuration;
        UpdateTimerText();
    }

    private void Update()
    {
        if (openOBJ == null || openButton == null || closeButton == null || player == null || reportFrame == null)
            return;
        openFounction();

        if (isOpen && !timerStart)
        {
            timerStart = true;
            StartCoroutine(TimerCoroutine());
        }

        if (timerEnd && timerStart)
        {
            gameEndFuntion(false);
        }

        if (dayText != null) dayText.text = "Day " + dayCount;

        
    }

    private void NeedMoneyCount()
    {
        // Each successful day adds one order, independent of optional extra earnings.
        needMoney = Mathf.Min(needMoney + 5000f, LaundryWorkflow.Current != null ? 40000f : 75000f);
        player.money = 0;
    }

    private void OnDrawGizmos()
    {
        if (openOBJ == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            openOBJ.position,
            new Vector3(openOBJsizeX * 2, openOBJsizeY * 2, openOBJsizeZ * 2)
        );
    }

    

    IEnumerator TimerCoroutine()
    {
        while (totalTime > 0 && isOpen)
        {
            totalTime = Mathf.Max(0f, totalTime - Time.deltaTime);
            UpdateTimerText();
            yield return null;
        }

        timer.text = "00:00";
        timerEnd = true;

    }

    private void UpdateTimerText()
    {
        if (timer == null) return;
        int remaining = Mathf.CeilToInt(totalTime);
        timer.text = $"{remaining / 60:00}:{remaining % 60:00}";
    }


    private void openFounction()
    {
        if (IsResultOpen || Time.timeScale == 0f)
        {
            openButton.SetActive(false);
            closeButton.SetActive(false);
            return;
        }
        bool playerNearby = Physics.CheckBox(
               openOBJ.position,
               new Vector3(openOBJsizeX, openOBJsizeY, openOBJsizeZ),
               Quaternion.identity,
               playerMask);

        if (playerNearby && GameInput.GetKeyDown(KeyCode.E))
        {
            if (isOpen && needMoney <= player.money)
            {
                gameEndFuntion(true);
            }
            else if (!isOpen)
            {
                isOpen = true;
                timerOBJ.SetActive(true);

                dayOBJ.SetActive(true);
            }
            else if (needMoney > player.money)
            {
                wearingText.SetActive(true);
                CancelInvoke(nameof(HideWearingText));
                Invoke("HideWearingText", 2f);
            }
        }

        if (playerNearby && !isOpen && !reportFrame.activeSelf)
        {
            openButton.SetActive(true);
            closeButton.SetActive(false);
            SetHover(openAnimator, true);
        }
        else if (!playerNearby && !isOpen && !reportFrame.activeSelf)
        {
            openButton.SetActive(false);
            SetHover(openAnimator, false);
        }

        if (playerNearby && isOpen)
        {
            closeButton.SetActive(true);
            openButton.SetActive(false);
            SetHover(closeAnimator, true);
        }
        else if (!playerNearby && isOpen)
        {
            closeButton.SetActive(false);
            SetHover(closeAnimator, false);
        }
    }

    private static void SetHover(Animator animator, bool active)
    {
        if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            animator.SetBool("bHoverLoop", active);
    }

    private void gameEndFuntion(bool resultbool)
    {
        if (IsResultOpen) return;
        daySucceeded = resultbool || player.money >= needMoney;
        isOpen = false;
        timerStart = false;
        timerEnd = false;
        StopAllCoroutines();
        openButton.SetActive(false);
        closeButton.SetActive(false);
        wearingText.SetActive(false);
        reportFrame.SetActive(true);
        completeOrders.text = $"완료한 주문 수: {player.completeOrder:N0}";
        fallOrders.text = $"실패한 주문 수: {player.fallOrder:N0}";
        result.text = daySucceeded ? "오늘도 수고했어요!" : "다시 도전해 봐요";
        TotalRevenue.text = $"총 수익: {player.money:N0} / {needMoney:N0}";
        var offers = GetComponent<LaundryEvents>();
        if (offers != null && offers.BonusEarned > 0)
            TotalRevenue.text += $"\n<size=24>이벤트 보너스 +{offers.BonusEarned:N0} 포함</size>";
        fallButtons.SetActive(!daySucceeded);
        winButtons.SetActive(daySucceeded);
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    public void AgainButton()
    {
        if (!IsResultOpen) return;
        if (daySucceeded)
        {
            NeedMoneyCount();
            dayCount += 1;
        }
        player.money = 0;
        player.grabClothes = false;
        var economy = FindAnyObjectByType<ShopEconomy>();
        if (economy != null) economy.ReturnItem(player.GetComponent<Grab>().itemID);
        if (economy != null && LaundryWorkflow.Current != null) economy.ResetLaundryDay();
        player.GetComponent<Grab>().SetID(-1);
        player.fallOrder = 0;
        player.completeOrder = 0;
        timerEnd = false;

        reportFrame.SetActive(false);
        fallButtons.SetActive(false);
        winButtons.SetActive(false);

        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

        totalTime = dayDuration;
        timerStart = false;
        timerEnd = false;
        UpdateTimerText();
    }

    private void HideWearingText()
    {
        wearingText.SetActive(false);
    }

}
