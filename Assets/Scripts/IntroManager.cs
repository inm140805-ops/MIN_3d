using System.Diagnostics;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject mainIntro;
    [SerializeField] private GameObject setting;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TMP_InputField inputTextSensitivity;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private TMP_InputField inputTextMusic;

    private Animator[] buttonAnimators;
    private TMP_Text[] buttonTexts;
    private Player player;
    [SerializeField] private int selectedIndex = 0;

    private void Awake()
    {
        Button[] buttons = { startButton, settingButton, exitButton };
        buttonAnimators = new Animator[buttons.Length];
        buttonTexts = new TMP_Text[buttons.Length];
        player = FindAnyObjectByType<Player>();

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
            {
                buttonAnimators[i] = buttons[i].GetComponent<Animator>();
                buttonTexts[i] = buttons[i].GetComponentInChildren<TMP_Text>();
            }
        }
    }

    private const string SensitivityKey = "MouseSensitivity";
    private const string MusicVolumeKey = "MusicVolume";

    private void Start()
    {
        SetupSensitivity();
        SetupMusicVolume();
        RefreshButtonSelection();
        if (player != null) player.enabled = false;
        startButton.onClick.AddListener(() => ActivateSelection(0));
        settingButton.onClick.AddListener(() => ActivateSelection(1));
        exitButton.onClick.AddListener(() => ActivateSelection(2));

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!mainIntro.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SettingOutButton();
            return; // Editing a settings value must not activate the hidden main menu.
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {

            selectedIndex = Mathf.Max(0, selectedIndex - 1);
            RefreshButtonSelection();

        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {

            selectedIndex = Mathf.Min(buttonAnimators.Length - 1, selectedIndex + 1);
            RefreshButtonSelection();

        }

        PlayerEnterKey();
    }

    internal void RefreshButtonSelection()
    {
        for (int i = 0; i < buttonAnimators.Length; i++)
        {
            if (buttonAnimators[i] != null)
                buttonAnimators[i].enabled = false;
            if (buttonTexts[i] == null) continue;
            Image face = buttonTexts[i].GetComponentInParent<Button>().GetComponent<Image>();
            LaundrySkin.Surface(face, i == selectedIndex ? LaundrySkin.SelectedButton : LaundrySkin.Button, Color.white);
        }
    }

    internal void SelectButton(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, buttonTexts.Length - 1);
        RefreshButtonSelection();
    }

    private void PlayerEnterKey() 
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ActivateSelection(selectedIndex);
        }
    }

    private void ActivateSelection(int index)
    {
        switch (index)
        {
            case 1:
                mainIntro.SetActive(false);
                setting.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                break;
            case 2:
                #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                #else
                    Application.Quit();
                #endif
                break;
            default:
                SceneManager.LoadScene("SampleScene");
                break;
        }
    }

    public void SettingOutButton()
    {
        mainIntro.SetActive(true);
        setting.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetupMusicVolume()
    {
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
        });

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
            }
            else
            {
                inputTextMusic.text = Mathf.RoundToInt(musicSlider.value).ToString();
            }
        });
    }

    private void SetupSensitivity()
    {
        sensitivitySlider.wholeNumbers = true;

        float savedValue = PlayerPrefs.GetFloat(SensitivityKey, 100f);
        savedValue = Mathf.Clamp(savedValue, sensitivitySlider.minValue, sensitivitySlider.maxValue);

        sensitivitySlider.value = savedValue;
        inputTextSensitivity.text = Mathf.RoundToInt(savedValue).ToString();

        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            int intValue = Mathf.RoundToInt(value);
            inputTextSensitivity.text = intValue.ToString();
            PlayerPrefs.SetFloat(SensitivityKey, intValue);
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
            }
            else
            {
                inputTextSensitivity.text = Mathf.RoundToInt(sensitivitySlider.value).ToString();
            }
        });
    }

}
