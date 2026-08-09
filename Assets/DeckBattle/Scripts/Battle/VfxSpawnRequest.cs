using UnityEngine;

namespace DeckBattle
{
    public readonly struct VfxSpawnRequest
    {
        public readonly Transform Anchor;
        public readonly Vector3 WorldPosition;
        public readonly Quaternion WorldRotation;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;
        public readonly bool FollowAnchor;
        public readonly int OwnerUnitId;

        public VfxSpawnRequest(
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 localScale,
            int ownerUnitId = 0)
            : this(null, worldPosition, worldRotation, Vector3.zero, Quaternion.identity, localScale, false, ownerUnitId)
        {
        }

        public VfxSpawnRequest(
            Transform anchor,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            int ownerUnitId = 0)
            : this(
                anchor,
                anchor != null ? anchor.position : Vector3.zero,
                anchor != null ? anchor.rotation : Quaternion.identity,
                localPosition,
                localRotation,
                localScale,
                true,
                ownerUnitId)
        {
        }

        private VfxSpawnRequest(
            Transform anchor,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            bool followAnchor,
            int ownerUnitId)
        {
            Anchor = anchor;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale == Vector3.zero ? Vector3.one : localScale;
            FollowAnchor = followAnchor && anchor != null;
            OwnerUnitId = ownerUnitId;
        }

        public static VfxSpawnRequest AtWorld(Vector3 position, int ownerUnitId = 0)
        {
            return new VfxSpawnRequest(position, Quaternion.identity, Vector3.one, ownerUnitId);
        }
    }
}
