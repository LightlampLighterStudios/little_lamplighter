using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject levelsPanel;
    [SerializeField] private Button levelsButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button firstLevelButton;

    private static bool openLevelSelectOnLoad;

    public static void RequestLevelSelect()
    {
        openLevelSelectOnLoad = true;
    }

    private void Awake()
    {
        levelsButton?.onClick.AddListener(ShowLevelSelect);
        backToMenuButton?.onClick.AddListener(ShowMainMenu);
    }

    private void Start()
    {
        if (!openLevelSelectOnLoad)
        {
            return;
        }

        openLevelSelectOnLoad = false;
        ShowLevelSelect();
    }

    public void ShowLevelSelect()
    {
        mainMenuPanel?.SetActive(false);
        levelsPanel?.SetActive(true);

        if (firstLevelButton != null && firstLevelButton.gameObject.activeInHierarchy)
        {
            EventSystem.current?.SetSelectedGameObject(firstLevelButton.gameObject);
        }
    }

    public void ShowMainMenu()
    {
        levelsPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
    }
}
