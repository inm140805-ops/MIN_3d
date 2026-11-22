using UnityEngine;

public class Grab : MonoBehaviour
{
    [SerializeField] private GameObject handR;
    [SerializeField] private GameObject handL;
    [SerializeField] private Transform handRPos;
    [SerializeField] private Transform handLPos;
    [SerializeField] private Transform orHandRPos;
    [SerializeField] private Transform orHandLPos;

    private GameObject mainItem;
    public int itemID = -1;
    [SerializeField] private GameObject item;

    private void Awake()
    {
        // Held clothing is a visual, not an extension of the player's physics body.
        // Its collider used to catch on machines when turning away from a shelf.
        if (item != null)
            foreach (var collider in item.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
    }

    public void SetID(int id)
    {
        itemID = id;
    }

    private void Update()
    {
        bool cargo = LaundryWorkflow.Current != null && LaundryWorkflow.Current.HasCargo;
        if (itemID == -1 || cargo)
        {
            item.SetActive(false);

        }
        else
        {
            item.SetActive(true);
        }

        if (itemID != -1 || cargo)
        {
            handR.transform.position = handRPos.position;
            handL.transform.position = handLPos.position;
        }else if (itemID == -1)
        {
            handR.transform.position = orHandRPos.position;
            handL.transform.position = orHandLPos.position;
        }
    }

}
