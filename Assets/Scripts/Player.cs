using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private float playerSpeed;
    private Rigidbody rb;
    private GUIManager guiManager;
    private CoreLoopManager coreLoopManager;
    public bool CanControl => Time.timeScale > 0f &&
        (ShopComputer.Current == null || !ShopComputer.Current.IsOpen) &&
        (guiManager == null || !guiManager.esc) &&
        (coreLoopManager == null || !coreLoopManager.IsResultOpen);

    private Vector3 moveVector;
    private float hMove;
    private float vMove;

    public float mouseSpeed;
    // Input Manager mouse axes already contain per-frame deltas. Keep the original
    // 60 Hz sensitivity while making the turn angle independent of rendering FPS.
    public float LookScale => mouseSpeed / 60f;
    private float xRotation = 0;

    [SerializeField] private GameObject cameraPivot;
    [SerializeField] private GameObject mainCamera;
    [SerializeField] private Transform headBone;
    public bool grabClothes;
    public float money;
    public float completeOrder;
    public float fallOrder;

    private void Awake()
    {
        rb = GetComponentInChildren<Rigidbody>();
        guiManager = FindAnyObjectByType<GUIManager>();
        coreLoopManager = FindAnyObjectByType<CoreLoopManager>();

        Camera playerCamera = null;
        if (mainCamera != null)
            playerCamera = mainCamera.GetComponentInChildren<Camera>(true);

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
        {
            enabled = false;
            return;
        }

        if (mainCamera == null)
            mainCamera = playerCamera.gameObject;
    }

    private void Start()
    {
        mouseSpeed = PlayerPrefs.GetFloat("MouseSensitivity", 100f);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!CanControl) { moveVector = Vector3.zero; return; }
        hMove = GameInput.GetAxisRaw("Horizontal");
        vMove = GameInput.GetAxisRaw("Vertical");

        moveVector = mainCamera.transform.forward * vMove + mainCamera.transform.right * hMove;
        moveVector.y = 0;
        moveVector.Normalize();
        

    }

    private void FixedUpdate()
    {
        Vector3 velocity = (CanControl ? moveVector : Vector3.zero) * playerSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    private void LateUpdate()
    {
        if (!CanControl) return;
        float mouseX = GameInput.GetAxis("Mouse X") * LookScale;
        float mouseY = GameInput.GetAxis("Mouse Y") * LookScale;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -60f, 60f);

        if (cameraPivot != null)
            cameraPivot.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (headBone != null)
            headBone.localRotation = Quaternion.Euler(0f, 0f, xRotation);

        transform.Rotate(Vector3.up * mouseX);
        
    }

    
}
    
