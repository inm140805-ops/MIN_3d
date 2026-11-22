using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
using System.Runtime.CompilerServices;

public class BoxManager : MonoBehaviour
{
    [SerializeField] private Transform[] boxs;
    public Transform[] StoragePositions => boxs;
    [SerializeField] private TextMeshProUGUI[] nameTMP;
    [SerializeField] private GameObject[] uiButtonsGet;
    [SerializeField] private GameObject[] uiButtonsPut;
    [SerializeField] public BoxData[] boxDatas;
    [SerializeField] private UnityEngine.UI.Image[] iconImage;
    [SerializeField] private float sizeX;
    [SerializeField] private float sizeY;
    [SerializeField] private float sizeZ;

    private Grab grab;
    private Player player;
    private ShopEconomy economy;
    private readonly Collider[] playerOverlap = new Collider[1];
    private int playerMask;
    private Animator[] getAnimators;
    private Animator[] putAnimators;
    private ParticleSystem pickupBurst;
    private static Material pickupMaterial;

    private void Awake()
    {
        player = GameObject.Find("Player").GetComponent<Player>();
        grab = GameObject.Find("Player").GetComponent<Grab>();
        playerMask = LayerMask.GetMask("PlayerBody");
        getAnimators = new Animator[boxs.Length];
        putAnimators = new Animator[boxs.Length];
        for (int i = 0; i < boxs.Length; i++)
        {
            getAnimators[i] = uiButtonsGet[i].GetComponent<Animator>();
            putAnimators[i] = uiButtonsPut[i].GetComponent<Animator>();
            StylePrompt(uiButtonsGet[i]);
            StylePrompt(uiButtonsPut[i]);
        }
        CreatePickupBurst();
        ConfigSetting();
    }

    

    private void Update()
    {
        OnTriggerInteraction();
    }

    private void Start()
    {
        economy = FindAnyObjectByType<ShopEconomy>();
        if (economy != null) economy.Changed += ConfigSetting;
        ConfigSetting();
    }
    private void OnDestroy() { if (economy != null) economy.Changed -= ConfigSetting; }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        for (int i = 0; i < boxs.Length; i++)
        {
            Transform box = boxs[i];

            Gizmos.DrawWireCube(
                box.position,
                new Vector3(sizeX * 2, sizeY * 2, sizeZ * 2)
            );
        }
    }

    private void ConfigSetting()
    {
        for (int i = 0; i < boxs.Length; i++)
        {
            TextMeshProUGUI nameText = nameTMP[i];
            UnityEngine.UI.Image icon = iconImage[i];

            nameText.text = boxDatas[i].BoxName + (economy != null ? "\n<size=80%>재고 " + economy.Stock(i) + "/" + economy.Capacity + "</size>" : "");
            icon.sprite = boxDatas[i].Icon;
        }
    }

    private void OnTriggerInteraction()
    {
        Vector3 halfSize = new Vector3(sizeX, sizeY, sizeZ);
        bool blocked = LaundryWorkflow.Current != null && LaundryWorkflow.Current.BlocksLegacyInteraction;
        bool interact = player.CanControl && !blocked && GameInput.GetKeyDown(KeyCode.E);
        for (int i = 0; i < boxs.Length; i++)
        {
            bool nearby = player.CanControl && !blocked && Physics.OverlapBoxNonAlloc(
                boxs[i].position, halfSize, playerOverlap, Quaternion.identity, playerMask) > 0;
            bool canTake = nearby && !player.grabClothes;
            bool canReturn = nearby && player.grabClothes && grab.itemID == i;
            SetPrompt(uiButtonsGet[i], getAnimators[i], canTake);
            SetPrompt(uiButtonsPut[i], putAnimators[i], canReturn);
            if (!interact || (!canTake && !canReturn)) continue;

            if (canTake && economy != null && !economy.TryTake(i))
            {
                if (ShopHUD.Current != null) ShopHUD.Current.ShowNotice(boxDatas[i].BoxName + " 품절 · 가게 컴퓨터에서 발주하세요");
                interact = false;
                continue;
            }
            if (canReturn && economy != null) economy.ReturnItem(i);

            player.grabClothes = canTake;
            grab.SetID(canTake ? i : -1);
            if (canTake) PlayPickupBurst(boxs[i].position + Vector3.up * 0.35f);
            interact = false;
        }
    }

    private void CreatePickupBurst()
    {
        if (pickupMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            pickupMaterial = new Material(shader) { name = "Laundry pickup bubbles" };
        }

        var effect = new GameObject("Pickup Bubbles");
        effect.transform.SetParent(transform, false);
        pickupBurst = effect.AddComponent<ParticleSystem>();
        var main = pickupBurst.main;
        main.playOnAwake = false;
        main.loop = false;
        main.startLifetime = 0.48f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.12f);
        main.maxParticles = 12;
        main.gravityModifier = 0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = pickupBurst.emission;
        emission.enabled = false;
        var shape = pickupBurst.shape;
        shape.enabled = false;
        var size = pickupBurst.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 0f));
        var alpha = new Gradient();
        alpha.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.92f, 0.12f), new GradientAlphaKey(0f, 1f) });
        var color = pickupBurst.colorOverLifetime;
        color.enabled = true;
        color.color = alpha;
        pickupBurst.GetComponent<ParticleSystemRenderer>().sharedMaterial = pickupMaterial;
    }

    private void PlayPickupBurst(Vector3 position)
    {
        if (pickupBurst == null) return;

        pickupBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pickupBurst.Play();
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 0.25f;
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = new Vector3(Mathf.Cos(angle) * 0.55f, 0.35f + (i % 3) * 0.12f, Mathf.Sin(angle) * 0.55f),
                startColor = i % 3 == 0 ? new Color32(255, 244, 214, 255)
                    : i % 3 == 1 ? new Color32(159, 219, 199, 255)
                    : new Color32(255, 209, 124, 255)
            };
            pickupBurst.Emit(particle, 1);
        }
    }

    private void SetPrompt(GameObject prompt, Animator animator, bool active)
    {
        if (prompt.activeSelf == active) return;
        prompt.SetActive(active);
        if (active && animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool("bHoverLoop", true);
    }

    private static void StylePrompt(GameObject prompt)
    {
        LaundrySkin.StylePrompt(prompt);
    }
}
