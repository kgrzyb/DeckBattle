using UnityEngine;

namespace DeckBattle
{
    public sealed class HexTileView : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Color playerZoneColor = new Color(0.18f, 0.34f, 0.48f, 1f);
        [SerializeField] private Color enemyZoneColor = new Color(0.50f, 0.23f, 0.21f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.27f, 0.29f, 0.27f, 1f);
        [SerializeField] private Color legalHighlightColor = new Color(0.28f, 0.72f, 0.38f, 1f);
        [SerializeField] private Color blockedHighlightColor = new Color(0.82f, 0.32f, 0.24f, 1f);
        [SerializeField] private Color selectedHighlightColor = new Color(0.96f, 0.78f, 0.26f, 1f);

        public HexCoord Coord { get; private set; }

        private MaterialPropertyBlock propertyBlock;
        private Color baseColor;

        private void Awake()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        public void Initialize(HexCoord coord, BattleSide? deploymentSide)
        {
            Coord = coord;
            baseColor = GetColorForSide(deploymentSide);
            ApplyColor(baseColor);
            name = "Hex_" + coord.Q + "_" + coord.R;
        }

        public void SetLegalHighlight()
        {
            ApplyColor(legalHighlightColor);
        }

        public void SetBlockedHighlight()
        {
            ApplyColor(blockedHighlightColor);
        }

        public void SetSelectedHighlight()
        {
            ApplyColor(selectedHighlightColor);
        }

        public void ClearHighlight()
        {
            ApplyColor(baseColor);
        }

        private Color GetColorForSide(BattleSide? deploymentSide)
        {
            if (!deploymentSide.HasValue)
            {
                return neutralColor;
            }

            return deploymentSide.Value == BattleSide.Player ? playerZoneColor : enemyZoneColor;
        }

        private void ApplyColor(Color color)
        {
            if (meshRenderer == null)
            {
                return;
            }

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
