using UnityEngine;

namespace DeckBattle
{
    // Assigned on a UnitView prefab to avoid transform-name searches during combat.
    public sealed class UnitVfxAnchors : MonoBehaviour
    {
        [SerializeField] private Transform ground;
        [SerializeField] private Transform body;
        [SerializeField] private Transform overhead;
        [SerializeField] private Transform projectileLaunch;

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
                case UnitVfxAnchor.ProjectileLaunch:
                    return projectileLaunch != null ? projectileLaunch : body;
                default:
                    return null;
            }
        }

        public bool HasAnchor(UnitVfxAnchor anchor)
        {
            return Resolve(anchor) != null;
        }

        public bool TryGetProjectileLaunch(out Transform anchor)
        {
            anchor = projectileLaunch;
            return anchor != null;
        }
    }
}
