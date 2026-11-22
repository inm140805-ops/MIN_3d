using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class ShopComputer : MonoBehaviour
{
    public static ShopComputer Current { get; private set; }
    private static int closedFrame = -1;
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;
    [SerializeField] private Transform usePosition;
    [SerializeField] private GameObject prompt, panel;
    [SerializeField] private RectTransform window;
    [SerializeField] private TMP_Text fundsText, infoText, noticeText, restockText;
    [SerializeField] private TMP_Text[] stockCounts, upgradeDescriptions, upgradePrices;
    [SerializeField] private UnityEngine.UI.Button[] stockButtons, upgradeButtons;
    [SerializeField] private UnityEngine.UI.Button restockButton;
    [SerializeField] private Sprite normalRow, selectedRow;
    private ShopEconomy economy;
    private BoxManager boxes;
    private Player player;
    private CoreLoopManager loop;
    private GUIManager gui;
    private int selected;
    [SerializeField] private TMP_Text quantityText;
    private bool suppliesPage = true;
    private int quantity = 3, displayedSecond = -1;
    private int Catalog => suppliesPage ? selected == 3 ? economy.SupplySetCatalog : selected : selected + 3;
    public void SelectSupplies() { suppliesPage = true; selected = 0; Refresh(); }
    public void SelectClothing() { suppliesPage = false; selected = 0; Refresh(); }
    public void IncreaseQuantity() { quantity = Mathf.Min(economy.Capacity, quantity + 1); Refresh(); }
    public void DecreaseQuantity() { quantity = Mathf.Max(1, quantity - 1); Refresh(); }
    private float previousTimeScale = 1f, openedAt;
    private bool ownsPause;
    public bool IsOpen => panel != null && panel.activeSelf;
    public int SelectedCatalog => Catalog;
    public int OrderQuantity => quantity;
    public bool ShowingSupplies => suppliesPage;

    private void Awake()
    {
        Current = this;
        economy = FindAnyObjectByType<ShopEconomy>();
        boxes = FindAnyObjectByType<BoxManager>();
        player = FindAnyObjectByType<Player>();
        loop = FindAnyObjectByType<CoreLoopManager>();
        gui = FindAnyObjectByType<GUIManager>();
        if (panel != null) panel.SetActive(false);
        if (prompt != null) prompt.SetActive(false);
        if (economy != null) economy.Changed += Refresh;
    }

    private void Update()
    {
        if (player == null || economy == null || usePosition == null) return;
        if (IsOpen)
        {
            if ((int)Time.unscaledTime != displayedSecond) { displayedSecond = (int)Time.unscaledTime; Refresh(); }
            float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.16f);
            window.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, 1f - (1f - t) * (1f - t));
            if (GameInput.GetKeyDown(KeyCode.Escape) || loop.IsResultOpen) Close();
            return;
        }
        Vector3 delta = player.transform.position - usePosition.position; delta.y = 0;
        bool nearby = player.CanControl && !loop.IsResultOpen && delta.sqrMagnitude <= .8f * .8f;
        prompt.SetActive(nearby);
        if (nearby && GameInput.GetKeyDown(KeyCode.E)) Open();
    }

    private void Open()
    {
        if (IsOpen || economy == null || gui.esc || loop.IsResultOpen || Time.timeScale <= 0) return;
        previousTimeScale = Time.timeScale;
        ownsPause = true;
        gui.CloseOrderGuide();
        Time.timeScale = 0;
        openedAt = Time.unscaledTime;
        panel.SetActive(true); panel.transform.SetAsLastSibling(); prompt.SetActive(false);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        noticeText.text = "재고와 설비는 다음 날에도 유지됩니다.";
        Refresh();
        if (EventSystem.current != null && stockButtons.Length > 0) EventSystem.current.SetSelectedGameObject(stockButtons[selected].gameObject);
    }

    public void Close()
    {
        if (!IsOpen) return;
        panel.SetActive(false); window.localScale = Vector3.one;
        Time.timeScale = previousTimeScale;
        ownsPause = false;
        closedFrame = Time.frameCount;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    public void SelectStock(int index)
    {
        if (!IsOpen || index < 0 || index >= economy.ItemCount) return;
        selected = index; Refresh();
    }

    public void RestockSelected()
    {
        if (!IsOpen) return;
        if (LaundryWorkflow.Current != null)
        {
            string message; economy.OrderDelivery(Catalog, quantity, LaundryClock.Now, out message);
            noticeText.text = message; Refresh(); return;
        }
        int amount = economy.RestockAmount(selected), cost = economy.RestockCost(selected);
        if (economy.TryRestock(selected)) noticeText.text = boxes.boxDatas[selected].BoxName + " +" + amount + "벌 입고 · " + (cost == 0 ? "긴급 지원" : "-" + cost.ToString("N0"));
        else noticeText.text = "보관 공간 또는 운영자금을 확인해 주세요.";
        Refresh();
    }
    public void UpgradeCapacity() { Upgrade(0); }
    public void UpgradeSupply() { Upgrade(1); }
    public void UpgradeService() { Upgrade(2); }
    private void Upgrade(int index)
    {
        if (!IsOpen) return;
        string[] names = { "창고 확장", "거래처 계약", "프리미엄 포장" };
        noticeText.text = economy.TryUpgrade(index) ? names[index] + " 업그레이드 완료!" : "운영자금 또는 업그레이드 단계를 확인해 주세요.";
        Refresh();
    }

    private void Refresh()
    {
        if (economy == null || !IsOpen) return;
        fundsText.text = "운영자금  " + economy.Funds.ToString("N0");
        infoText.text = "주문당 자금 +" + economy.OrderIncome.ToString("N0") + "  ·  발주 단가 " + economy.UnitCost.ToString("N0") + "\n오늘 매출과 별도로 모이는 가게 운영자금입니다.";
        for (int i = 0; i < stockCounts.Length; i++)
        {
            stockCounts[i].text = economy.Stock(i) + " / " + economy.Capacity + (economy.Stock(i) == 0 ? "  품절" : "");
            foreach (TMP_Text label in stockButtons[i].GetComponentsInChildren<TMP_Text>(true))
                label.color = i == selected ? Color.white : Color.black;
            stockCounts[i].color = economy.Stock(i) == 0 ? (i == selected ? new Color32(255, 177, 142, 255) : new Color32(160, 25, 25, 255)) : (i == selected ? Color.white : Color.black);
            stockButtons[i].GetComponent<UnityEngine.UI.Image>().sprite = i == selected ? selectedRow : normalRow;
        }
        int amount = economy.RestockAmount(selected), cost = economy.RestockCost(selected);
        restockButton.interactable = amount > 0;
        restockText.color = amount > 0 ? Color.black : new Color32(90, 90, 90, 255);
        restockText.text = economy.EmergencyAvailable(selected) ? "긴급 1벌 지원 · 무료" : amount > 0 ? boxes.boxDatas[selected].BoxName + " +" + amount + "벌 발주 · " + cost.ToString("N0") : "보관함이 가득 찼거나 운영자금이 부족해요";
        string[] descriptions = {
            "품목별 보관량  " + economy.Capacity + (economy.Level(0) < 2 ? " → " + (economy.Capacity + 3) + "벌" : "벌"),
            "한 벌 발주 단가  " + economy.UnitCost + (economy.Level(1) < 2 ? " → " + (economy.UnitCost - 50) : ""),
            "주문 자금 +" + economy.OrderIncome.ToString("N0") + (economy.Level(2) < 2 ? " → +" + (economy.OrderIncome + 250).ToString("N0") : "")
        };
        for (int i = 0; i < upgradeButtons.Length; i++)
        {
            int price = economy.UpgradeCost(i);
            upgradeDescriptions[i].text = "LV " + economy.Level(i) + " / 2   ·   " + descriptions[i];
            upgradePrices[i].text = price == 0 ? "최대 단계" : economy.Funds >= price ? "업그레이드 · " + price.ToString("N0") : "자금 부족 · " + price.ToString("N0");
            upgradeButtons[i].interactable = price > 0 && economy.Funds >= price;
            upgradePrices[i].color = upgradeButtons[i].interactable ? Color.black : new Color32(90, 90, 90, 255);
        }
        if (LaundryWorkflow.Current != null) RefreshDelivery();
    }

    private void RefreshDelivery()
    {
        var workflow = LaundryWorkflow.Current;
        for (int i = 0; i < stockButtons.Length; i++)
        {
            stockButtons[i].gameObject.SetActive(!suppliesPage || i < 4);
            int catalog = suppliesPage ? i == 3 ? economy.SupplySetCatalog : i : i + 3;
            if (suppliesPage && i >= 4) continue;
            var name = stockButtons[i].transform.Find("Clothing Name");
            if (name != null) name.GetComponent<TMP_Text>().text = workflow.CatalogName(catalog);
            var icon = stockButtons[i].transform.Find("Clothing Icon");
            if (icon != null)
            {
                var image = icon.GetComponent<UnityEngine.UI.Image>();
                image.sprite = suppliesPage ? Resources.Load<Sprite>("LaundryRetro/Supply" + Mathf.Min(i, 2)) : boxes.boxDatas[i].Icon;
            }
            stockCounts[i].text = economy.CatalogStock(catalog) + "/" + economy.Capacity + " · " + economy.CatalogUnitCost(catalog);
        }
        if (quantityText != null) quantityText.text = "수량  " + quantity;
        var d = economy.Operations.delivery;
        bool busy = d != null && (!d.Complete || LaundryClock.Now < d.dueAt + 35);
        int price = economy.CatalogUnitCost(Catalog) * quantity;
        bool emergency = Catalog < 3 && economy.Supply(Catalog) == 0 && economy.Funds < economy.CatalogUnitCost(Catalog) && quantity == 1;
        bool room = economy.CatalogSpace(Catalog) >= quantity;
        restockButton.interactable = !busy && room && (emergency || economy.Funds >= price);
        restockText.color = restockButton.interactable ? Color.black : Color.gray;
        restockText.text = busy ? "기존 배송 정리·차량 복귀 대기" : emergency ? "긴급 지원 1개 배송 · 무료" : workflow.CatalogName(Catalog) + " ×" + quantity + " 발주 · " + price.ToString("N0");
        infoText.text = workflow.DeliveryStatus() + "\n주문 60초 뒤 도착 · 박스에서 꺼내 보관함에 직접 입고";
        if (Catalog == economy.SupplySetCatalog) infoText.text = workflow.DeliveryStatus() + "\n1세트 = 세제 1 + 건조 시트 1 + 포장재 1 · 60초 배송";
        if (!room && !busy) infoText.text += " · 공간 부족";
    }

    private void OnDestroy()
    {
        if (economy != null) economy.Changed -= Refresh;
        if (Application.isPlaying && ownsPause) Time.timeScale = previousTimeScale;
        if (Current == this) { Current = null; closedFrame = -1; }
    }
}
