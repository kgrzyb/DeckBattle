using UnityEngine;

namespace DeckBattle
{
    [DisallowMultipleComponent]
    public sealed class BattleCameraController : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform focusTarget;

        [Header("Zoom")]
        [SerializeField, Min(0.1f)] private float minDistance = 24f;
        [SerializeField, Min(0.1f)] private float maxDistance = 45f;
        [SerializeField, Min(0f)] private float pinchSensitivity = 35f;

        [Header("Pan")]
        [SerializeField] private Vector2 panOffsetLimitsX = new Vector2(-4f, 4f);
        [SerializeField] private Vector2 panOffsetLimitsZ = new Vector2(-3f, 3f);
        [SerializeField, Min(0f)] private float panSensitivity = 12f;

        private BattleCameraPanState panState;
        private Vector3 initialFocusPosition;
        private Quaternion initialRotation;
        private bool isInitialized;

        private void Awake()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            InitializeFromCurrentPose();
        }

        private void OnValidate()
        {
            minDistance = NormalizeDistance(minDistance, 0.1f);
            maxDistance = Mathf.Max(minDistance, NormalizeDistance(maxDistance, minDistance));
            pinchSensitivity = NormalizeNonNegative(pinchSensitivity);
            panSensitivity = NormalizeNonNegative(panSensitivity);
            panOffsetLimitsX = NormalizeLimits(panOffsetLimitsX);
            panOffsetLimitsZ = NormalizeLimits(panOffsetLimitsZ);
        }

        public void Pan(Vector2 normalizedScreenDelta)
        {
            if (!EnsureInitialized() || !panState.Pan(normalizedScreenDelta, panSensitivity))
            {
                return;
            }

            ApplyPose();
        }

        public void Zoom(float normalizedPinchDelta)
        {
            if (!EnsureInitialized() || !panState.Zoom(normalizedPinchDelta, pinchSensitivity))
            {
                return;
            }

            ApplyPose();
        }

        private bool EnsureInitialized()
        {
            if (!isInitialized)
            {
                InitializeFromCurrentPose();
            }

            return isInitialized;
        }

        private void InitializeFromCurrentPose()
        {
            OnValidate();
            if (controlledCamera == null || focusTarget == null)
            {
                isInitialized = false;
                return;
            }

            Vector3 cameraOffset = controlledCamera.transform.position - focusTarget.position;
            float initialDistance = cameraOffset.magnitude;
            initialFocusPosition = focusTarget.position;
            initialRotation = controlledCamera.transform.rotation;
            panState = new BattleCameraPanState(
                initialDistance,
                panOffsetLimitsX,
                panOffsetLimitsZ,
                minDistance,
                maxDistance);
            isInitialized = true;
            ApplyPose();
        }

        private void ApplyPose()
        {
            Vector2 panOffset = panState.PanOffset;
            Vector3 focusPosition = initialFocusPosition + new Vector3(panOffset.x, 0f, panOffset.y);
            Vector3 position = focusPosition - initialRotation * Vector3.forward * panState.Distance;
            controlledCamera.transform.SetPositionAndRotation(position, initialRotation);
        }

        private static Vector2 NormalizeLimits(Vector2 limits)
        {
            float lower = IsFinite(limits.x) ? limits.x : 0f;
            float upper = IsFinite(limits.y) ? limits.y : 0f;
            return lower <= upper ? new Vector2(lower, upper) : new Vector2(upper, lower);
        }

        private static float NormalizeDistance(float value, float fallback)
        {
            return IsFinite(value) && value >= 0.1f ? value : fallback;
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
