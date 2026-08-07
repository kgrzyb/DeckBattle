using UnityEngine;

namespace DeckBattle
{
    public sealed class HexTileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color defaultColor = new Color(0.43f, 0.45f, 0.55f, 0.53f);
        [SerializeField] private Color legalHighlightColor = new Color(0.28f, 0.72f, 0.38f, 1f);
        [SerializeField] private Color blockedHighlightColor = new Color(0.82f, 0.32f, 0.24f, 1f);
        [SerializeField] private Color selectedHighlightColor = new Color(0.96f, 0.78f, 0.26f, 1f);

        public HexCoord Coord { get; private set; }

        private void Awake()
        {
            EnsureSpriteRenderer();
        }

        public void Initialize(HexCoord coord)
        {
            Coord = coord;
            name = "Hex_" + coord.Q + "_" + coord.R;
            SetVisualState(PreparationHexVisualState.Default);
        }

        public void SetVisualState(PreparationHexVisualState state)
        {
            EnsureSpriteRenderer();
            if (spriteRenderer == null)
            {
                return;
            }

            switch (state)
            {
                case PreparationHexVisualState.Legal:
                    spriteRenderer.color = legalHighlightColor;
                    break;
                case PreparationHexVisualState.Blocked:
                    spriteRenderer.color = blockedHighlightColor;
                    break;
                case PreparationHexVisualState.Selected:
                    spriteRenderer.color = selectedHighlightColor;
                    break;
                default:
                    spriteRenderer.color = defaultColor;
                    break;
            }
        }

        private void EnsureSpriteRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }
    }
}
