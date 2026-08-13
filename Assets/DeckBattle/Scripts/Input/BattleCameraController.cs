using UnityEngine;

namespace DeckBattle
{
    [DisallowMultipleComponent]
    public sealed class BattleCameraController : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform focusTarget;

        [Header("Default Pose")]
        [Tooltip("Optional transform containing the camera pose restored by ResetToDefaultPose. When empty, the camera's scene transform is used.")]
        [SerializeField] private Transform defaultPose;

        [Header("Zoom")]
        [SerializeField, Min(0.1f)] private float minDistance = 24f;
        [SerializeField, Min(0.1f)] private float maxDistance = 45f;
        [SerializeField, Min(0f)] private float pinchSensitivity = 35f;

        [Header("Pan")]
        [SerializeField] private Vector2 panOffsetLimitsX = new Vector2(-4f, 4f);
        [SerializeField] private Vector2 panOffsetLimitsZ = new Vector2(-3f, 3f);
        [SerializeField, Min(0f)] private float panSensitivity = 12f;

        private BattleCameraPanState panState;
        private Vector3 initialCameraPosition;
        private float initialDistance;
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

        /// <summary>
        /// Restores the position, rotation, pan offset, and zoom captured from the configured default pose.
        /// </summary>
        public void ResetToDefaultPose()
        {
            if (!EnsureInitialized())
            {
                return;
            }

            panState.Reset();
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

            Transform poseSource = defaultPose != null ? defaultPose : controlledCamera.transform;
            Vector3 cameraOffset = poseSource.position - focusTarget.position;
            initialCameraPosition = poseSource.position;
            initialRotation = poseSource.rotation;
            panState = new BattleCameraPanState(
                cameraOffset.magnitude,
                panOffsetLimitsX,
                panOffsetLimitsZ,
                minDistance,
                maxDistance);
            initialDistance = panState.Distance;
            isInitialized = true;
            ApplyPose();
        }

        private void ApplyPose()
        {
            Vector2 panOffset = panState.PanOffset;
            Vector3 panPosition = new Vector3(panOffset.x, 0f, panOffset.y);
            Vector3 zoomPosition = initialRotation * Vector3.forward * (initialDistance - panState.Distance);
            Vector3 position = initialCameraPosition + panPosition + zoomPosition;
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
