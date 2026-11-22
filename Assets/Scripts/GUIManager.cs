using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GUIManager : MonoBehaviour
{
    public void CloseOrderGuide() { if (OrderMenu != null) OrderMenu.gameObject.SetActive(false); }
    private const string SensitivityKey = "MouseSensitivity";
    private const string MusicVolumeKey = "MusicVolume";

    [SerializeField] private TMP_Text Money;
    [SerializeField] private Image OrderMenu;
    [SerializeField] private GameObject escPanal;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_InputField inputTextSensitivity;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TMP_InputField inputTextMusic;
    [SerializeField] private GameObject settingPanal;

    private Player player;
    private CoreLoopManager coreLoopManager;
    private MusicManager musicManager;
    public bool esc;

    private void Awake()
    {
        player = FindAnyObjectByType<Player>();
        GameObject loopObject = GameObject.Find("CoreLoopManager");
        if (loopObject != null) coreLoopManager = loopObject.GetComponent<CoreLoopManager>();
        musicManager = FindAnyObjectByType<MusicManager>();
    }

    private void Start()
    {
        SetupSensitivity();
        SetupMusicVolume();
    }

    private void Update()
    {
        if (Money == null || player == null || coreLoopManager == null) return;
        Money.text = $"{player.money:N0} <size=60%>/ {coreLoopManager.needMoney:N0}</size>";
        OrderCheackUI();
        InputEsc();

        if (esc || (ShopComputer.Current != null && ShopComputer.Current.IsOpen))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }else if (!esc && !coreLoopManager.reportFrame.activeSelf)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OrderCheackUI()
    {
        if (OrderMenu != null && GameInput.GetKeyDown(KeyCode.Tab) && !esc && !coreLoopManager.IsResultOpen && (ShopComputer.Current == null || !ShopComputer.Current.IsOpen))
        {
            if (OrderMenu.gameObject.activeSelf) 
            {
                OrderMenu.gameObject.SetActive(false);
            }else if (!OrderMenu.gameObject.activeSelf)
            {
                OrderMenu.gameObject.SetActive(true);
            }   
        }
    }

    private void InputEsc()
    {
        if (ShopComputer.ClosedThisFrame || (ShopComputer.Current != null && ShopComputer.Current.IsOpen)) return;
        if (escPanal == null || settingPanal == null || coreLoopManager == null) return;
        if (GameInput.GetKeyDown(KeyCode.Escape) && !esc && coreLoopManager.reportFrame != null && !coreLoopManager.reportFrame.activeSelf)
        {
            Time.timeScale = 0f;
            escPanal.SetActive(true);
            OrderMenu.gameObject.SetActive(false);
            esc = true;
        }else if (GameInput.GetKeyDown(KeyCode.Escape) && esc)
        {
            Time.timeScale = 1f;
            escPanal.SetActive(false);
            settingPanal.SetActive(false);
            esc = false;
        }
    }

    public void KeepGoing()
    {
        Time.timeScale = 1f;
        escPanal.SetActive(false);
        esc = false;
    }

    public void Stop()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Intro");
    }

    public void Setting()
    {
        settingPanal.SetActive(true);
        escPanal.SetActive(false);
    }

    public void BackSetting()
    {
        settingPanal.SetActive(false);
        escPanal.SetActive(true);
    }

    private void SetupSensitivity()
    {
        if (sensitivitySlider == null || inputTextSensitivity == null)
            return;

        sensitivitySlider.wholeNumbers = true;

        float savedValue = PlayerPrefs.GetFloat(SensitivityKey, 100f);
        savedValue = Mathf.Clamp(savedValue, sensitivitySlider.minValue, sensitivitySlider.maxValue);
        sensitivitySlider.value = savedValue;
        inputTextSensitivity.text = Mathf.RoundToInt(savedValue).ToString();
        if (player != null) player.mouseSpeed = savedValue;

        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            int intValue = Mathf.RoundToInt(value);
            inputTextSensitivity.text = intValue.ToString();
            PlayerPrefs.SetFloat(SensitivityKey, intValue);

            if (player != null)
                player.mouseSpeed = intValue;
        });

        inputTextSensitivity.onEndEdit.AddListener(text =>
        {
            if (int.TryParse(text, out int value))
            {
                value = Mathf.Clamp(
                    value,
                    Mathf.CeilToInt(sensitivitySlider.minValue),
                    Mathf.FloorToInt(sensitivitySlider.maxValue)
                );

                sensitivitySlider.value = value;
                inputTextSensitivity.text = value.ToString();
                PlayerPrefs.SetFloat(SensitivityKey, value);

                if (player != null)
                    player.mouseSpeed = value;
            }
            else
            {
                inputTextSensitivity.text = Mathf.RoundToInt(sensitivitySlider.value).ToString();
            }
        });
    }

    private void SetupMusicVolume()
    {
        if (musicSlider == null || inputTextMusic == null)
            return;

        musicSlider.wholeNumbers = true;

        float savedVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 50f);
        savedVolume = Mathf.Clamp(savedVolume, musicSlider.minValue, musicSlider.maxValue);
        musicSlider.value = savedVolume;
        inputTextMusic.text = Mathf.RoundToInt(savedVolume).ToString();

        musicSlider.onValueChanged.AddListener(value =>
        {
            int volume = Mathf.RoundToInt(value);
            inputTextMusic.text = volume.ToString();
            PlayerPrefs.SetFloat(MusicVolumeKey, volume);

            if (musicManager != null)
                musicManager.SetVolume(volume);
        });

        if (musicManager != null)
            musicManager.SetVolume(savedVolume);

        inputTextMusic.onEndEdit.AddListener(text =>
        {
            if (int.TryParse(text, out int volume))
            {
                volume = Mathf.Clamp(
                    volume,
                    Mathf.CeilToInt(musicSlider.minValue),
                    Mathf.FloorToInt(musicSlider.maxValue)
                );

                musicSlider.value = volume;
                inputTextMusic.text = volume.ToString();
                PlayerPrefs.SetFloat(MusicVolumeKey, volume);

                if (musicManager != null)
                    musicManager.SetVolume(volume);
            }
            else
            {
                inputTextMusic.text = Mathf.RoundToInt(musicSlider.value).ToString();
            }
        });
    }
}
