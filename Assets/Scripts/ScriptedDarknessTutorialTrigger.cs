using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ScriptedDarknessTutorialTrigger : MonoBehaviour
{
    [SerializeField] private TutorialDirector director;
    [SerializeField] private Transform recoveryCheckpoint;
    private bool triggered;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other.GetComponentInParent<PlayerController>() == null)
        {
            return;
        }

        triggered = true;
        director?.BeginScriptedDarknessLesson(recoveryCheckpoint);
        gameObject.SetActive(false);
    }
}
