using UnityEngine;
using UnityEngine.EventSystems;

// Attach to any UI Button to give it hover/click sound via the shared
// UISoundManager - no per-button AudioSource, no wiring into OnClick().
public sealed class ButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        UISoundManager.instance?.PlayHover();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        UISoundManager.instance?.PlayClick();
    }
}
