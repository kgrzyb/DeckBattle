using UnityEngine;

namespace DeckBattle
{
    public struct BattleCameraPanState
    {
        private const float MinimumDistance = 0.1f;

        private readonly Vector2 panOffsetLimitsX;
        private readonly Vector2 panOffsetLimitsZ;
        private readonly float minDistance;
        private readonly float maxDistance;

        private Vector2 panOffset;
        private float distance;

        public Vector2 PanOffset
        {
            get { return panOffset; }
        }

        public float Distance
        {
            get { return distance; }
        }

        public BattleCameraPanState(
            float initialDistance,
            Vector2 sourcePanOffsetLimitsX,
            Vector2 sourcePanOffsetLimitsZ,
            float sourceMinDistance,
            float sourceMaxDistance)
        {
            panOffsetLimitsX = NormalizeRange(sourcePanOffsetLimitsX);
            panOffsetLimitsZ = NormalizeRange(sourcePanOffsetLimitsZ);
            minDistance = NormalizeDistance(sourceMinDistance, MinimumDistance);
            maxDistance = Mathf.Max(minDistance, NormalizeDistance(sourceMaxDistance, minDistance));

            panOffset = Vector2.zero;
            distance = Mathf.Clamp(NormalizeDistance(initialDistance, minDistance), minDistance, maxDistance);
        }

        public bool Pan(Vector2 normalizedScreenDelta, float panSensitivity)
        {
            float safeSensitivity = NormalizeNonNegative(panSensitivity);
            Vector2 nextOffset = new Vector2(
                Mathf.Clamp(
                    panOffset.x - normalizedScreenDelta.x * safeSensitivity,
                    panOffsetLimitsX.x,
                    panOffsetLimitsX.y),
                Mathf.Clamp(
                    panOffset.y - normalizedScreenDelta.y * safeSensitivity,
                    panOffsetLimitsZ.x,
                    panOffsetLimitsZ.y));
            if (nextOffset == panOffset)
            {
                return false;
            }

            panOffset = nextOffset;
            return true;
        }

        public bool Zoom(float normalizedPinchDelta, float pinchSensitivity)
        {
            float nextDistance = Mathf.Clamp(
                distance - normalizedPinchDelta * NormalizeNonNegative(pinchSensitivity),
                minDistance,
                maxDistance);
            if (Mathf.Approximately(distance, nextDistance))
            {
                return false;
            }

            distance = nextDistance;
            return true;
        }

        private static Vector2 NormalizeRange(Vector2 limits)
        {
            float lower = IsFinite(limits.x) ? limits.x : 0f;
            float upper = IsFinite(limits.y) ? limits.y : 0f;
            return lower <= upper ? new Vector2(lower, upper) : new Vector2(upper, lower);
        }

        private static float NormalizeDistance(float value, float fallback)
        {
            return IsFinite(value) && value >= MinimumDistance ? value : fallback;
        }

        private static float NormalizeNonNegative(float value)
        {
            return IsFinite(value) && value > 0f ? value : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
