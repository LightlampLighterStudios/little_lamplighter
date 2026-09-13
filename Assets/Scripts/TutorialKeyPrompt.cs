using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>A non-interactive HUD prompt that follows an authored world anchor.</summary>
public sealed class TutorialKeyPrompt : MonoBehaviour
{
    public enum Lesson { Move, Light, Jump, Relight }
    [SerializeField] private CanvasGroup group;
    [SerializeField] private CanvasGroup keys;
    [SerializeField] private Image firstKey;
    [SerializeField] private Image secondKey;
    [SerializeField] private Text firstFallback;
    [SerializeField] private Text secondFallback;
    [SerializeField] private Text verb;
    [SerializeField] private Image cue;
    [SerializeField] private Image progress;
    [SerializeField] private Sprite leftUp, leftDown, rightUp, rightDown;
    [SerializeField] private Sprite jumpUp, jumpDown, enterUp, enterDown;
    [Header("Prompt Timing")]
    [Tooltip("Seconds before Enter appears for HOLD/RELIGHT. MOVE is always immediate.")]
    [SerializeField] private float keyDelay = 2.5f;
    [Tooltip("Seconds before HOLD/RELIGHT text appears. MOVE is always immediate.")]
    [SerializeField] private float verbDelay = 5f;
    [Tooltip("Seconds before Space appears. Use zero for immediate.")]
    [SerializeField] private float jumpKeyDelay = 0f;
    [Tooltip("Seconds before JUMP appears. Use zero for immediate.")]
    [SerializeField] private float jumpVerbDelay = 0f;
    [SerializeField] private float fadeSeconds = 0.2f;
    [Header("Prompt Text")]
    [SerializeField] private Color labelColour = new Color(.08f, .055f, .13f, 1f);
    [SerializeField] private Color labelOutlineColour = new Color(.96f, .9f, .75f, .95f);
    [SerializeField, Range(0f, 2f)] private float labelOutlinePixels = 1f;

    private Transform anchor;
    private Vector3 offset;
    private PlayerController player;
    private LampController lamp;
    private InputAction moveAction, jumpAction, lightAction;
    private Canvas canvas;
    private RectTransform rect;
    private Lesson lesson;
    private bool showing;
    private bool hudPresentation;
    private float idleTime, jumpFlash;
    private Camera worldCamera;
    private Outline verbOutline;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>().rootCanvas;
        worldCamera = Camera.main;
        group.alpha = 0;
        group.blocksRaycasts = false;
        group.interactable = false;
        verb.fontStyle = FontStyle.Bold;
        verb.fontSize = 30;
        verb.resizeTextMaxSize = 30;
        verbOutline = verb.gameObject.AddComponent<Outline>();
        verbOutline.effectColor = labelOutlineColour;
        verbOutline.effectDistance = new Vector2(labelOutlinePixels, -labelOutlinePixels);
        AddKeyShadow(firstKey);
        AddKeyShadow(secondKey);
        // A plain UI rectangle has no sprite mesh for Image.fillAmount to clip.
        progress.type = Image.Type.Simple;
        progress.rectTransform.anchorMin = progress.rectTransform.anchorMax = new Vector2(0, .5f);
        progress.rectTransform.pivot = new Vector2(0, .5f);
        progress.rectTransform.anchoredPosition = Vector2.zero;
    }

    public void Show(Lesson kind, Transform target, Vector3 worldOffset,
        PlayerController targetPlayer, LampController targetLamp = null)
    {
        if (showing && lesson == kind && anchor == target) return;
        lesson = kind;
        anchor = target;
        offset = worldOffset;
        // MOVE can use a fixed HUD position when no anchor is supplied, or an
        // authored world position when the director supplies its start anchor.
        hudPresentation = kind == Lesson.Move && target == null;
        player = targetPlayer;
        lamp = targetLamp;
        var input = player.GetComponent<PlayerInput>();
        moveAction = input != null ? input.actions.FindAction("Move") : null;
        jumpAction = input != null ? input.actions.FindAction("Jump") : null;
        lightAction = input != null ? input.actions.FindAction("Interact") : null;
        showing = true;
        idleTime = jumpFlash = 0;
        firstFallback.gameObject.SetActive(false);
        secondFallback.gameObject.SetActive(false);
        bool keyImmediate = kind == Lesson.Move || (kind == Lesson.Jump && jumpKeyDelay <= 0);
        bool labelImmediate = kind == Lesson.Move || (kind == Lesson.Jump && jumpVerbDelay <= 0);
        keys.alpha = keyImmediate ? 1 : 0;
        verb.text = kind == Lesson.Move ? "MOVE" : kind == Lesson.Jump ? "JUMP" :
            kind == Lesson.Relight ? "RELIGHT" : "HOLD";
        verb.color = new Color(labelColour.r, labelColour.g, labelColour.b, labelImmediate ? 1 : 0);
        secondKey.gameObject.SetActive(kind == Lesson.Move);
        firstKey.rectTransform.anchoredPosition = new Vector2(kind == Lesson.Move ? -40 : 0, 0);
        firstKey.rectTransform.sizeDelta = new Vector2(kind == Lesson.Jump ? 128 : 72, 84);
        // The key artwork is the only input indicator; no box or hold-progress bar.
        progress.transform.parent.gameObject.SetActive(false);
        cue.gameObject.SetActive(kind != Lesson.Move && !hudPresentation);
        rect.anchorMin = rect.anchorMax = hudPresentation ? new Vector2(.5f, 0) : new Vector2(.5f, .5f);
        rect.pivot = hudPresentation ? new Vector2(.5f, 0) : new Vector2(.5f, .5f);
        rect.anchoredPosition = hudPresentation ? new Vector2(0, 56) : Vector2.zero;
    }

    public void Hide() { showing = false; }

    private void LateUpdate()
    {
        // Scaled time keeps escalation, key flashes and fading frozen in the pause menu.
        float dt = Time.deltaTime;
        bool visible = showing && (hudPresentation || (anchor != null && worldCamera != null));
        if (!hudPresentation && anchor != null && worldCamera != null)
        {
            Vector3 screen = worldCamera.WorldToScreenPoint(anchor.position + offset);
            visible &= screen.z > 0 && screen.x >= 0 && screen.x <= Screen.width &&
                screen.y >= 0 && screen.y <= Screen.height;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out Vector2 local))
            {
                // Clamp in canvas units; works with CanvasScaler and different aspect ratios.
                Rect area = ((RectTransform)canvas.transform).rect;
                local.x = Mathf.Clamp(local.x, area.xMin + 115, area.xMax - 115);
                local.y = Mathf.Clamp(local.y, area.yMin + 95, area.yMax - 135);
                // localPosition also supports the existing HUD's bottom-left pivot.
                rect.localPosition = (Vector3)(local + Vector2.up * (Mathf.Sin(Time.time * 2.4f) * 4));
            }
        }
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1 : 0, dt / Mathf.Max(.01f, fadeSeconds));
        if (!showing || player == null) return;

        bool activity = lesson == Lesson.Move ? Mathf.Abs(player.MoveInput) > .1f :
            lesson == Lesson.Jump ? jumpAction != null && jumpAction.WasPressedThisFrame() :
            lamp != null && lamp.LightingProgress > 0;
        // Once help is visible it persists until success, even after a failed input attempt.
        float keyRevealTime = lesson == Lesson.Move ? 0 : lesson == Lesson.Jump ? jumpKeyDelay : keyDelay;
        float verbRevealTime = lesson == Lesson.Move ? 0 : lesson == Lesson.Jump ? jumpVerbDelay : verbDelay;
        if (!activity || idleTime >= keyRevealTime) idleTime += dt;
        else idleTime = 0;
        keys.alpha = Mathf.MoveTowards(keys.alpha, idleTime >= keyRevealTime ? 1 : 0, dt / .25f);
        Color visibleColour = verb.color;
        visibleColour.a = Mathf.MoveTowards(visibleColour.a, idleTime >= verbRevealTime ? 1 : 0, dt / .25f);
        verb.color = visibleColour;
        cue.color = new Color(1f, .81f, .35f, .45f + .25f * Mathf.Sin(Time.time * 3));

        if (lesson == Lesson.Move)
        {
            float x = moveAction != null ? moveAction.ReadValue<Vector2>().x : 0;
            DrawKey(firstKey, leftUp, leftDown, x < -.1f);
            DrawKey(secondKey, rightUp, rightDown, x > .1f);
            float pulse = Mathf.Abs(x) > .1f ? 1f : 1f + .045f * (.5f + .5f * Mathf.Sin(Time.time * 4f));
            keys.transform.localScale = Vector3.one * pulse;
        }
        else if (lesson == Lesson.Jump)
        {
            keys.transform.localScale = Vector3.one;
            if (jumpAction != null && jumpAction.WasPressedThisFrame()) jumpFlash = .18f;
            jumpFlash = Mathf.Max(0, jumpFlash - dt);
            DrawKey(firstKey, jumpUp, jumpDown, jumpFlash > 0);
        }
        else
        {
            keys.transform.localScale = Vector3.one;
            DrawKey(firstKey, enterUp, enterDown != null ? enterDown : enterUp,
                lightAction != null && lightAction.IsPressed());
            float amount = lamp != null ? lamp.LightingProgress : 0;
            float width = ((RectTransform)progress.transform.parent).rect.width;
            progress.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * amount);
        }
    }

    private static void AddKeyShadow(Image key)
    {
        Shadow shadow = key.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.02f, .025f, .06f, .85f);
        shadow.effectDistance = new Vector2(4, -5);
    }

    private static void DrawKey(Image image, Sprite up, Sprite down, bool pressed)
    {
        image.sprite = pressed ? down : up;
        image.color = Color.white;
        image.rectTransform.localScale = Vector3.one * (pressed ? .90f : 1f);
        Vector2 position = image.rectTransform.anchoredPosition;
        position.y = pressed ? -5 : 0;
        image.rectTransform.anchoredPosition = position;
    }

}
