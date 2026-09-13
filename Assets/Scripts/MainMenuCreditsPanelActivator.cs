using UnityEngine;
using UnityEngine.SceneManagement;

// Added to the Cayla_MainMenu controller by the installer. Its static flag is
// set only by Level 4's ResultsDisplay Continue action before loading the menu.
public sealed class MainMenuCreditsPanelActivator : MonoBehaviour
{
    private static bool pendingCredits;

    public static void Request()
    {
        pendingCredits = true;
    }

    private void Start()
    {
        if (!pendingCredits || SceneManager.GetActiveScene().name != "Cayla_MainMenu")
        {
            return;
        }

        pendingCredits = false;
        SetPanelActive("MainMenuPanel", false);
        SetPanelActive("LevelsPanel", false);
        SetPanelActive("CreditsPanel", true);
    }

    private void SetPanelActive(string panelName, bool active)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject candidate in objects)
        {
            if (candidate.name == panelName && candidate.scene.handle == gameObject.scene.handle)
            {
                candidate.SetActive(active);
                return;
            }
        }

        Debug.LogWarning("Main-menu panel not found: " + panelName, this);
    }
}
