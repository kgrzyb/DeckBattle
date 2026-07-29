using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class UnitStatusVfxController : MonoBehaviour
    {
        private const int StatusKindCapacity = (int)StatusKind.Guard + 1;

        [SerializeField] private StatusPresentationCatalog presentationCatalog;
        [SerializeField] private Transform poolRoot;

        private readonly Dictionary<int, Transform> pivotsByUnitId = new Dictionary<int, Transform>(16);
        private readonly Dictionary<StatusVfxView, Stack<StatusVfxView>> poolsByPrefab = new Dictionary<StatusVfxView, Stack<StatusVfxView>>(8);
        private readonly List<ActiveVfx> activeVfx = new List<ActiveVfx>(32);
        private readonly List<OneShotVfx> activeOneShots = new List<OneShotVfx>(16);
        private readonly List<ShadowStatus> shadowStatuses = new List<ShadowStatus>(32);
        private readonly int[] syncVersions = new int[StatusKindCapacity];
        private int syncVersion = 1;

        public void Initialize(StatusPresentationCatalog catalog)
        {
            presentationCatalog = catalog;
            Prewarm();
        }

        private void Update()
        {
            for (int i = activeOneShots.Count - 1; i >= 0; i--)
            {
                OneShotVfx oneShot = activeOneShots[i];
                if (oneShot.View != null && !oneShot.View.IsOneShotComplete)
                {
                    continue;
                }

                ReturnToPool(oneShot.Prefab, oneShot.View);
                activeOneShots.RemoveAt(i);
            }
        }

        public void BindOrSync(UnitRuntimeState unit, UnitView view)
        {
            if (unit == null || view == null)
            {
                return;
            }

            pivotsByUnitId[unit.UnitId] = view.StatusVfxPivot;
            RebuildShadow(unit.UnitId, unit.Statuses);
            Sync(unit.UnitId, unit.Statuses);
        }

        public void HandleStatusEvent(BattleEvent battleEvent)
        {
            if (presentationCatalog == null || !presentationCatalog.TryGet(battleEvent.StatusKind, out StatusPresentationEntry entry) || entry.Mode != StatusPresentationMode.Vfx)
            {
                return;
            }

            int previousStacks = GetShadowStacks(battleEvent.UnitId, battleEvent.StatusKind, battleEvent.TargetUnitId);
            int nextStacks = previousStacks;
            switch (battleEvent.Type)
            {
                case BattleEventType.StatusApplied:
                case BattleEventType.StatusRefreshed:
                case BattleEventType.StatusStackChanged:
                    nextStacks = Mathf.Max(0, battleEvent.StatusStackCount);
                    break;
                case BattleEventType.StatusRemoved:
                    nextStacks = 0;
                    break;
                default:
                    return;
            }

            int delta = battleEvent.StatusStackDelta;
            if (delta == 0)
            {
                delta = nextStacks - previousStacks;
            }
            if (delta > 0)
            {
                PlayOneShots(battleEvent.UnitId, entry.ApplyVfxPrefab, entry, delta);
            }
            else if (delta < 0)
            {
                PlayOneShots(battleEvent.UnitId, entry.RemoveVfxPrefab, entry, -delta);
            }

            SetShadowStacks(battleEvent.UnitId, battleEvent.StatusKind, battleEvent.TargetUnitId, nextStacks);
        }

        public void Sync(UnitRuntimeState unit)
        {
            if (unit != null)
            {
                Sync(unit.UnitId, unit.Statuses);
            }
        }

        public void Release(int unitId)
        {
            pivotsByUnitId.Remove(unitId);
            for (int i = activeVfx.Count - 1; i >= 0; i--)
            {
                ActiveVfx active = activeVfx[i];
                if (active.UnitId != unitId)
                {
                    continue;
                }

                ReturnToPool(active.Prefab, active.View);
                activeVfx.RemoveAt(i);
            }

            for (int i = shadowStatuses.Count - 1; i >= 0; i--)
            {
                if (shadowStatuses[i].UnitId == unitId)
                {
                    shadowStatuses.RemoveAt(i);
                }
            }

            for (int i = activeOneShots.Count - 1; i >= 0; i--)
            {
                OneShotVfx oneShot = activeOneShots[i];
                if (oneShot.UnitId != unitId) continue;
                ReturnToPool(oneShot.Prefab, oneShot.View);
                activeOneShots.RemoveAt(i);
            }
        }

        public void ReleaseAll()
        {
            for (int i = activeVfx.Count - 1; i >= 0; i--)
            {
                ActiveVfx active = activeVfx[i];
                ReturnToPool(active.Prefab, active.View);
            }

            activeVfx.Clear();
            for (int i = activeOneShots.Count - 1; i >= 0; i--)
            {
                OneShotVfx oneShot = activeOneShots[i];
                ReturnToPool(oneShot.Prefab, oneShot.View);
            }
            activeOneShots.Clear();
            pivotsByUnitId.Clear();
            shadowStatuses.Clear();
        }

        private void Sync(int unitId, UnitStatusCollection statuses)
        {
            if (presentationCatalog == null || !pivotsByUnitId.TryGetValue(unitId, out Transform pivot) || pivot == null)
            {
                return;
            }

            syncVersion++;
            if (syncVersion == int.MaxValue)
            {
                for (int i = 0; i < syncVersions.Length; i++) syncVersions[i] = 0;
                syncVersion = 1;
            }

            int statusCount = statuses != null ? statuses.Count : 0;
            for (int i = 0; i < statusCount; i++)
            {
                StatusKind kind = statuses[i].Kind;
                int kindIndex = (int)kind;
                if (kindIndex <= 0 || syncVersions[kindIndex] == syncVersion)
                {
                    continue;
                }

                syncVersions[kindIndex] = syncVersion;
                int desired = GetTotalStacks(statuses, kind);
                if (presentationCatalog.TryGet(kind, out StatusPresentationEntry entry) && entry.Mode == StatusPresentationMode.Vfx)
                {
                    Reconcile(unitId, kind, desired, pivot, entry);
                }
                else
                {
                    Reconcile(unitId, kind, 0, pivot, null);
                }
            }

            for (int i = activeVfx.Count - 1; i >= 0; i--)
            {
                ActiveVfx active = activeVfx[i];
                if (active.UnitId == unitId && syncVersions[(int)active.Kind] != syncVersion)
                {
                    ReturnToPool(active.Prefab, active.View);
                    activeVfx.RemoveAt(i);
                }
            }
        }

        private void Reconcile(int unitId, StatusKind kind, int desiredCount, Transform pivot, StatusPresentationEntry entry)
        {
            int currentCount = 0;
            for (int i = 0; i < activeVfx.Count; i++)
            {
                if (activeVfx[i].UnitId == unitId && activeVfx[i].Kind == kind) currentCount++;
            }

            while (currentCount < desiredCount)
            {
                StatusVfxView view = GetFromPool(entry != null ? entry.ActiveVfxPrefab : null);
                if (view == null) return;
                view.BeginActive(pivot, entry);
                activeVfx.Add(new ActiveVfx(unitId, kind, entry.ActiveVfxPrefab, view));
                currentCount++;
            }

            while (currentCount > desiredCount)
            {
                for (int i = activeVfx.Count - 1; i >= 0; i--)
                {
                    ActiveVfx active = activeVfx[i];
                    if (active.UnitId != unitId || active.Kind != kind) continue;
                    ReturnToPool(active.Prefab, active.View);
                    activeVfx.RemoveAt(i);
                    currentCount--;
                    break;
                }
            }
        }

        private void PlayOneShots(int unitId, StatusVfxView prefab, StatusPresentationEntry entry, int count)
        {
            if (prefab == null || !pivotsByUnitId.TryGetValue(unitId, out Transform pivot) || pivot == null) return;
            for (int i = 0; i < count; i++)
            {
                StatusVfxView view = GetFromPool(prefab);
                if (view == null) return;
                view.PlayOneShot(pivot, entry);
                activeOneShots.Add(new OneShotVfx(unitId, prefab, view));
            }
        }

        private int GetTotalStacks(UnitStatusCollection statuses, StatusKind kind)
        {
            int total = 0;
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].Kind == kind) total += Mathf.Max(1, statuses[i].Stacks);
            }
            return total;
        }

        private void RebuildShadow(int unitId, UnitStatusCollection statuses)
        {
            for (int i = shadowStatuses.Count - 1; i >= 0; i--)
            {
                if (shadowStatuses[i].UnitId == unitId) shadowStatuses.RemoveAt(i);
            }

            if (statuses == null) return;
            for (int i = 0; i < statuses.Count; i++)
            {
                StatusInstance status = statuses[i];
                SetShadowStacks(unitId, status.Kind, status.SourceUnitId, status.Stacks);
            }
        }

        private int GetShadowStacks(int unitId, StatusKind kind, int sourceUnitId)
        {
            for (int i = 0; i < shadowStatuses.Count; i++)
            {
                ShadowStatus status = shadowStatuses[i];
                if (status.UnitId == unitId && status.Kind == kind && status.SourceUnitId == sourceUnitId) return status.Stacks;
            }
            return 0;
        }

        private void SetShadowStacks(int unitId, StatusKind kind, int sourceUnitId, int stacks)
        {
            for (int i = 0; i < shadowStatuses.Count; i++)
            {
                if (shadowStatuses[i].UnitId == unitId && shadowStatuses[i].Kind == kind && shadowStatuses[i].SourceUnitId == sourceUnitId)
                {
                    if (stacks <= 0) shadowStatuses.RemoveAt(i);
                    else shadowStatuses[i] = new ShadowStatus(unitId, kind, sourceUnitId, stacks);
                    return;
                }
            }
            if (stacks > 0) shadowStatuses.Add(new ShadowStatus(unitId, kind, sourceUnitId, stacks));
        }

        private void Prewarm()
        {
            if (presentationCatalog == null) return;
            StatusPresentationEntry[] entries = presentationCatalog.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                StatusPresentationEntry entry = entries[i];
                if (entry == null || entry.Mode != StatusPresentationMode.Vfx) continue;
                Prewarm(entry.ApplyVfxPrefab, entry.PrewarmCountPerPrefab);
                Prewarm(entry.ActiveVfxPrefab, entry.PrewarmCountPerPrefab);
                Prewarm(entry.RemoveVfxPrefab, entry.PrewarmCountPerPrefab);
            }
        }

        private void Prewarm(StatusVfxView prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                StatusVfxView view = Instantiate(prefab, ResolvePoolRoot());
                view.gameObject.SetActive(false);
                GetPool(prefab).Push(view);
            }
        }

        private StatusVfxView GetFromPool(StatusVfxView prefab)
        {
            if (prefab == null) return null;
            Stack<StatusVfxView> pool = GetPool(prefab);
            return pool.Count > 0 ? pool.Pop() : Instantiate(prefab, ResolvePoolRoot());
        }

        private void ReturnToPool(StatusVfxView prefab, StatusVfxView view)
        {
            if (view == null) return;
            view.Release();
            view.transform.SetParent(ResolvePoolRoot(), false);
            GetPool(prefab).Push(view);
        }

        private Stack<StatusVfxView> GetPool(StatusVfxView prefab)
        {
            if (!poolsByPrefab.TryGetValue(prefab, out Stack<StatusVfxView> pool))
            {
                pool = new Stack<StatusVfxView>(8);
                poolsByPrefab.Add(prefab, pool);
            }
            return pool;
        }

        private Transform ResolvePoolRoot() { return poolRoot != null ? poolRoot : transform; }

        private readonly struct ActiveVfx
        {
            public readonly int UnitId; public readonly StatusKind Kind; public readonly StatusVfxView Prefab; public readonly StatusVfxView View;
            public ActiveVfx(int unitId, StatusKind kind, StatusVfxView prefab, StatusVfxView view) { UnitId = unitId; Kind = kind; Prefab = prefab; View = view; }
        }

        private readonly struct OneShotVfx
        {
            public readonly int UnitId; public readonly StatusVfxView Prefab; public readonly StatusVfxView View;
            public OneShotVfx(int unitId, StatusVfxView prefab, StatusVfxView view) { UnitId = unitId; Prefab = prefab; View = view; }
        }

        private readonly struct ShadowStatus
        {
            public readonly int UnitId; public readonly StatusKind Kind; public readonly int SourceUnitId; public readonly int Stacks;
            public ShadowStatus(int unitId, StatusKind kind, int sourceUnitId, int stacks) { UnitId = unitId; Kind = kind; SourceUnitId = sourceUnitId; Stacks = stacks; }
        }
    }
}
