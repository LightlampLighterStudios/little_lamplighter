using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Controls the PauseMenu prefab placed in gameplay scenes by the patch
/// installer. All visual references are serialized in that prefab; no
/// Resources assets or runtime asset-path loading are used.
/// </summary>
public sealed class PauseMenuController : MonoBehaviour
{
    private const string MainMenuScene = "Cayla_MainMenu";
    private static readonly Color SelectedButtonColor = new Color(1f, 0.72f, 0.2f, 1f);
    private static readonly Color UnselectedButtonColor = new Color(0.88f, 0.84f, 0.75f, 1f);

    [SerializeField] private GameObject overlay;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    private GameManager manager;
    private DarknessController darkness;
    private bool paused;
    private float savedTimeScale = 1f;
    private bool worldWasRunning;
    private bool darknessWasPaused;

#if ENABLE_INPUT_SYSTEM
    private PlayerInput gameplayInput;
    private bool gameplayInputWasActive;
#endif

    // Called once by the prefab authoring utility. Keeping the references on
    // the prefab lets a built player use ordinary Sprite and Font references.
    public void Configure(GameObject overlayObject, Button resume, Button restart, Button levelSelect)
    {
        overlay = overlayObject;
        resumeButton = resume;
        restartButton = restart;
        levelSelectButton = levelSelect;
    }

    private void Awake()
    {
        manager = GetComponentInParent<GameManager>();
        if (manager == null)
        {
            manager = FindFirstObjectByType<GameManager>();
        }

        overlay.SetActive(false);
        resumeButton.onClick.AddListener(Resume);
        restartButton.onClick.AddListener(Restart);
        levelSelectButton.onClick.AddListener(ReturnToLevelSelect);
        ConfigureButtonSounds();
        PrepareSelection(resumeButton);
        PrepareSelection(restartButton);
        PrepareSelection(levelSelectButton);
        ConfigureVerticalNavigation();
        SetSelectedButton(resumeButton);
        EnsureEventSystem();
    }

    private void Update()
    {
        if (!PausePressed() || (manager != null && manager.IsLevelEnded))
        {
            return;
        }

        if (paused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    private static bool PausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    public void Pause()
    {
        if (paused || (manager != null && manager.IsLevelEnded))
        {
            return;
        }

        savedTimeScale = Time.timeScale;
        worldWasRunning = manager != null && manager.IsClockRunning;
        manager?.PauseWorld();

        darkness = FindFirstObjectByType<DarknessController>();
        darknessWasPaused = darkness != null && darkness.IsMotionPaused;
        if (darkness != null && !darknessWasPaused)
        {
            darkness.SetMotionPaused(true);
        }

#if ENABLE_INPUT_SYSTEM
        // Time.timeScale stops simulation, but Input System callbacks still run
        // during a pause. Suspend the gameplay map while the UI stays active.
        gameplayInput = FindFirstObjectByType<PlayerInput>();
        gameplayInputWasActive = gameplayInput != null && gameplayInput.inputIsActive;
        if (gameplayInputWasActive)
        {
            gameplayInput.DeactivateInput();
        }
#endif

        Time.timeScale = 0f;
        paused = true;
        overlay.SetActive(true);
        SetSelectedButton(resumeButton);
        resumeButton.GetComponent<ButtonSfx>()?.SuppressNextSelectionSound();
        EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void Resume()
    {
        if (!paused)
        {
            return;
        }

        RestoreGameplayAfterPause();
        paused = false;
        overlay.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void Restart()
    {
        Resume();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ReturnToLevelSelect()
    {
        Resume();
        if (Application.CanStreamedLevelBeLoaded(MainMenuScene))
        {
            MainMenuController.RequestLevelSelect();
            SceneManager.LoadScene(MainMenuScene);
        }
        else
        {
            Debug.LogWarning("Pause menu could not find Cayla_MainMenu in Build Settings.", this);
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject system = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        system.AddComponent<InputSystemUIInputModule>();
#else
        system.AddComponent<StandaloneInputModule>();
#endif
    }

    private void ConfigureButtonSounds()
    {
        if (hoverSound == null || clickSound == null)
        {
            Debug.LogWarning("Pause menu button sounds are not assigned.", this);
            return;
        }

        UISoundManager soundManager = UISoundManager.instance;
        if (soundManager == null)
        {
            GameObject soundHost = new GameObject("Pause Menu UI Sound Manager");
            soundHost.transform.SetParent(transform, false);
            soundManager = soundHost.AddComponent<UISoundManager>();
            soundManager.Configure(CreateUiAudioSource(soundHost, hoverSound), CreateUiAudioSource(soundHost, clickSound));
        }

        AddButtonSfx(resumeButton);
        AddButtonSfx(restartButton);
        AddButtonSfx(levelSelectButton);
    }

    private static AudioSource CreateUiAudioSource(GameObject host, AudioClip clip)
    {
        AudioSource source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private static void AddButtonSfx(Button button)
    {
        if (button != null && button.GetComponent<ButtonSfx>() == null)
        {
            button.gameObject.AddComponent<ButtonSfx>();
        }
    }

    private void ConfigureVerticalNavigation()
    {
        ConfigureNavigation(resumeButton, null, restartButton);
        ConfigureNavigation(restartButton, resumeButton, levelSelectButton);
        ConfigureNavigation(levelSelectButton, restartButton, null);
    }

    private static void ConfigureNavigation(Button button, Button up, Button down)
    {
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.wrapAround = false;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        navigation.selectOnLeft = null;
        navigation.selectOnRight = null;
        button.navigation = navigation;
    }

    private void RestoreGameplayAfterPause()
    {
        Time.timeScale = savedTimeScale;

        // Resume only if the player opened pause while the route was moving;
        // do not override a tutorial, checkpoint, or results-screen stop.
        if (worldWasRunning)
        {
            manager?.ResumeWorld();
        }
        worldWasRunning = false;

        if (darkness != null && !darknessWasPaused)
        {
            darkness.SetMotionPaused(false);
        }
        darkness = null;
        darknessWasPaused = false;

#if ENABLE_INPUT_SYSTEM
        if (gameplayInputWasActive && gameplayInput != null)
        {
            gameplayInput.ActivateInput();
        }
        gameplayInput = null;
        gameplayInputWasActive = false;
#endif
    }

    private void PrepareSelection(Button button)
    {
        // Button tinting would otherwise replace the authored gold/grey base
        // color. Leave the Image color under this controller's ownership.
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        AddSelectionTrigger(trigger, EventTriggerType.Select, button, false);
        AddSelectionTrigger(trigger, EventTriggerType.PointerEnter, button, true);
        AddSelectionTrigger(trigger, EventTriggerType.PointerDown, button, true);
    }

    private void AddSelectionTrigger(EventTrigger trigger, EventTriggerType type, Button button, bool selectInEventSystem)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ =>
        {
            if (!paused)
            {
                return;
            }

            SetSelectedButton(button);
            if (selectInEventSystem && EventSystem.current.currentSelectedGameObject != button.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
        });
        trigger.triggers.Add(entry);
    }

    private void SetSelectedButton(Button selected)
    {
        SetButtonColor(resumeButton, selected == resumeButton ? SelectedButtonColor : UnselectedButtonColor);
        SetButtonColor(restartButton, selected == restartButton ? SelectedButtonColor : UnselectedButtonColor);
        SetButtonColor(levelSelectButton, selected == levelSelectButton ? SelectedButtonColor : UnselectedButtonColor);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button != null && button.targetGraphic is Image image)
        {
            image.color = color;
        }
    }

    private void OnDestroy()
    {
        if (paused)
        {
            RestoreGameplayAfterPause();
        }
    }
}
