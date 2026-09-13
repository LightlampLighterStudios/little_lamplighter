using UnityEngine;
using UnityEngine.UI;

public sealed class GameHudController : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Text sparkText;
    [SerializeField] private Image[] lifeOrbs;
    [SerializeField] private RectTransform lampProgressRoot;
    [SerializeField] private Image lampProgressFill;
    [SerializeField] private Image boneIcon;
    [SerializeField] private GameObject dogRecoveryPrompt;
    [SerializeField, Min(0f)] private float lampProgressClearance = 18f;

    private LampController trackedLamp;

    private void LateUpdate()
    {
        if (trackedLamp == null || lampProgressRoot == null || Camera.main == null) return;

        Vector3 worldAnchor = GetLampTop(trackedLamp);
        Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldAnchor);
        Vector2 clearance = Vector2.up * lampProgressClearance;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform, screenPoint, canvas.worldCamera, out Vector2 localPoint))
                lampProgressRoot.anchoredPosition = localPoint + clearance;
        }
        else lampProgressRoot.position = screenPoint + clearance;
    }

    private static Vector3 GetLampTop(LampController lamp)
    {
        SpriteRenderer[] renderers = lamp.GetComponentsInChildren<SpriteRenderer>(true);
        bool found = false;
        Bounds bounds = default;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (!renderer.enabled || renderer.sprite == null) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (!found) return lamp.transform.position;
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    public void Configure(Canvas targetCanvas, Text sparks, Image[] orbs,
        RectTransform progressRoot, Image progressFill, Image carriedBoneIcon = null,
        GameObject recoveryPrompt = null)
    {
        canvas = targetCanvas;
        sparkText = sparks;
        lifeOrbs = orbs;
        lampProgressRoot = progressRoot;
        lampProgressFill = progressFill;
        boneIcon = carriedBoneIcon;
        dogRecoveryPrompt = recoveryPrompt;
        HideLampProgress();
        SetBoneCarried(false);
        SetDogRecoveryPrompt(false);
    }

    public void SetSparks(int amount)
    {
        if (sparkText != null) sparkText.text = amount.ToString();
    }

    public void SetLives(int amount)
    {
        if (lifeOrbs == null) return;
        for (int index = 0; index < lifeOrbs.Length; index++)
        {
            if (lifeOrbs[index] == null) continue;
            Color colour = lifeOrbs[index].color;
            colour.a = index < amount ? 1f : .2f;
            lifeOrbs[index].color = colour;
        }
    }

    public void ShowLampProgress(LampController lamp, float progress)
    {
        trackedLamp = lamp;
        if (lampProgressRoot != null) lampProgressRoot.gameObject.SetActive(true);
        if (lampProgressFill == null) return;
        lampProgressFill.fillAmount = Mathf.Clamp01(progress);
        if (lamp != null) lampProgressFill.color = lamp.ProgressColour;
    }

    public void HideLampProgress()
    {
        trackedLamp = null;
        if (lampProgressRoot != null) lampProgressRoot.gameObject.SetActive(false);
    }

    public void SetBoneCarried(bool carried) => boneIcon?.gameObject.SetActive(carried);
    public void SetDogRecoveryPrompt(bool visible) => dogRecoveryPrompt?.SetActive(visible);
}
