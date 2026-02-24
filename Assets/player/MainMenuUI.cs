using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    const string PrefVolume = "settings.volume";
    const string PrefDifficulty = "settings.difficulty";

    [Header("UI (назначь в инспекторе)")]
    public GameObject root;
    public Button playButton;
    public Button settingsButton;
    public Button quitButton;

    [Header("Экран настроек")]
    public GameObject settingsRoot;
    public Button settingsBackButton;
    public Slider volumeSlider;
    public TMP_Dropdown difficultyDropdown;

    [Header("Опционально")]
    public TMP_Text titleText;
    public string title = "Главное меню";

    [Header("Сцена игры")]
    public string gameSceneName = "Game";

    void Awake()
    {
        Time.timeScale = 1f;
        ShowMain();

        if (titleText != null)
            titleText.text = title;

        if (playButton != null)
            playButton.onClick.AddListener(Play);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowSettings);

        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(ShowMain);

        if (quitButton != null)
            quitButton.onClick.AddListener(Quit);

        LoadSettingsToUI();

        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        if (difficultyDropdown != null)
            difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Play()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("MainMenuUI: gameSceneName пустой");
            return;
        }

        // ====== ДОБАВЬ ЭТУ СТРОКУ ======
        if (MusicManager.Instance != null)
            MusicManager.Instance.StartMusic();
        // ================================

        SceneManager.LoadScene(gameSceneName);
    }

    void Quit()
    {
        Debug.Log("MainMenuUI: Quit pressed");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void ShowMain()
    {
        if (root != null) root.SetActive(true);
        if (settingsRoot != null) settingsRoot.SetActive(false);
    }

    void ShowSettings()
    {
        if (root != null) root.SetActive(false);
        if (settingsRoot != null) settingsRoot.SetActive(true);
    }

    void LoadSettingsToUI()
    {
        float volume = PlayerPrefs.GetFloat(PrefVolume, 1f);
        int difficulty = PlayerPrefs.GetInt(PrefDifficulty, 1);

        AudioListener.volume = Mathf.Clamp01(volume);

        if (volumeSlider != null)
            volumeSlider.value = AudioListener.volume;

        if (difficultyDropdown != null)
            difficultyDropdown.value = Mathf.Clamp(difficulty, 0, difficultyDropdown.options.Count - 1);
    }

    void OnVolumeChanged(float v)
    {
        v = Mathf.Clamp01(v);
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(PrefVolume, v);
        PlayerPrefs.Save();

        // ====== ДОБАВЬ ЭТИ 2 СТРОКИ ======
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetVolume(v);
        // ==================================
    }

    void OnDifficultyChanged(int idx)
    {
        PlayerPrefs.SetInt(PrefDifficulty, idx);
        PlayerPrefs.Save();
    }
}