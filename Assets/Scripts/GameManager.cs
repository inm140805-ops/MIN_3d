using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private float spawnTime;
    [SerializeField] private Transform[] S1;
    public Transform[] deadPoints;
    public Transform[] linePoint;
    public bool[] line;
    public bool fullLine;
    private int[] orderDeck;
    private int nextOrder;
    private int lastOrder = -1;
    private int visitNumber;
    // Keep both existing collection visits and new laundry deposits in every pair of spawns.
    public NPCUI.VisitPurpose NextVisitPurpose() => visitNumber++ % 2 == 0 ? NPCUI.VisitPurpose.Collection : NPCUI.VisitPurpose.DropOff;

    // Shuffle each set of requests so a busy shop does not repeat one request by chance.
    public int NextOrderIndex(int count)
    {
        if (count <= 1) return 0;
        if (orderDeck == null || orderDeck.Length != count || nextOrder >= count)
        {
            orderDeck = new int[count];
            for (int i = 0; i < count; i++) orderDeck[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int swap = orderDeck[i]; orderDeck[i] = orderDeck[j]; orderDeck[j] = swap;
            }
            if (orderDeck[0] == lastOrder)
            {
                int j = Random.Range(1, count);
                int swap = orderDeck[0]; orderDeck[0] = orderDeck[j]; orderDeck[j] = swap;
            }
            nextOrder = 0;
        }
        lastOrder = orderDeck[nextOrder++];
        return lastOrder;
    }
    void Start()
    {
        StartCoroutine(SpawnEnemies(spawnTime));
    }

    public void RestoreLaundryCustomers()
    {
        var economy = FindAnyObjectByType<ShopEconomy>();
        if (economy == null) return;
        foreach (var ticket in economy.Operations.tickets)
        {
            if (ticket.stage == LaundryStage.Returned || ticket.serviceSlot < 0 || ticket.serviceSlot >= linePoint.Length) continue;
            bool exists = false;
            foreach (var ui in FindObjectsByType<NPCUI>(FindObjectsSortMode.None)) if (ui.LaundryTicketId == ticket.id) exists = true;
            if (exists) continue;
            int slot = ticket.serviceSlot;
            var npc = Instantiate(npcPrefab, linePoint[slot].position, Quaternion.Euler(0, 90, 0));
            var control = npc.GetComponent<NPCController>();
            control.spawnBool[0] = true; control.deadPoints = deadPoints;
            var queue = npc.GetComponent<NPCQueue>(); queue.lineTarget = slot; queue.isQueuing = true;
            line[slot] = true;
            npc.GetComponent<NPCMovement>().inLine = true;
            npc.GetComponent<NPCUI>().BindLaundryTicket(ticket.id);
        }
    }

    private void Update()
    {
        CheakFull();
    }

    IEnumerator SpawnEnemies(float time)
    {
        while (true)
        {
            yield return new WaitForSeconds(time);
            SpawnNPC();
        }
    }

    private void SpawnNPC()
    {
        int spawnIndex = Random.Range(0, S1.Length);
        GameObject NPC = Instantiate(npcPrefab, S1[spawnIndex].position, S1[spawnIndex].rotation);
        NPCController npcController = NPC.GetComponent<NPCController>();

        npcController.spawnBool[spawnIndex] = true;
        npcController.deadPoints = deadPoints;
        NPC.GetComponent<NPCUI>().ConfigureVisit(NextVisitPurpose());
    }

    private void CheakFull() 
    {
        fullLine = line.Length > 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (!line[i]) { fullLine = false; break; }
        }
    }
}
