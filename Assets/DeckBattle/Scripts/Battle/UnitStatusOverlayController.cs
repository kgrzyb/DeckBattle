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
        [SerializeField] private StatusPresentationCatalog presentationCatalog;
        [SerializeField] private Color playerHpFillColor = new Color(0.2f, 0.86f, 0.32f, 0.96f);
        [SerializeField] private Color enemyHpFillColor = new Color(0.88f, 0.22f, 0.24f, 0.96f);

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
        private float combatSpeed = 1f;

        private void Awake()
        {
            ResolveRoot();
            ResolveCamera();
        }

        public void SetPresentationCatalog(StatusPresentationCatalog catalog)
        {
            presentationCatalog = catalog;
        }

        public void SetCombatSpeed(float speed)
        {
            combatSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
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
            float deltaTime = Time.deltaTime * combatSpeed;
            int overlayToRelease = 0;
            foreach (KeyValuePair<int, TrackedOverlay> entry in activeOverlays)
            {
                TrackedOverlay tracked = entry.Value;
                if (tracked.View != null)
                {
                    tracked.View.TickDamageFill(deltaTime);
                }

                UpdateOverlayPosition(tracked, root, camera, cameraOrRootChanged);

                if (tracked.ReleaseDelayRemaining < 0f)
                {
                    continue;
                }

                tracked.ReleaseDelayRemaining -= deltaTime;
                if (tracked.ReleaseDelayRemaining <= 0f)
                {
                    overlayToRelease = entry.Key;
                    break;
                }
            }

            if (overlayToRelease > 0)
            {
                Release(overlayToRelease);
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
            Bind(unit.RuntimeId, view.transform, unit.Side, displayName, unit.CurrentHp, maxHp, 0, maxMana);
        }

        public void BindRealtimeUnit(UnitRuntimeState unit, UnitView view)
        {
            if (unit == null || view == null)
            {
                return;
            }

            Bind(
                unit.UnitId,
                view.transform,
                unit.Side,
                unit.DisplayName,
                unit.CurrentHp,
                unit.CombatSpec.MaxHp,
                unit.CurrentMana,
                unit.CombatSpec.ManaThreshold);
            SetStatuses(unit);
        }

        public void BindPresentationUnit(UnitPresentationState state, UnitView view)
        {
            if (view == null)
            {
                return;
            }

            Bind(
                state.UnitId,
                view.transform,
                state.Side,
                state.DisplayName,
                state.CurrentHp,
                state.MaxHp,
                state.CurrentMana,
                state.MaxMana);
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

        public void SetStatuses(UnitRuntimeState unit)
        {
            if (unit == null)
            {
                return;
            }

            TrackedOverlay tracked;
            if (!activeOverlays.TryGetValue(unit.UnitId, out tracked) || tracked.View == null)
            {
                return;
            }

            tracked.View.SetStatuses(unit.Statuses, unit.StatusSnapshot.TotalShield, presentationCatalog);
        }

        public void SetPresentationStatuses(int unitId, IReadOnlyList<StatusPresentationState> statuses, int totalShield)
        {
            TrackedOverlay tracked;
            if (!activeOverlays.TryGetValue(unitId, out tracked) || tracked.View == null)
            {
                return;
            }

            tracked.View.SetPresentationStatuses(statuses, totalShield, presentationCatalog);
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

        public void ReleaseAfterDamageAnimation(int unitId)
        {
            TrackedOverlay tracked;
            if (!activeOverlays.TryGetValue(unitId, out tracked) || tracked.View == null)
            {
                return;
            }

            tracked.ReleaseDelayRemaining = tracked.View.DamageFillAnimationDuration;
        }

        public void ReleaseAll()
        {
            foreach (TrackedOverlay tracked in activeOverlays.Values)
            {
                Pool(tracked.View);
            }

            activeOverlays.Clear();
        }

        private void Bind(int unitId, Transform target, BattleSide side, string displayName, int currentHp, int maxHp, int currentMana, int maxMana)
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
            tracked.View.SetHpFillColor(side == BattleSide.Enemy ? enemyHpFillColor : playerHpFillColor);
            tracked.View.SetStatuses(null, 0);
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
            public float ReleaseDelayRemaining = -1f;

            public TrackedOverlay(UnitStatusOverlayView view)
            {
                View = view;
            }

            public void ResetPositionCache()
            {
                HasPositionCache = false;
                HasAnchoredPosition = false;
                IsVisible = false;
                ReleaseDelayRemaining = -1f;
            }
        }
    }
}
