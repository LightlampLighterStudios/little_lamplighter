using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ScriptedGustTrigger : MonoBehaviour
{
    [SerializeField] private LampController targetLamp;
    [SerializeField] private TutorialDirector director;
    [SerializeField] private SpriteRenderer gustVisual;
    [SerializeField] private AudioSource gustAudio;
    private bool triggered;
    private Collider2D triggerCollider;

    public bool IsTriggered => triggered;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        if (gustVisual != null)
        {
            gustVisual.enabled = false;
        }
    }

    public void Configure(
        LampController lamp,
        TutorialDirector tutorial,
        SpriteRenderer visual)
    {
        targetLamp = lamp;
        director = tutorial;
        gustVisual = visual;
    }

    public void Configure(LampController lamp, SpriteRenderer visual)
    {
        Configure(lamp, null, visual);
    }

    public void RestoreState(bool wasTriggered)
    {
        triggered = wasTriggered;
        triggerCollider ??= GetComponent<Collider2D>();
        triggerCollider.enabled = !triggered;

        if (gustVisual != null)
        {
            gustVisual.enabled = triggered;
        }

        gameObject.SetActive(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        triggered = true;
        triggerCollider.enabled = false;
        if (gustAudio != null)
        {
            gustAudio.Play();
        }

        if (gustVisual != null)
        {
            gustVisual.enabled = true;
        }

        targetLamp?.Extinguish();
        director?.BeginRelightTutorial(targetLamp);
    }
}
