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
    [SerializeField] private Vector2 lampProgressOffset = new Vector2(0f, -55f);

    private LampController trackedLamp;

    private void LateUpdate()
    {
        if (trackedLamp == null || lampProgressRoot == null)
        {
            return;
        }

        Vector2 screenPoint = Camera.main.WorldToScreenPoint(trackedLamp.transform.position);

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            Camera eventCamera = canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                screenPoint,
                eventCamera,
                out screenPoint);
        }

        lampProgressRoot.position = screenPoint + lampProgressOffset;
    }

    public void Configure(
        Canvas targetCanvas,
        Text sparks,
        Image[] orbs,
        RectTransform progressRoot,
        Image progressFill,
        Image carriedBoneIcon = null,
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
        if (sparkText != null)
        {
            sparkText.text = amount.ToString();
        }
    }

    public void SetLives(int amount)
    {
        if (lifeOrbs == null)
        {
            return;
        }

        for (int index = 0; index < lifeOrbs.Length; index++)
        {
            if (lifeOrbs[index] != null)
            {
                Color colour = lifeOrbs[index].color;
                colour.a = index < amount ? 1f : 0.2f;
                lifeOrbs[index].color = colour;
            }
        }
    }

    public void ShowLampProgress(LampController lamp, float progress)
    {
        trackedLamp = lamp;

        if (lampProgressRoot != null)
        {
            lampProgressRoot.gameObject.SetActive(true);
        }

        if (lampProgressFill != null)
        {
            lampProgressFill.fillAmount = Mathf.Clamp01(progress);
        }
    }

    public void HideLampProgress()
    {
        trackedLamp = null;

        if (lampProgressRoot != null)
        {
            lampProgressRoot.gameObject.SetActive(false);
        }
    }

    public void SetBoneCarried(bool carried)
    {
        boneIcon?.gameObject.SetActive(carried);
    }

    public void SetDogRecoveryPrompt(bool visible)
    {
        dogRecoveryPrompt?.SetActive(visible);
    }
}
