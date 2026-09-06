using UnityEngine;

// Swaps the taper pole's sprite between its normal and wet versions.
// Mirrors the same swap pattern used for the lamp on/off sprites.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class TaperPoleWet : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite wetSprite;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    public void SetWet(bool wet)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite = wet ? wetSprite : normalSprite;
    }
}