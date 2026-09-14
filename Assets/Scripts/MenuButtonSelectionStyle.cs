using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Matches PauseMenuController's selected/unselected treatment for standalone
// menus and the game-over card, without changing their layouts or listeners.
public sealed class MenuButtonSelectionStyle : MonoBehaviour, ISelectHandler, IPointerEnterHandler, IPointerDownHandler
{
    private static readonly Color SelectedColor = new Color(1f, 0.72f, 0.2f, 1f);
    private static readonly Color UnselectedColor = new Color(0.88f, 0.84f, 0.75f, 1f);
    private Button[] group;

    public void Configure(Button[] buttons)
    {
        group = buttons;
        Apply(null);
    }

    public void OnSelect(BaseEventData eventData) => Apply(GetComponent<Button>());
    public void OnPointerEnter(PointerEventData eventData) => SelectThis();
    public void OnPointerDown(PointerEventData eventData) => SelectThis();

    private void SelectThis()
    {
        Button button = GetComponent<Button>();
        if (button == null || !button.IsInteractable()) return;

        Apply(button);
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    private void Apply(Button selected)
    {
        if (group == null) return;
        foreach (Button button in group)
        {
            if (button == null) continue;

            // Some older menu buttons have a navy outer Image plus a child
            // Fill Image. Colour both layers so the outer image cannot read as
            // an unintended outline around the pause-menu style.
            Color color = button == selected ? SelectedColor : UnselectedColor;
            foreach (Image image in button.GetComponents<Image>()) image.color = color;
            if (button.targetGraphic is Image targetImage) targetImage.color = color;
        }
    }
}

public sealed class MenuButtonSelectionBootstrap : MonoBehaviour
{
    private readonly Dictionary<int, string> groupSignatures = new Dictionary<int, string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindFirstObjectByType<MenuButtonSelectionBootstrap>() != null) return;
        GameObject host = new GameObject("Menu Button Selection Style");
        DontDestroyOnLoad(host);
        host.AddComponent<MenuButtonSelectionBootstrap>();
    }

    private void Update() => ReconcileButtons();

    private void LateUpdate()
    {
        // The menu cards are right-anchored inside stretched parent panels.
        // Their visual position consequently varies with the Canvas Scaler's
        // runtime size unless the cards themselves, rather than their parents,
        // are horizontally centred after layout has finished. Vertical placement
        // remains authored in the scene.
        if (SceneManager.GetActiveScene().name != "Cayla_MainMenu") return;

        CentreMenuCard("MainMenuPanel", "MenuCard");
        CentreMenuCard("LevelsPanel", "LevelsCard");
        CentreMenuCard("CreditsPanel", "CreditsCard");
        CentreLogo();
    }

    private void ReconcileButtons()
    {
        var groups = new Dictionary<Canvas, List<Button>>();
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            // The pause menu already owns its own gold/grey selection state.
            if (!button.IsInteractable() || button.GetComponentInParent<PauseMenuController>() != null) continue;

            Canvas canvas = button.GetComponentInParent<Canvas>();
            if (canvas == null || HasNonInteractableCanvasGroup(button)) continue;
            if (!groups.TryGetValue(canvas, out List<Button> buttons))
            {
                buttons = new List<Button>();
                groups.Add(canvas, buttons);
            }
            buttons.Add(button);
        }

        foreach (KeyValuePair<Canvas, List<Button>> pair in groups)
        {
            List<Button> buttons = pair.Value;
            buttons.Sort(CompareScreenOrder);
            string signature = BuildSignature(buttons);
            int canvasId = pair.Key.GetInstanceID();
            if (groupSignatures.TryGetValue(canvasId, out string previous) && previous == signature) continue;
            groupSignatures[canvasId] = signature;

            Button[] group = buttons.ToArray();
            ConfigureMenuNavigation(buttons);
            foreach (Button button in group)
            {
                if (button.GetComponent<ButtonSfx>() == null)
                {
                    button.gameObject.AddComponent<ButtonSfx>();
                }

                MenuButtonSelectionStyle tracker = button.GetComponent<MenuButtonSelectionStyle>();
                if (tracker == null) tracker = button.gameObject.AddComponent<MenuButtonSelectionStyle>();
                tracker.Configure(group);
            }

            Button current = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject?.GetComponent<Button>()
                : null;
            Button defaultButton = GetDefaultButton(buttons);
            if (SceneManager.GetActiveScene().name == "Cayla_Results") Select(defaultButton);
            else if (current == null || !buttons.Contains(current)) Select(defaultButton);
            else current.GetComponent<MenuButtonSelectionStyle>()?.OnSelect(null);
        }
    }

    private static Button GetDefaultButton(List<Button> buttons)
    {
        if (SceneManager.GetActiveScene().name == "Cayla_Results")
        {
            foreach (Button button in buttons)
                if (button.name == "ContinueButton") return button;
        }

        return buttons[0];
    }

    // Authored scenes used explicit navigation, but several linked only the
    // first and last buttons. Rebuild active menus as a sequential list with
    // the direction matching the authored layout.
    private static void ConfigureMenuNavigation(List<Button> buttons)
    {
        bool isLevelCompleteMenu = SceneManager.GetActiveScene().name == "Cayla_Results";
        for (int index = 0; index < buttons.Count; index++)
        {
            Button button = buttons[index];
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = false;
            navigation.selectOnUp = isLevelCompleteMenu ? null : index > 0 ? buttons[index - 1] : null;
            navigation.selectOnDown = isLevelCompleteMenu ? null : index < buttons.Count - 1 ? buttons[index + 1] : null;
            navigation.selectOnLeft = isLevelCompleteMenu && index > 0 ? buttons[index - 1] : null;
            navigation.selectOnRight = isLevelCompleteMenu && index < buttons.Count - 1 ? buttons[index + 1] : null;
            button.navigation = navigation;
        }
    }

    private static bool HasNonInteractableCanvasGroup(Button button)
    {
        foreach (CanvasGroup group in button.GetComponentsInParent<CanvasGroup>(true))
            if (!group.interactable) return true;
        return false;
    }

    private static int CompareScreenOrder(Button left, Button right)
    {
        int vertical = right.transform.position.y.CompareTo(left.transform.position.y);
        return vertical != 0 ? vertical : left.transform.position.x.CompareTo(right.transform.position.x);
    }

    private static string BuildSignature(List<Button> buttons)
    {
        var signature = new System.Text.StringBuilder();
        foreach (Button button in buttons)
            signature.Append(button.GetInstanceID()).Append(':').Append(button.gameObject.activeInHierarchy).Append('|');
        return signature.ToString();
    }

    private static void Select(Button button)
    {
        button.GetComponent<ButtonSfx>()?.SuppressNextSelectionSound();
        button.GetComponent<MenuButtonSelectionStyle>()?.OnSelect(null);
        EventSystem.current?.SetSelectedGameObject(button.gameObject);
    }

    private static void CentreMenuCard(string panelName, string cardName)
    {
        RectTransform panel = FindSceneRectTransform(panelName);
        if (panel == null) return;

        RectTransform card = panel.Find(cardName) as RectTransform;
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        if (card == null || canvas == null) return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector3 canvasCentre = canvasRect.TransformPoint(canvasRect.rect.center);
        float offsetX = canvasCentre.x - card.position.x;
        panel.position += new Vector3(offsetX, 0f, 0f);
    }

    private static void CentreLogo()
    {
        RectTransform logo = FindSceneRectTransform("Logo");
        if (logo == null) return;

        Canvas canvas = logo.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        float offsetX = canvasRect.TransformPoint(canvasRect.rect.center).x - logo.position.x;
        logo.position += new Vector3(offsetX, 0f, 0f);
    }

    private static RectTransform FindSceneRectTransform(string objectName)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RectTransform transform in root.GetComponentsInChildren<RectTransform>(true))
                if (transform.name == objectName) return transform;
        }
        return null;
    }
}
