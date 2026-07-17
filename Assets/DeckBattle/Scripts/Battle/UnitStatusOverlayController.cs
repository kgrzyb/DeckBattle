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

            foreach (TrackedOverlay tracked in activeOverlays.Values)
            {
                UpdateOverlayPosition(tracked, root, camera);
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

        private void UpdateOverlayPosition(TrackedOverlay tracked, RectTransform root, Camera camera)
        {
            UnitStatusOverlayView view = tracked.View;
            if (view == null)
            {
                return;
            }

            Transform target = tracked.Target;
            if (target == null)
            {
                view.SetVisible(false);
                return;
            }

            Vector3 screenPosition = camera.WorldToScreenPoint(target.position + worldOffset);
            if (screenPosition.z <= 0f)
            {
                view.SetVisible(false);
                return;
            }

            Vector2 anchoredPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, null, out anchoredPosition))
            {
                view.SetVisible(false);
                return;
            }

            view.RectTransform.anchoredPosition = anchoredPosition;
            view.SetVisible(true);
        }

        private sealed class TrackedOverlay
        {
            public readonly UnitStatusOverlayView View;
            public Transform Target;
            public int MaxHp;
            public int MaxMana;

            public TrackedOverlay(UnitStatusOverlayView view)
            {
                View = view;
            }
        }
    }
}
