using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Carries one completed level's result across the scene change. This avoids
// making the level's GameManager persistent and keeps each level independent.
public static class ResultsSession
{
    public static bool HasResult { get; private set; }
    public static int LampsLit { get; private set; }
    public static int TotalLamps { get; private set; }
    public static int Sparks { get; private set; }
    public static string Medal { get; private set; }
    public static string RestartScene { get; private set; }
    public static string ContinueScene { get; private set; }
    public static string LevelSelectScene { get; private set; }
    public static bool ShowContinue { get; private set; }
    public static bool ShowLevelSelect { get; private set; }

    public static void Store(
        int lampsLit,
        int totalLamps,
        int sparks,
        string medal,
        string restartScene,
        string continueScene,
        string levelSelectScene,
        bool showContinue,
        bool showLevelSelect)
    {
        LampsLit = lampsLit;
        TotalLamps = totalLamps;
        Sparks = sparks;
        Medal = medal;
        RestartScene = restartScene;
        ContinueScene = continueScene;
        LevelSelectScene = levelSelectScene;
        ShowContinue = showContinue;
        ShowLevelSelect = showLevelSelect;
        HasResult = true;
    }

    public static void Clear()
    {
        HasResult = false;
        LampsLit = 0;
        TotalLamps = 0;
        Sparks = 0;
        Medal = string.Empty;
        RestartScene = string.Empty;
        ContinueScene = string.Empty;
        LevelSelectScene = string.Empty;
        ShowContinue = false;
        ShowLevelSelect = false;
    }
}

public sealed class ResultsDisplay : MonoBehaviour
{
    [Header("Lamps row")]
    [SerializeField] private Text lampsValueText;

    [Header("Sparks row")]
    [SerializeField] private Text sparksValueText;

    [Header("Trophy")]
    [SerializeField] private Image medalImage;
    [SerializeField] private Text medalText;
    [SerializeField] private Sprite goldTrophySprite;
    [SerializeField] private Sprite silverTrophySprite;
    [SerializeField] private Sprite bronzeTrophySprite;

    private Button restartButton;
    private Button continueButton;
    private Button levelSelectButton;

    private void Awake()
    {
        restartButton = FindButton("RestartButton");
        continueButton = FindButton("ContinueButton");
        levelSelectButton = FindButton("LevelSelectButton");
    }

    private void Start()
    {
        // Replace the prototype SceneNavigationButton listeners. Restart and
        // Continue had blank destinations in the original Results scene. Start
        // runs after every SceneNavigationButton has completed its Awake.
        ConfigureButton(restartButton, Restart);
        ConfigureButton(continueButton, Continue);
        ConfigureButton(levelSelectButton, LevelSelect);
    }

    private void OnEnable()
    {
        RefreshDisplay();
    }

    private Button FindButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static void ConfigureButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void RefreshDisplay()
    {
        if (ResultsSession.HasResult)
        {
            ApplyResult(
                ResultsSession.LampsLit,
                ResultsSession.TotalLamps,
                ResultsSession.Sparks,
                ResultsSession.Medal);

            continueButton?.gameObject.SetActive(ResultsSession.ShowContinue);
            levelSelectButton?.gameObject.SetActive(ResultsSession.ShowLevelSelect);
            return;
        }

        // Supports additive loading during development. When the scene is
        // opened directly, its serialized preview values remain visible.
        GameManager manager = GameManager.instance;
        if (manager != null)
        {
            ApplyResult(
                manager.UniqueLampsLit,
                manager.TotalLamps,
                manager.Sparks,
                manager.GetMedal());
            return;
        }

        Debug.LogWarning(
            "ResultsDisplay: no completed level result is available; showing preview values.",
            this);
    }

    private void ApplyResult(int lampsLit, int totalLamps, int sparks, string medal)
    {
        if (lampsValueText != null)
        {
            lampsValueText.text = $"{lampsLit} / {totalLamps}";
        }

        if (sparksValueText != null)
        {
            sparksValueText.text = sparks.ToString();
        }

        if (medalText != null)
        {
            medalText.text = medal;
        }

        if (medalImage == null)
        {
            return;
        }

        switch (medal)
        {
            case "GOLD":
                medalImage.sprite = goldTrophySprite;
                break;
            case "SILVER":
                medalImage.sprite = silverTrophySprite;
                break;
            default:
                medalImage.sprite = bronzeTrophySprite;
                break;
        }
    }

    private void Restart()
    {
        LoadStoredScene(ResultsSession.RestartScene);
    }

    private void Continue()
    {
        // The final level's Continue is an ending, rather than another level.
        // Preserve a one-shot request across the scene load so the menu opens
        // its existing CreditsPanel instead of its usual landing panel.
        if (ResultsSession.RestartScene == "Elliot_Level4" &&
            ResultsSession.ContinueScene == "Cayla_MainMenu")
        {
            MainMenuCreditsPanelActivator.Request();
        }

        LoadStoredScene(ResultsSession.ContinueScene);
    }

    private void LevelSelect()
    {
        string destination = ResultsSession.LevelSelectScene;
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        MainMenuController.RequestLevelSelect();
        ResultsSession.Clear();
        SceneManager.LoadScene(destination);
    }

    private static void LoadStoredScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        ResultsSession.Clear();
        SceneManager.LoadScene(sceneName);
    }
}
