using UnityEngine;
using UnityEngine.EventSystems;

// Attach to any UI Button to give it hover/click sound via the shared
// UISoundManager - no per-button AudioSource, no wiring into OnClick().
public sealed class ButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISelectHandler, ISubmitHandler
{
    // Pointer-hover normally selects the same button in the same EventSystem
    // update. Keep that mouse path to one sound while still playing a hover
    // sound for keyboard/controller selection changes.
    private const float HoverDedupWindow = 0.05f;
    private float lastHoverSoundTime = float.NegativeInfinity;
    private bool suppressNextSelectionSound;

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayHoverOnce();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (suppressNextSelectionSound)
        {
            suppressNextSelectionSound = false;
            return;
        }

        PlayHoverOnce();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClick();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClick();
    }

    private void PlayHoverOnce()
    {
        if (Time.unscaledTime - lastHoverSoundTime < HoverDedupWindow)
        {
            return;
        }

        lastHoverSoundTime = Time.unscaledTime;
        UISoundManager.instance?.PlayHover();
    }

    private static void PlayClick()
    {
        UISoundManager.instance?.PlayClick();
    }

    // Menu setup calls this immediately before assigning the default focused
    // button. That focus is not user navigation and should remain silent.
    public void SuppressNextSelectionSound()
    {
        suppressNextSelectionSound = true;
    }
}
