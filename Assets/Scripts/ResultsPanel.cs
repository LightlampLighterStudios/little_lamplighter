using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ResultsPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text medalText;
    [SerializeField] private Text statisticsText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private PlayerController player;
    [SerializeField] private bool levelOneDemoOnly;
    [SerializeField] private string continueScene = "Elliot_Level2";
    [SerializeField] private string levelSelectScene = "TemporaryLevelSelect";
    [SerializeField, Min(0f)] private float runOffDuration = 1.1f;
    [SerializeField, Min(0f)] private float runOffSpeed = 12f;

    private void Awake()
    {
        restartButton?.onClick.AddListener(Restart);
        continueButton?.onClick.AddListener(Continue);
        levelSelectButton?.onClick.AddListener(LevelSelect);
        panel?.SetActive(false);
    }

    public void Configure(
        GameObject rootPanel,
        Text medal,
        Text stats,
        Button restart,
        Button continueToNext,
        Button returnToSelect,
        PlayerController targetPlayer,
        string nextScene = "Elliot_Level2",
        string selectScene = "TemporaryLevelSelect")
    {
        panel = rootPanel;
        medalText = medal;
        statisticsText = stats;
        restartButton = restart;
        continueButton = continueToNext;
        levelSelectButton = returnToSelect;
        player = targetPlayer;
        continueScene = nextScene;
        levelSelectScene = selectScene;

        if (panel != null)
        {
            titleText = panel.transform.Find("Title")?.GetComponent<Text>();
        }
    }

    public void Show(GameManager manager)
    {
        StartCoroutine(ShowRoutine(manager));
    }

    public void ShowFailure(GameManager manager)
    {
        player?.SetControlsEnabled(false);

        if (titleText != null)
        {
            titleText.text = "THE DARKNESS CAUGHT YOU";
        }

        if (medalText != null)
        {
            medalText.text = "ROUTE FAILED";
        }

        if (statisticsText != null)
        {
            statisticsText.text =
                $"Lamps lit: {manager.UniqueLampsLit}/{manager.TotalLamps}\n" +
                $"Sparks earned: {manager.Sparks}\n" +
                "Orbs remaining: 0/3";
        }

        continueButton?.gameObject.SetActive(false);
        levelSelectButton?.gameObject.SetActive(!levelOneDemoOnly);
        panel?.SetActive(true);
    }

    private IEnumerator ShowRoutine(GameManager manager)
    {
        if (titleText != null)
        {
            titleText.text = "LEVEL COMPLETE";
        }

        continueButton?.gameObject.SetActive(!levelOneDemoOnly);
        levelSelectButton?.gameObject.SetActive(!levelOneDemoOnly);

        if (player != null)
        {
            player.SetControlsEnabled(false);
            float elapsed = 0f;

            while (elapsed < runOffDuration)
            {
                elapsed += Time.deltaTime;
                player.transform.position += Vector3.right * runOffSpeed * Time.deltaTime;
                yield return null;
            }
        }

        if (medalText != null)
        {
            medalText.text = manager.GetMedal() + " MEDAL";
        }

        if (statisticsText != null)
        {
            statisticsText.text =
                $"Lamps lit: {manager.UniqueLampsLit}/{manager.TotalLamps}\n" +
                $"Sparks earned: {manager.Sparks}\n" +
                $"Orbs remaining: {manager.Lives}/3";
        }

        panel?.SetActive(true);
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Continue()
    {
        SceneManager.LoadScene(continueScene);
    }

    public void LevelSelect()
    {
        SceneManager.LoadScene(levelSelectScene);
    }
}
