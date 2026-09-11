using UnityEngine;
using UnityEngine.UI;

// New script redesigned Results panel (from Cayla if one wants to use it this way)
// Important notte I made it so so that it does NOT touch GameManager, ResultsPanel, or any other teammate
// script ; I made it so it only READS the finished level's numbers from GameManager
// (lamps lit, sparks, medal) and uses them to update this panel's own
// visuals: the LampsValue text, the SparksValue text, and the trophy
// image + word.
//
// This runs every time the panel becomes visible (OnEnable), so it
// always shows the real result for whatever level it's sitting in -
// this same script and panel can be copied into all 4 level scenes.
public sealed class ResultsDisplay : MonoBehaviour
{
    [Header("Lamps row")]
    [SerializeField] private Text lampsValueText;

    [Header("Sparks row")]


    [Header("Trophy")]
    [SerializeField] private Image medalImage;
    [SerializeField] private Text medalText;
    [SerializeField] private Sprite goldTrophySprite;
    [SerializeField] private Sprite silverTrophySprite;
    [SerializeField] private Sprite bronzeTrophySprite;

    private void OnEnable()
    {
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        GameManager manager = GameManager.instance;

        if (manager == null)
        {
            // This is expected/normal if you press Play directly inside
            // the Cayla_Results scene by itself so there is no GameManager
            // here, only inside an actual level scene. See the note in
            // the instructions about testing this properly.
            Debug.LogWarning(
                "ResultsDisplay: no GameManager found in this scene.",
                this);
            return;
        }

        if (lampsValueText != null)
        {
            lampsValueText.text = $"{manager.UniqueLampsLit}/{manager.TotalLamps}";
        }

        if (sparksValueText != null)
        {
            sparksValueText.text = manager.Sparks.ToString();
        }

        // GetMedal() already exists on GameManager (Soulies
        // script) and returns "GOLD", "SILVER", or "BRONZE".
        string medal = manager.GetMedal();

        if (medalText != null)
        {
            medalText.text = medal;
        }

        if (medalImage != null)
        {
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
    }
}