using System.Collections;
using UnityEngine;

// The "conductor": turns the wet look on for both the character and the
// taper pole at the same time, waits wetDuration seconds, then turns it
// back off. Call TriggerWet() whenever the player jumps into a puddle.
public sealed class CharacterWetState : MonoBehaviour
{
    [SerializeField] private CharacterAnimator characterAnimator;
    [SerializeField] private TaperPoleWet taperPoleWet;

    // Change this number in the Inspector any time you want a shorter or
    // longer wet duration. Defaults to 3 seconds as requested.
    [SerializeField, Min(0f)] private float wetDuration = 3f;

    private Coroutine wetRoutine;

    private void Awake()
    {
        if (characterAnimator == null)
        {
            characterAnimator = GetComponent<CharacterAnimator>();
        }

        if (taperPoleWet == null)
        {
            taperPoleWet = GetComponentInChildren<TaperPoleWet>();
        }
    }

    // Uses the wetDuration set in the Inspector.
    public void TriggerWet()
    {
        TriggerWet(wetDuration);
    }

    // Optional overload if one ever want something like a one-off custom duration.
    public void TriggerWet(float duration)
    {
        if (wetRoutine != null)
        {
            StopCoroutine(wetRoutine);
        }

        wetRoutine = StartCoroutine(WetRoutine(duration));
    }

    private IEnumerator WetRoutine(float duration)
    {
        characterAnimator?.SetWet(true);
        taperPoleWet?.SetWet(true);

        yield return new WaitForSeconds(duration);

        characterAnimator?.SetWet(false);
        taperPoleWet?.SetWet(false);
        wetRoutine = null;
    }
}