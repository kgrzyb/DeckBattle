using UnityEngine;

namespace DeckBattle
{
    // Assigned on a UnitView prefab to avoid transform-name searches during combat.
    public sealed class UnitVfxAnchors : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform feet;
        [SerializeField] private Transform weapon;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform head;
        [SerializeField] private Transform special;

        public Transform Resolve(UnitVfxAnchor anchor)
        {
            switch (anchor)
            {
                case UnitVfxAnchor.Body:
                    return body;
                case UnitVfxAnchor.Feet:
                    return feet;
                case UnitVfxAnchor.Weapon:
                    return weapon;
                case UnitVfxAnchor.Muzzle:
                    return muzzle;
                case UnitVfxAnchor.Head:
                    return head;
                case UnitVfxAnchor.Special:
                    return special;
                default:
                    return transform;
            }
        }
    }
}
