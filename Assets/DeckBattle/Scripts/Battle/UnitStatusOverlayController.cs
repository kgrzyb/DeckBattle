using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class UnitStatusOverlayController : MonoBehaviour
    {
        [SerializeField] private UnitStatusOverlayView overlayPrefab;
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.55f, 0f);

        private readonly Dictionary<int, TrackedOverlay> activeOverlays = new Dictionary<int, TrackedOverlay>(16);
        private readonly Stack<UnitStatusOverlayView> pooledOverlays = new Stack<UnitStatusOverlayView>(16);

        private RectTransform cachedRoot;
        private Matrix4x4 lastWorldToCameraMatrix;
        private Matrix4x4 lastProjectionMatrix;
        private Rect lastCameraPixelRect;
        private Rect lastRootRect;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private bool projectionStateInitialized;

        private void Awake()
        {
            ResolveRoot();
            ResolveCamera();
        }

        private void LateUpdate()
        {
            RectTransform root = ResolveRoot();
            Camera camera = ResolveCamera();
            if (root == null || camera == null)
            {
                return;
            }

            bool cameraOrRootChanged = HasProjectionStateChanged(root, camera);
            foreach (TrackedOverlay tracked in activeOverlays.Values)
            {
                UpdateOverlayPosition(tracked, root, camera, cameraOrRootChanged);
            }
        }

        public void BindRuntimeUnit(RuntimeUnit unit, UnitView view)
        {
            if (unit == null || view == null)
            {
                return;
            }

            UnitDefinition definition = unit.Definition;
            int maxHp = definition != null ? definition.MaxHp : 1;
            int maxMana = definition != null ? definition.ManaThreshold : 0;
            string displayName = definition != null ? definition.DisplayName : null;
            Bind(unit.RuntimeId, view.transform, displayName, unit.CurrentHp, maxHp, 0, maxMana);
        }

        public void BindRealtimeUnit(UnitRuntimeState unit, UnitView view)
        {
            if (unit == null || view == null)
            {
                return;
            }

            UnitDefinition definition = unit.Definition;
            int maxHp = definition != null ? definition.MaxHp : 1;
            int maxMana = definition != null ? definition.ManaThreshold : 0;
            string displayName = definition != null ? definition.DisplayName : null;
            Bind(unit.UnitId, view.transform, displayName, unit.CurrentHp, maxHp, unit.CurrentMana, maxMana);
        }

        public void SetHealth(int unitId, int currentHp, int maxHp)
        {
            TrackedOverlay tracked;
            if (!activeOverlays.TryGetValue(unitId, out tracked) || tracked.View == null)
            {
                return;
            }

            tracked.View.SetHealth(currentHp, maxHp);
        }

        public void SetMana(int unitId, int currentMana, int maxMana)
        {
            TrackedOverlay tracked;
            if (!activeOverlays.TryGetValue(unitId, out tracked) || tracked.View == null)
            {
                return;
            }

            tracked.View.SetMana(currentMana, maxMana);
        }

        public void Release(int unitId)
        {
            TrackedOverlay tracked;
            if (!activeOverlays.TryGetValue(unitId, out tracked))
            {
                return;
            }

            activeOverlays.Remove(unitId);
            Pool(tracked.View);
        }

        public void ReleaseAll()
        {
            foreach (TrackedOverlay tracked in activeOverlays.Values)
            {
                Pool(tracked.View);
            }

            activeOverlays.Clear();
        }

        private void Bind(int unitId, Transform target, string displayName, int currentHp, int maxHp, int currentMana, int maxMana)
        {
            TrackedOverlay tracked;
            if (!activeOverlays.TryGetValue(unitId, out tracked) || tracked.View == null)
            {
                tracked = new TrackedOverlay(GetOverlay());
                activeOverlays[unitId] = tracked;
            }

            tracked.Target = target;
            tracked.MaxHp = Mathf.Max(1, maxHp);
            tracked.MaxMana = Mathf.Max(1, maxMana);
            tracked.ResetPositionCache();
            tracked.View.Bind(unitId, target, displayName, currentHp, tracked.MaxHp, currentMana, tracked.MaxMana);
        }

        private UnitStatusOverlayView GetOverlay()
        {
            UnitStatusOverlayView view = pooledOverlays.Count > 0 ? pooledOverlays.Pop() : Instantiate(overlayPrefab, ResolveRoot());
            view.transform.SetParent(ResolveRoot(), false);
            return view;
        }

        private void Pool(UnitStatusOverlayView view)
        {
            if (view == null)
            {
                return;
            }

            view.Release();
            view.transform.SetParent(ResolveRoot(), false);
            pooledOverlays.Push(view);
        }

        private RectTransform ResolveRoot()
        {
            if (overlayRoot != null)
            {
                return overlayRoot;
            }

            if (cachedRoot == null)
            {
                cachedRoot = transform as RectTransform;
            }

            return cachedRoot;
        }

        private Camera ResolveCamera()
        {
            if (worldCamera != null && worldCamera.isActiveAndEnabled)
            {
                return worldCamera;
            }

            worldCamera = Camera.main;
            return worldCamera;
        }

        private bool HasProjectionStateChanged(RectTransform root, Camera camera)
        {
            Matrix4x4 worldToCameraMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projectionMatrix = camera.projectionMatrix;
            Rect cameraPixelRect = camera.pixelRect;
            Rect rootRect = root.rect;
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            bool changed = !projectionStateInitialized
                || lastWorldToCameraMatrix != worldToCameraMatrix
                || lastProjectionMatrix != projectionMatrix
                || lastCameraPixelRect != cameraPixelRect
                || lastRootRect != rootRect
                || lastScreenWidth != screenWidth
                || lastScreenHeight != screenHeight;

            lastWorldToCameraMatrix = worldToCameraMatrix;
            lastProjectionMatrix = projectionMatrix;
            lastCameraPixelRect = cameraPixelRect;
            lastRootRect = rootRect;
            lastScreenWidth = screenWidth;
            lastScreenHeight = screenHeight;
            projectionStateInitialized = true;
            return changed;
        }

        private void UpdateOverlayPosition(TrackedOverlay tracked, RectTransform root, Camera camera, bool cameraOrRootChanged)
        {
            UnitStatusOverlayView view = tracked.View;
            if (view == null)
            {
                return;
            }

            Transform target = tracked.Target;
            if (target == null)
            {
                SetVisible(tracked, false);
                return;
            }

            Vector3 targetPosition = target.position;
            if (!cameraOrRootChanged && tracked.HasPositionCache && tracked.LastTargetPosition == targetPosition)
            {
                return;
            }

            tracked.LastTargetPosition = targetPosition;
            tracked.HasPositionCache = true;
            Vector3 screenPosition = camera.WorldToScreenPoint(targetPosition + worldOffset);
            if (screenPosition.z <= 0f)
            {
                SetVisible(tracked, false);
                return;
            }

            Vector2 anchoredPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, null, out anchoredPosition))
            {
                SetVisible(tracked, false);
                return;
            }

            if (!tracked.HasAnchoredPosition || (tracked.LastAnchoredPosition - anchoredPosition).sqrMagnitude > 0.0001f)
            {
                view.RectTransform.anchoredPosition = anchoredPosition;
                tracked.LastAnchoredPosition = anchoredPosition;
                tracked.HasAnchoredPosition = true;
            }

            SetVisible(tracked, true);
        }

        private static void SetVisible(TrackedOverlay tracked, bool visible)
        {
            if (tracked.IsVisible == visible)
            {
                return;
            }

            tracked.View.SetVisible(visible);
            tracked.IsVisible = visible;
        }

        private sealed class TrackedOverlay
        {
            public readonly UnitStatusOverlayView View;
            public Transform Target;
            public int MaxHp;
            public int MaxMana;
            public Vector3 LastTargetPosition;
            public Vector2 LastAnchoredPosition;
            public bool HasPositionCache;
            public bool HasAnchoredPosition;
            public bool IsVisible;

            public TrackedOverlay(UnitStatusOverlayView view)
            {
                View = view;
            }

            public void ResetPositionCache()
            {
                HasPositionCache = false;
                HasAnchoredPosition = false;
                IsVisible = false;
            }
        }
    }
}
