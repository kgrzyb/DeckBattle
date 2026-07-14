using UnityEngine;

namespace DeckBattle
{
    public sealed class HexTileView : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Color playerZoneColor = new Color(0.18f, 0.34f, 0.48f, 1f);
        [SerializeField] private Color enemyZoneColor = new Color(0.50f, 0.23f, 0.21f, 1f);
        [SerializeField] private Color neutralColor = new Color(0.27f, 0.29f, 0.27f, 1f);

        public HexCoord Coord { get; private set; }

        private MaterialPropertyBlock propertyBlock;

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
            ApplyColor(GetColorForSide(deploymentSide));
            name = "Hex_" + coord.Q + "_" + coord.R;
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
