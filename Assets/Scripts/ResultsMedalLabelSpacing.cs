using UnityEngine;
using UnityEngine.UI;

// Keeps the medal name the same visual distance below each trophy. The three
// images have different transparent padding, even though their source files
// are all 2000 x 2000 pixels.
public sealed class ResultsMedalLabelSpacing : MonoBehaviour
{
    [SerializeField] private Image trophyImage;
    [SerializeField] private RectTransform medalLabel;
    [SerializeField] private Sprite goldTrophy;
    [SerializeField] private Sprite silverTrophy;
    [SerializeField] private Sprite bronzeTrophy;

    [Header("Medal label position (local Y)")]
    [SerializeField] private float goldLabelY = -82f;
    [SerializeField] private float silverLabelY = -39f;
    [SerializeField] private float bronzeLabelY = -60f;

    private Sprite lastSprite;

    private void LateUpdate()
    {
        if (trophyImage == null || medalLabel == null || trophyImage.sprite == lastSprite)
            return;

        Vector2 position = medalLabel.anchoredPosition;
        position.y = LabelYFor(trophyImage.sprite);
        medalLabel.anchoredPosition = position;
        lastSprite = trophyImage.sprite;
    }

    private float LabelYFor(Sprite trophy)
    {
        if (trophy == silverTrophy) return silverLabelY;
        if (trophy == bronzeTrophy) return bronzeLabelY;
        return goldLabelY;
    }
}
