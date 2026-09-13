using UnityEngine;

public enum TutorialSectionType
{
    FirstLamp,
    FirstPuddle,
    PuddleCleared,
    GustLamp
}

[RequireComponent(typeof(Collider2D))]
public sealed class TutorialSectionTrigger : MonoBehaviour
{
    [SerializeField] private TutorialSectionType sectionType;
    [SerializeField] private TutorialDirector director;
    private bool triggered;
    public bool IsTriggered => triggered;

    public void RestoreState(bool wasTriggered)
    {
        triggered = wasTriggered;
        gameObject.SetActive(!wasTriggered || sectionType == TutorialSectionType.PuddleCleared);
    }

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    public void Configure(TutorialSectionType type, TutorialDirector tutorial)
    {
        sectionType = type;
        director = tutorial;
    }

    public void ResetTrigger()
    {
        triggered = false;
        gameObject.SetActive(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        triggered = true;
        director?.EnterSection(sectionType);

        if (sectionType != TutorialSectionType.PuddleCleared)
        {
            gameObject.SetActive(false);
        }
    }
}
