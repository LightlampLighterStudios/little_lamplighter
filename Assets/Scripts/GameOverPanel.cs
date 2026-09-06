using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Reusable failure-only overlay. Successful results remain owned by ResultsPanel.
public sealed class GameOverPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup presentation;
    [SerializeField] private Text lampsText;
    [SerializeField] private Text sparksText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField, Min(0f)] private float fadeDuration = 0.45f;
    [SerializeField] private GameOverDarknessTransition darknessTransition;
    [SerializeField] private RectTransform card;
    [SerializeField, Min(0f)] private float entranceLift = 38f;
    [SerializeField, Range(0.8f, 1f)] private float entranceScale = 0.97f;
    private Vector2 cardHome;
    private Font systemSerif;

    private string restartScene;
    private string levelSelectScene;
    private bool shown;
    private bool navigating;
    private bool ownsPause;
    private float previousTimeScale;
    private InputSystemUIInputModule uiInput;
    private PlayerInput gameplayInput;

    private void Awake()
    {
        // Use the reference's serif when it is installed; no system font files
        // are copied or redistributed. The authored Unity font is the fallback.
        if (Application.platform != RuntimePlatform.WebGLPlayer)
        {
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string candidate in new[] { "Georgia", "Liberation Serif", "DejaVu Serif" })
            {
                if (System.Array.IndexOf(installed, candidate) < 0) continue;
                systemSerif = Font.CreateDynamicFontFromOSFont(candidate, 48);
                foreach (Text label in GetComponentsInChildren<Text>(true)) label.font = systemSerif;
                break;
            }
        }
        if (card != null) cardHome = card.anchoredPosition;
        presentation.alpha = 0f;
        presentation.interactable = false;
        presentation.blocksRaycasts = true;
        restartButton.onClick.AddListener(Restart);
        levelSelectButton.onClick.AddListener(ReturnToLevelSelect);
    }

    public void Show(GameManager manager, string selectScene)
    {
        if (shown || manager == null)
        {
            return;
        }

        shown = true;
        restartScene = SceneManager.GetActiveScene().path;
        levelSelectScene = selectScene;
        lampsText.text = $"{manager.UniqueLampsLit} / {manager.TotalLamps}";
        sparksText.text = manager.Sparks.ToString();
        uiInput = FindFirstObjectByType<InputSystemUIInputModule>();
        gameplayInput = FindFirstObjectByType<PlayerInput>();
        EventSystem.current?.SetSelectedGameObject(null);

        // Stop physics, gameplay timers and animations, while the UI fades in real time.
        previousTimeScale = Time.timeScale;
        ownsPause = true;
        Time.timeScale = 0f;
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        if (darknessTransition != null)
        {
            yield return darknessTransition.PlayTakeover();
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            presentation.alpha = eased;
            if (card != null)
            {
                card.anchoredPosition = cardHome + Vector2.down * entranceLift * (1f - eased);
                card.localScale = Vector3.one * Mathf.Lerp(entranceScale, 1f, eased);
            }
            yield return null;
        }
        presentation.alpha = 1f;
        if (card != null)
        {
            card.anchoredPosition = cardHome;
            card.localScale = Vector3.one;
        }

        // Require release of the controls that were held on death. This also covers
        // a click during the fade, remapped Submit actions and controller buttons.
        do
        {
            yield return null;
        }
        while (AnyActivationHeld());

        presentation.interactable = true;
        EventSystem.current?.SetSelectedGameObject(restartButton.gameObject);
    }

    private bool AnyActivationHeld()
    {
        if (uiInput != null && (
            IsPressed(uiInput.submit) || IsPressed(uiInput.leftClick) ||
            IsPressed(uiInput.move)))
        {
            return true;
        }

        if (gameplayInput != null && gameplayInput.actions != null)
        {
            foreach (InputAction action in gameplayInput.actions)
            {
                // Pointer position is always nonzero; only buttons and movement
                // from the gameplay map should postpone keyboard/controller focus.
                if (action.type == InputActionType.Button ||
                    action.name == "Move" || action.name == "Interact")
                {
                    if (action.enabled && action.IsPressed()) return true;
                }
            }
        }

        Keyboard keyboard = Keyboard.current;
        return (keyboard != null && (
            keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed ||
            keyboard.spaceKey.isPressed || keyboard.eKey.isPressed)) ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
    }

    private static bool IsPressed(InputActionReference reference)
    {
        return reference != null && reference.action != null && reference.action.IsPressed();
    }

    public void Restart()
    {
        Navigate(restartScene);
    }

    public void ReturnToLevelSelect()
    {
        Navigate(levelSelectScene);
    }

    private void Navigate(string scene)
    {
        if (!shown || navigating || !presentation.interactable) return;
        if (string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"Game over destination is not in Build Settings: {scene}", this);
            return;
        }

        navigating = true;
        presentation.interactable = false;
        ReleasePause();
        SceneManager.LoadScene(scene);
    }

    private void ReleasePause()
    {
        if (!ownsPause) return;
        Time.timeScale = previousTimeScale;
        ownsPause = false;
    }

    private void OnDestroy()
    {
        ReleasePause();
        if (systemSerif != null) Destroy(systemSerif);
    }
}
