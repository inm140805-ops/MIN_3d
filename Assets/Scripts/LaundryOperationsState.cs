using System;
using System.Collections.Generic;
using System.Diagnostics;

public enum LaundryStage { Dirty, Washing, Wet, Drying, Dry, Folding, Ready, Returned }
public enum LaundryLocation { Player, Washer, Dryer, Table }

[Serializable] public class LaundryTicket
{
    public string id;
    public int clothing, quantity = 1, serviceSlot, machine = -1;
    public LaundryStage stage;
    public LaundryLocation location;
    public double acceptedAt, readyAt;
    public bool wrongAttempt;
}

[Serializable] public class LaundryDelivery
{
    public string id;
    public int catalog, quantity, remaining, carrying, stored;
    public double orderedAt, dueAt, completedAt;
    public bool opened;
    public bool Complete => stored == quantity && quantity > 0;
}

[Serializable] public class LaundryOperationsState
{
    public int[] supplies = { 3, 3, 3 };
    public List<LaundryTicket> tickets = new List<LaundryTicket>();
    public LaundryDelivery delivery;
    public string heldTicket;
    public int nextTicket = 1, dailySales, dailyCompleted;
}

// This project is single-player. All authoritative local commands use this one monotonic UTC clock.
// A network edition must supply server time and execute these commands on its server.
public static class LaundryClock
{
    private static readonly double epoch = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    private static readonly Stopwatch elapsed = Stopwatch.StartNew();
    public static double Now => epoch + elapsed.Elapsed.TotalSeconds;
}
