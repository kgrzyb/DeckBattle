using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class UnitStatusVfxController : MonoBehaviour
    {
        private const int StatusKindCapacity = (int)StatusKind.Guard + 1;

        [SerializeField] private StatusPresentationCatalog presentationCatalog;
        private BattleVfxPool battleVfxPool;
        private readonly Dictionary<int, Transform> pivotsByUnitId = new Dictionary<int, Transform>(16);
        private readonly List<ActivePooledVfx> activePooledVfx = new List<ActivePooledVfx>(16);
        private readonly List<ShadowStatus> shadowStatuses = new List<ShadowStatus>(32);
        private readonly int[] syncVersions = new int[StatusKindCapacity];
        private int syncVersion = 1;

        public void Initialize(StatusPresentationCatalog catalog, BattleVfxPool vfxPool)
        {
            ReleaseAll();
            presentationCatalog = catalog;
            battleVfxPool = vfxPool;
            Prewarm();
        }

        public void SetCombatSpeed(float speed)
        {
            battleVfxPool?.SetCombatSpeed(speed);
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

        public void BindPresentationUnit(int unitId, UnitView view)
        {
            if (view != null)
            {
                pivotsByUnitId[unitId] = view.StatusVfxPivot;
            }
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
                PlayOneShots(battleEvent.UnitId, entry.ApplyVfxDefinition, entry, delta);
            }
            else if (delta < 0)
            {
                PlayOneShots(battleEvent.UnitId, entry.RemoveVfxDefinition, entry, -delta);
            }

            SetShadowStacks(battleEvent.UnitId, battleEvent.StatusKind, battleEvent.TargetUnitId, nextStacks);
            ReconcileShadowStatusVfx(battleEvent.UnitId, battleEvent.StatusKind, entry);
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
            ReleasePooledByUnit(unitId);

            for (int i = shadowStatuses.Count - 1; i >= 0; i--)
            {
                if (shadowStatuses[i].UnitId == unitId)
                {
                    shadowStatuses.RemoveAt(i);
                }
            }

        }

        public void ReleaseAll()
        {
            ReleaseAllPooled();
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

            for (int i = activePooledVfx.Count - 1; i >= 0; i--)
            {
                ActivePooledVfx active = activePooledVfx[i];
                if (active.UnitId == unitId && syncVersions[(int)active.Kind] != syncVersion)
                {
                    ReleasePooledAt(i);
                }
            }
        }

        private void Reconcile(int unitId, StatusKind kind, int desiredCount, Transform pivot, StatusPresentationEntry entry)
        {
            if (entry != null && CanPlayActive(entry.ActiveVfxDefinition) && battleVfxPool != null)
            {
                ReconcilePooled(unitId, kind, desiredCount, pivot, entry);
                return;
            }

            ReleasePooled(unitId, kind);
        }

        private void ReconcilePooled(int unitId, StatusKind kind, int desiredCount, Transform pivot, StatusPresentationEntry entry)
        {
            int currentCount = 0;
            for (int i = 0; i < activePooledVfx.Count; i++)
            {
                if (activePooledVfx[i].UnitId == unitId && activePooledVfx[i].Kind == kind)
                {
                    currentCount++;
                }
            }

            while (currentCount < desiredCount)
            {
                VfxHandle handle = battleVfxPool.Play(
                    entry.ActiveVfxDefinition,
                    new VfxSpawnRequest(
                        pivot,
                        entry.LocalPosition,
                        Quaternion.Euler(entry.LocalEulerAngles),
                        entry.LocalScale == Vector3.zero ? Vector3.one : entry.LocalScale,
                        unitId));
                if (!handle.IsValid)
                {
                    return;
                }

                activePooledVfx.Add(new ActivePooledVfx(unitId, kind, handle));
                currentCount++;
            }

            while (currentCount > desiredCount)
            {
                for (int i = activePooledVfx.Count - 1; i >= 0; i--)
                {
                    ActivePooledVfx active = activePooledVfx[i];
                    if (active.UnitId != unitId || active.Kind != kind)
                    {
                        continue;
                    }

                    ReleasePooledAt(i);
                    currentCount--;
                    break;
                }
            }
        }

        private void ReleasePooled(int unitId, StatusKind kind)
        {
            for (int i = activePooledVfx.Count - 1; i >= 0; i--)
            {
                ActivePooledVfx active = activePooledVfx[i];
                if (active.UnitId == unitId && active.Kind == kind)
                {
                    ReleasePooledAt(i);
                }
            }
        }

        private void ReleasePooledByUnit(int unitId)
        {
            for (int i = activePooledVfx.Count - 1; i >= 0; i--)
            {
                if (activePooledVfx[i].UnitId == unitId)
                {
                    ReleasePooledAt(i);
                }
            }
        }

        private void ReleaseAllPooled()
        {
            for (int i = activePooledVfx.Count - 1; i >= 0; i--)
            {
                ReleasePooledAt(i);
            }
        }

        private void ReleasePooledAt(int index)
        {
            ActivePooledVfx active = activePooledVfx[index];
            activePooledVfx.RemoveAt(index);
            battleVfxPool?.Release(active.Handle);
        }

        private void PlayOneShots(int unitId, VfxDefinition definition, StatusPresentationEntry entry, int count)
        {
            if (!CanPlayOneShot(definition)
                || battleVfxPool == null
                || !pivotsByUnitId.TryGetValue(unitId, out Transform pivot)
                || pivot == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                battleVfxPool.Play(
                    definition,
                    new VfxSpawnRequest(
                        pivot,
                        entry.LocalPosition,
                        Quaternion.Euler(entry.LocalEulerAngles),
                        entry.LocalScale == Vector3.zero ? Vector3.one : entry.LocalScale,
                        unitId));
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

        private void ReconcileShadowStatusVfx(int unitId, StatusKind kind, StatusPresentationEntry entry)
        {
            if (!pivotsByUnitId.TryGetValue(unitId, out Transform pivot) || pivot == null)
            {
                return;
            }

            int totalStacks = 0;
            for (int i = 0; i < shadowStatuses.Count; i++)
            {
                ShadowStatus status = shadowStatuses[i];
                if (status.UnitId == unitId && status.Kind == kind)
                {
                    totalStacks += status.Stacks;
                }
            }

            Reconcile(unitId, kind, totalStacks, pivot, entry);
        }

        private void Prewarm()
        {
            if (presentationCatalog == null) return;
            StatusPresentationEntry[] entries = presentationCatalog.Entries;
            for (int i = 0; i < entries.Length; i++)
            {
                StatusPresentationEntry entry = entries[i];
                if (entry == null || entry.Mode != StatusPresentationMode.Vfx) continue;
                Prewarm(entry.ApplyVfxDefinition);
                Prewarm(entry.ActiveVfxDefinition);
                Prewarm(entry.RemoveVfxDefinition);
            }
        }

        private void Prewarm(VfxDefinition definition)
        {
            if (IsUsable(definition))
            {
                battleVfxPool?.Prewarm(definition);
            }
        }

        private static bool IsUsable(VfxDefinition definition)
        {
            return definition != null && definition.Prefab != null;
        }

        private static bool CanPlayActive(VfxDefinition definition)
        {
            return IsUsable(definition) && definition.LifetimeMode == VfxLifetimeMode.Manual;
        }

        private static bool CanPlayOneShot(VfxDefinition definition)
        {
            return IsUsable(definition) && definition.LifetimeMode != VfxLifetimeMode.Manual;
        }

        private readonly struct ActivePooledVfx
        {
            public readonly int UnitId;
            public readonly StatusKind Kind;
            public readonly VfxHandle Handle;

            public ActivePooledVfx(int unitId, StatusKind kind, VfxHandle handle)
            {
                UnitId = unitId;
                Kind = kind;
                Handle = handle;
            }
        }

        private readonly struct ShadowStatus
        {
            public readonly int UnitId; public readonly StatusKind Kind; public readonly int SourceUnitId; public readonly int Stacks;
            public ShadowStatus(int unitId, StatusKind kind, int sourceUnitId, int stacks) { UnitId = unitId; Kind = kind; SourceUnitId = sourceUnitId; Stacks = stacks; }
        }
    }
}
