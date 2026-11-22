using System;
using UnityEngine;

// Operating funds are separate from daily sales, so buying stock never undoes the day's goal.
public partial class ShopEconomy : MonoBehaviour
{
    private const string SaveKey = "Laundry.Shop.v1";
    [Serializable] private class SaveData
    {
        public int version = 2;
        public int funds;
        public int[] stock, levels;
        public LaundryOperationsState laundry;
    }
    [SerializeField] private int startingFunds = 6000;
    private int funds;
    private int[] stock, reserved, levels = new int[3];
    private bool initialized, saveEnabled;
    public event Action Changed;
    public int Funds => funds;
    public int Capacity => 6 + 3 * Level(0);
    public int UnitCost => 300 - 50 * Level(1);
    public int OrderIncome => 1000 + 250 * Level(2);
    public int ItemCount => stock == null ? 0 : stock.Length;
    public int Level(int upgrade) => upgrade >= 0 && upgrade < levels.Length ? levels[upgrade] : 0;
    public int Stock(int index) => Valid(index) ? stock[index] : 0;
    public int FreeCapacity(int index) => Valid(index) ? Mathf.Max(0, Capacity - stock[index] - reserved[index]) : 0;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        var boxes = FindAnyObjectByType<BoxManager>();
        bool persistent = true;
#if UNITY_EDITOR
        // Automated playtests use a temporary shop and cannot overwrite the player's purchases.
        persistent = string.IsNullOrEmpty(UnityEditor.SessionState.GetString("LaundryQA.ActiveSession", ""));
#endif
        Initialize(boxes != null ? boxes.boxDatas.Length : 7, persistent);
    }

    private void Initialize(int count, bool persistent)
    {
        stock = new int[Mathf.Max(0, count)];
        reserved = new int[stock.Length];
        levels = new int[3];
        funds = startingFunds;
        operations = new LaundryOperationsState();
        for (int i = 0; i < stock.Length; i++) stock[i] = 6;
        saveEnabled = persistent;
        initialized = true;
        if (persistent && PlayerPrefs.HasKey(SaveKey)) Restore(PlayerPrefs.GetString(SaveKey));
    }

    private bool Valid(int index) => initialized && index >= 0 && index < stock.Length;

    public bool TryTake(int index)
    {
        if (!Valid(index) || stock[index] <= 0 || HasLaundryCargo) return false;
        for (int i = 0; i < reserved.Length; i++) if (reserved[i] > 0) return false;
        stock[index]--; reserved[index]++;
        Commit();
        return true;
    }

    public bool ReturnItem(int index)
    {
        if (!Valid(index) || reserved[index] == 0) return false;
        reserved[index]--; stock[index]++;
        Commit();
        return true;
    }

    public bool CompleteSale(int index)
    {
        if (!Valid(index) || reserved[index] == 0) return false;
        reserved[index]--;
        funds = Mathf.Min(9999999, funds + OrderIncome);
        Commit();
        return true;
    }

    public bool EmergencyAvailable(int index) => Valid(index) && stock[index] == 0 && reserved[index] == 0 && funds < UnitCost;
    public int RestockAmount(int index) => EmergencyAvailable(index) ? 1 : Mathf.Min(3, FreeCapacity(index), funds / UnitCost);
    public int RestockCost(int index) => EmergencyAvailable(index) ? 0 : RestockAmount(index) * UnitCost;

    public bool TryRestock(int index)
    {
        if (!Valid(index)) return false;
        int amount = RestockAmount(index), cost = RestockCost(index);
        if (amount <= 0 || funds < cost || amount > FreeCapacity(index)) return false;
        funds -= cost; stock[index] += amount;
        Commit();
        return true;
    }

    public int UpgradeCost(int upgrade)
    {
        if (upgrade < 0 || upgrade >= 3 || Level(upgrade) >= 2) return 0;
        int[] first = { 4000, 5000, 6000 }, second = { 7500, 9000, 11000 };
        return Level(upgrade) == 0 ? first[upgrade] : second[upgrade];
    }

    public bool TryUpgrade(int upgrade)
    {
        if (!initialized) return false;
        int cost = UpgradeCost(upgrade);
        if (cost <= 0 || funds < cost) return false;
        funds -= cost; levels[upgrade]++;
        Commit();
        return true;
    }

    public string CaptureSave()
    {
        var savedStock = new int[stock.Length];
        // A carried garment is returned to its shelf on a fresh scene/load.
        for (int i = 0; i < stock.Length; i++) savedStock[i] = stock[i] + reserved[i];
        return JsonUtility.ToJson(new SaveData { funds = funds, stock = savedStock, levels = (int[])levels.Clone(), laundry = operations });
    }

    private void Restore(string json)
    {
        SaveData data;
        try { data = JsonUtility.FromJson<SaveData>(json); }
        catch (ArgumentException) { return; }
        if (data == null || (data.version != 1 && data.version != 2) || data.levels == null || data.levels.Length != 3 || data.stock == null) return;
        for (int i = 0; i < 3; i++) levels[i] = Mathf.Clamp(data.levels[i], 0, 2);
        funds = Mathf.Clamp(data.funds, 0, 9999999);
        for (int i = 0; i < Mathf.Min(stock.Length, data.stock.Length); i++) stock[i] = Mathf.Clamp(data.stock[i], 0, Capacity);
        if (data.version == 2 && ValidOperations(data.laundry))
        {
            operations = data.laundry;
            for (int i = 0; i < 3; i++) operations.supplies[i] = Mathf.Clamp(operations.supplies[i], 0, Capacity);
        }
    }

    private void Commit()
    {
        if (saveEnabled) { PlayerPrefs.SetString(SaveKey, CaptureSave()); PlayerPrefs.Save(); }
        Changed?.Invoke();
    }
}
