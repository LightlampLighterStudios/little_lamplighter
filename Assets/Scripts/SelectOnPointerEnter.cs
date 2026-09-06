using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public sealed class SelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler
{
    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
        }

        if (
            selectable == null ||
            !selectable.IsInteractable() ||
            (eventData == null && EventSystem.current == null))
        {
            return;
        }

        if (eventData != null)
        {
            eventData.selectedObject = gameObject;
        }
        else if (EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
