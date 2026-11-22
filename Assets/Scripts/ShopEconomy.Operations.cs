using System;
using UnityEngine;

public partial class ShopEconomy
{
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private bool ValidOperations(LaundryOperationsState state)
    {
        if (state == null || state.supplies == null || state.supplies.Length != 3 || state.tickets == null || state.tickets.Count > 4) return false;
        var ids = new System.Collections.Generic.HashSet<string>(); var slots = new System.Collections.Generic.HashSet<int>();
        var stations = new System.Collections.Generic.HashSet<string>(); int held = 0, maxId = 0;
        foreach (var t in state.tickets)
        {
            int number;
            if (t == null || !int.TryParse(t.id, out number) || number < 1 || !ids.Add(t.id) || !Valid(t.clothing) || t.quantity < 1 || t.quantity > 2 ||
                t.serviceSlot < 0 || t.serviceSlot > 2 || !Enum.IsDefined(typeof(LaundryStage), t.stage) || !Enum.IsDefined(typeof(LaundryLocation), t.location) || !Finite(t.readyAt) || !Finite(t.acceptedAt)) return false;
            maxId = Mathf.Max(maxId, number);
            if (t.stage == LaundryStage.Returned) continue;
            if (!slots.Add(t.serviceSlot)) return false;
            if (t.location == LaundryLocation.Player) { held++; if (state.heldTicket != t.id) return false; }
            else if (!stations.Add(t.location + ":" + t.machine)) return false;
            if (t.location == LaundryLocation.Washer && (t.machine < 0 || t.machine > 2)) return false;
        }
        if (held > 1 || (held == 0 && !string.IsNullOrEmpty(state.heldTicket))) return false;
        var d = state.delivery;
        // JsonUtility serializes null inline classes as an empty value object.
        if (d != null && string.IsNullOrEmpty(d.id) && d.quantity == 0 && d.remaining == 0 && d.carrying == 0 && d.stored == 0)
        { state.delivery = null; d = null; }
        if (d != null && (string.IsNullOrEmpty(d.id) || d.catalog < 0 || d.catalog > SupplySetCatalog || d.quantity < 1 || d.quantity > Capacity ||
            d.remaining < 0 || d.carrying < 0 || d.carrying > 3 || d.stored < 0 || (long)d.remaining + d.carrying + d.stored != d.quantity ||
            !Finite(d.orderedAt) || !Finite(d.dueAt) || Math.Abs(d.dueAt - d.orderedAt - 60) > .001 || (d.carrying > 0 && (held > 0 || !d.opened)))) return false;
        state.nextTicket = Mathf.Max(1, maxId + 1, state.nextTicket);
        state.dailySales = Mathf.Clamp(state.dailySales, 0, 9999999); state.dailyCompleted = Mathf.Clamp(state.dailyCompleted, 0, 9999);
        return true;
    }
    private LaundryOperationsState operations = new LaundryOperationsState();
    public LaundryOperationsState Operations => operations;
    public static readonly string[] SupplyNames = { "세제", "건조 시트", "포장재" };
    public int SupplySetCatalog => ItemCount + 3;
    public bool IsSupplyCatalog(int catalog) => catalog >= 0 && (catalog < 3 || catalog == SupplySetCatalog);
    public bool HasLaundryCargo => !string.IsNullOrEmpty(operations.heldTicket) || (operations.delivery != null && operations.delivery.carrying > 0);
    public LaundryTicket Ticket(string id) => string.IsNullOrEmpty(id) ? null : operations.tickets.Find(t => t.id == id);
    public LaundryTicket HeldTicket => Ticket(operations.heldTicket);
    public int Supply(int index) => index >= 0 && index < 3 ? operations.supplies[index] : 0;
    public int CatalogStock(int catalog) => catalog == SupplySetCatalog ? Mathf.Min(Supply(0), Supply(1), Supply(2)) : catalog < 3 ? Supply(catalog) : Stock(catalog - 3);
    public int CatalogUnitCost(int catalog) => catalog == SupplySetCatalog ? 480 - Level(1) * 60 : catalog < 0 || catalog >= ItemCount + 3 ? 0 : catalog < 3 ? new[] { 200, 120, 160 }[catalog] - Level(1) * 20 : UnitCost;
    public int CatalogSpace(int catalog)
    {
        if (catalog == SupplySetCatalog) return Mathf.Min(CatalogSpace(0), CatalogSpace(1), CatalogSpace(2));
        if (catalog < 0 || catalog >= ItemCount + 3) return 0;
        int incoming = operations.delivery != null && !operations.delivery.Complete && (operations.delivery.catalog == catalog || (catalog < 3 && operations.delivery.catalog == SupplySetCatalog))
            ? operations.delivery.remaining + operations.delivery.carrying : 0;
        return Mathf.Max(0, (catalog < 3 ? Capacity - Supply(catalog) : FreeCapacity(catalog - 3)) - incoming);
    }

    public bool OrderDelivery(int catalog, int quantity, double now, out string reason)
    {
        reason = "";
        if (!initialized || double.IsNaN(now) || double.IsInfinity(now) || quantity < 1 || quantity > Capacity || catalog < 0 || catalog > SupplySetCatalog)
        { reason = "주문 수량을 확인해 주세요."; return false; }
        var previous = operations.delivery;
        if (previous != null && (!previous.Complete || now < previous.dueAt + 35))
        { reason = previous.Complete ? "배송 차량이 복귀 중입니다." : "기존 배송 물품을 먼저 정리해 주세요."; return false; }
        if (quantity > CatalogSpace(catalog)) { reason = "보관할 공간이 부족합니다."; return false; }
        int cost = CatalogUnitCost(catalog) * quantity;
        // Emergency credit is still physically delivered and must be carried to storage.
        bool emergency = catalog < 3 && Supply(catalog) == 0 && funds < CatalogUnitCost(catalog) && quantity == 1;
        if (!emergency && funds < cost) { reason = "운영자금이 부족합니다."; return false; }
        funds -= emergency ? 0 : cost;
        operations.delivery = new LaundryDelivery { id = Guid.NewGuid().ToString("N"), catalog = catalog, quantity = quantity,
            remaining = quantity, orderedAt = now, dueAt = now + 60.0 };
        reason = emergency ? "긴급 지원 1개 접수 · 60초 뒤 도착" : "주문 접수 · 60초 뒤 도착 · -" + cost.ToString("N0");
        Commit(); return true;
    }

    public bool OpenDelivery(string deliveryId, double now)
    {
        var d = operations.delivery;
        if (!Finite(now) || d == null || d.id != deliveryId || d.Complete || d.opened || now < d.dueAt + 4) return false;
        d.opened = true; Commit(); return true;
    }
    public bool TakeDelivery(string deliveryId, double now)
    {
        var d = operations.delivery;
        if (!Finite(now) || d == null || d.id != deliveryId || !d.opened || now < d.dueAt + 4 || HasLaundryCargo || d.remaining <= 0) return false;
        for (int i = 0; i < reserved.Length; i++) if (reserved[i] > 0) return false;
        d.carrying = Mathf.Min(3, d.remaining); d.remaining -= d.carrying; Commit(); return true;
    }
    public bool StoreDelivery(int catalog)
    {
        var d = operations.delivery;
        if (d == null || d.catalog != catalog || d.carrying <= 0 || d.Complete) return false;
        int room = catalog == SupplySetCatalog ? Capacity - Mathf.Max(Supply(0), Supply(1), Supply(2)) : catalog < 3 ? Capacity - Supply(catalog) : FreeCapacity(catalog - 3);
        if (room < d.carrying) return false;
        if (catalog == SupplySetCatalog) for (int i=0;i<3;i++) operations.supplies[i] += d.carrying;
        else if (catalog < 3) operations.supplies[catalog] += d.carrying;
        else stock[catalog - 3] += d.carrying;
        d.stored += d.carrying; d.carrying = 0;
        if (d.Complete) d.completedAt = LaundryClock.Now;
        Commit(); return true;
    }

    public LaundryTicket AcceptLaundry(int clothing, int quantity, int serviceSlot, double now)
    {
        if (!Finite(now) || !Valid(clothing) || quantity < 1 || quantity > 2 || serviceSlot < 0 || serviceSlot > 2 || HasLaundryCargo) return null;
        for (int i = 0; i < reserved.Length; i++) if (reserved[i] > 0) return null;
        if (operations.tickets.Exists(t => t.stage != LaundryStage.Returned && t.serviceSlot == serviceSlot)) return null;
        // Completed records need no live object; preserve monotonically increasing identities.
        operations.tickets.RemoveAll(t => t.stage == LaundryStage.Returned);
        var ticket = new LaundryTicket { id = (operations.nextTicket++).ToString("D4"), clothing = clothing, quantity = quantity,
            serviceSlot = serviceSlot, acceptedAt = now, stage = LaundryStage.Dirty, location = LaundryLocation.Player };
        operations.tickets.Add(ticket); operations.heldTicket = ticket.id; Commit(); return ticket;
    }

    public LaundryTicket AtStation(LaundryLocation location, int machine = -1)
        => operations.tickets.Find(t => t.stage != LaundryStage.Returned && t.location == location && (location != LaundryLocation.Washer || t.machine == machine));

    public bool UseStation(LaundryLocation location, int machine, double now, out string reason)
    {
        reason = ""; AdvanceLaundry(now);
        if (!Finite(now) || !Enum.IsDefined(typeof(LaundryLocation), location)) return false;
        for (int i = 0; i < reserved.Length; i++) if (reserved[i] > 0) { reason = "의류를 원래 보관함에 놓아 주세요."; return false; }
        if (location == LaundryLocation.Player || machine < -1 || (location == LaundryLocation.Washer && (machine < 0 || machine >= 3))) return false;
        var occupant = AtStation(location, machine);
        var held = HeldTicket;
        if (occupant != null)
        {
            if (HasLaundryCargo) { reason = "손을 비운 뒤 완성된 옷을 꺼내세요."; return false; }
            if (occupant.stage == LaundryStage.Washing || occupant.stage == LaundryStage.Drying || occupant.stage == LaundryStage.Folding)
            { reason = "작업 중 · " + Mathf.CeilToInt((float)(occupant.readyAt - now)) + "초"; return false; }
            occupant.location = LaundryLocation.Player; occupant.machine = -1; operations.heldTicket = occupant.id;
            reason = "의뢰 #" + occupant.id + " 옷을 꺼냈어요."; Commit(); return true;
        }
        if (held == null) { reason = "접수한 옷을 들고 이용해 주세요."; return false; }
        int supply = -1; float duration = 0; LaundryStage next = held.stage;
        if (location == LaundryLocation.Washer)
        {
            if (held.stage != LaundryStage.Dirty && held.stage != LaundryStage.Wet) { reason = "세탁 전·세탁 직후 옷만 보관할 수 있어요."; return false; }
            if (held.stage == LaundryStage.Dirty && Supply(0) >= held.quantity) { supply = 0; duration = 12; next = LaundryStage.Washing; }
        }
        else if (location == LaundryLocation.Dryer)
        {
            if (held.stage != LaundryStage.Wet && held.stage != LaundryStage.Dry) { reason = "세탁을 마친 옷을 넣어 주세요."; return false; }
            if (held.stage == LaundryStage.Wet && Supply(1) >= held.quantity) { supply = 1; duration = 8; next = LaundryStage.Drying; }
        }
        else if (held.stage == LaundryStage.Dry && Supply(2) >= held.quantity) { supply = 2; duration = 3; next = LaundryStage.Folding; }
        if (supply >= 0 && Supply(supply) < held.quantity)
        { reason = SupplyNames[supply] + " 부족 · 정리대에 옷을 놓고 컴퓨터에서 발주하세요."; return false; }
        if (supply >= 0) operations.supplies[supply] -= held.quantity;
        held.location = location; held.machine = machine; held.stage = next; held.readyAt = now + duration; operations.heldTicket = null;
        reason = duration > 0 ? "의뢰 #" + held.id + " 작업 시작" : "옷을 임시 보관했어요. 소모품 보충 후 꺼내서 다시 넣으면 작업합니다.";
        Commit(); return true;
    }
    public void AdvanceLaundry(double now)
    {
        if (!Finite(now)) return;
        bool changed = false;
        foreach (var t in operations.tickets)
        {
            if (now < t.readyAt) continue;
            if (t.stage == LaundryStage.Washing) { t.stage = LaundryStage.Wet; changed = true; }
            else if (t.stage == LaundryStage.Drying) { t.stage = LaundryStage.Dry; changed = true; }
            else if (t.stage == LaundryStage.Folding) { t.stage = LaundryStage.Ready; changed = true; }
        }
        if (changed) Commit();
    }
    public bool CanReturnLaundry(string customerTicket) => HeldTicket != null && HeldTicket.id == customerTicket && HeldTicket.stage == LaundryStage.Ready;
    public int ReturnLaundry(string customerTicket, int bonus)
    {
        if (!CanReturnLaundry(customerTicket)) return 0;
        var t = HeldTicket; int amount = 5000 * t.quantity + Mathf.Clamp(bonus, 0, 10000);
        t.stage = LaundryStage.Returned; operations.heldTicket = null;
        funds = Mathf.Min(9999999, funds + OrderIncome * t.quantity);
        operations.dailySales += amount; operations.dailyCompleted++;
        Commit(); return amount;
    }
    public void MarkLaundryMistake(string id) { var t = Ticket(id); if (t != null && t.stage != LaundryStage.Returned) { t.wrongAttempt = true; Commit(); } }
    public void ResetLaundryDay() { operations.dailySales = 0; operations.dailyCompleted = 0; Commit(); }
    public bool CanCompleteCollection(int clothing) => Valid(clothing) && reserved[clothing] == 1 && !HasLaundryCargo;
    public int CompleteCollection(int clothing, int bonus)
    {
        if (!CanCompleteCollection(clothing)) return 0;
        int amount = 5000 + Mathf.Clamp(bonus, 0, 10000);
        reserved[clothing]--; funds = Mathf.Min(9999999, funds + OrderIncome);
        operations.dailySales += amount; operations.dailyCompleted++;
        Commit(); return amount;
    }
}
