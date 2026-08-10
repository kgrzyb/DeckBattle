using UnityEngine;

namespace DeckBattle
{
    // Assigned on a UnitView prefab to avoid transform-name searches during combat.
    public sealed class UnitVfxAnchors : MonoBehaviour
    {
        [SerializeField] private Transform ground;
        [SerializeField] private Transform body;
        [SerializeField] private Transform overhead;

        public Transform Resolve(UnitVfxAnchor anchor)
        {
            switch (anchor)
            {
                case UnitVfxAnchor.Ground:
                    return ground;
                case UnitVfxAnchor.Body:
                    return body;
                case UnitVfxAnchor.Overhead:
                    return overhead;
                default:
                    return null;
            }
        }

        public bool HasAnchor(UnitVfxAnchor anchor)
        {
            return Resolve(anchor) != null;
        }
    }
}
