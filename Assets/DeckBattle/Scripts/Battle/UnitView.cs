using UnityEngine;

namespace DeckBattle
{
    public sealed class UnitView : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private float groundOffset = 0.65f;
        [SerializeField] private Color playerColor = new Color(0.18f, 0.62f, 0.95f, 1f);
        [SerializeField] private Color enemyColor = new Color(0.95f, 0.35f, 0.25f, 1f);

        public int RuntimeId { get; private set; }

        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            if (modelRoot == null)
            {
                modelRoot = transform;
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        public void Bind(RuntimeUnit unit, Vector3 worldPosition)
        {
            RuntimeId = unit.RuntimeId;
            transform.position = worldPosition + Vector3.up * groundOffset;
            name = unit.Side + "_Unit_" + unit.RuntimeId + "_" + unit.Definition.DisplayName;
            ApplySideColor(unit.Side);
        }

        private void ApplySideColor(BattleSide side)
        {
            if (meshRenderer == null)
            {
                return;
            }

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", side == BattleSide.Player ? playerColor : enemyColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
