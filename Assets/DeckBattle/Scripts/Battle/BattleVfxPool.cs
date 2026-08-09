using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    // Central lifecycle owner for pooled, presentation-only VFX instances.
    public sealed class BattleVfxPool : MonoBehaviour
    {
        [SerializeField] private Transform poolRoot;
        [SerializeField, Min(1)] private int maxPrewarmedInstances = 64;

        private readonly Dictionary<PooledVfxView, Stack<PooledVfxView>> availableByPrefab = new Dictionary<PooledVfxView, Stack<PooledVfxView>>(8);
        private readonly Dictionary<VfxDefinition, int> activeCountByDefinition = new Dictionary<VfxDefinition, int>(16);
        private readonly List<ActiveVfx> activeVfx = new List<ActiveVfx>(32);
        private readonly List<PooledVfxView> retiredViews = new List<PooledVfxView>(8);
        private float combatSpeed = 1f;
        private int nextInstanceId = 1;
        private int createdInstanceCount;
        private int prewarmedInstanceCount;
        private int poolMissCount;
        private int skippedSpawnCount;
        private int peakActiveCount;

        internal int ActiveCount
        {
            get { return activeVfx.Count; }
        }

        internal int CreatedInstanceCount
        {
            get { return createdInstanceCount; }
        }

        public int PrewarmedInstanceCount
        {
            get { return prewarmedInstanceCount; }
        }

        public int PoolMissCount
        {
            get { return poolMissCount; }
        }

        public int SkippedSpawnCount
        {
            get { return skippedSpawnCount; }
        }

        public int PeakActiveCount
        {
            get { return peakActiveCount; }
        }

        private Transform PoolRoot
        {
            get { return poolRoot != null ? poolRoot : transform; }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        public void Prewarm(VfxDefinition definition)
        {
            if (definition == null || definition.Prefab == null)
            {
                return;
            }

            int desiredCount = Mathf.Min(definition.PrewarmCount, definition.MaxActiveCount);
            Stack<PooledVfxView> pool = GetPool(definition.Prefab);
            while (pool.Count < desiredCount && prewarmedInstanceCount < maxPrewarmedInstances)
            {
                pool.Push(CreateInstance(definition.Prefab));
                prewarmedInstanceCount++;
            }
        }

        public void Prewarm(IReadOnlyList<VfxDefinition> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                Prewarm(definitions[i]);
            }
        }

        public VfxHandle Play(VfxDefinition definition, in VfxSpawnRequest request)
        {
            if (definition == null || definition.Prefab == null || GetActiveCount(definition) >= definition.MaxActiveCount)
            {
                if (definition != null && definition.Prefab != null)
                {
                    skippedSpawnCount++;
                }

                return default;
            }

            PooledVfxView view = GetFromPool(definition.Prefab);
            int generation = view.Play(definition, request, PoolRoot, combatSpeed);
            activeVfx.Add(new ActiveVfx(view, definition, request.OwnerUnitId, request.FollowAnchor));
            IncrementActiveCount(definition);
            peakActiveCount = Mathf.Max(peakActiveCount, activeVfx.Count);
            return new VfxHandle(view.InstanceId, generation);
        }

        public bool Release(VfxHandle handle)
        {
            if (!handle.IsValid)
            {
                return false;
            }

            for (int i = activeVfx.Count - 1; i >= 0; i--)
            {
                ActiveVfx active = activeVfx[i];
                if (active.View == null
                    || active.View.InstanceId != handle.InstanceId
                    || active.View.Generation != handle.Generation)
                {
                    continue;
                }

                ReturnAt(i);
                return true;
            }

            return false;
        }

        public void ReleaseOwnedByUnit(int unitId)
        {
            if (unitId <= 0)
            {
                return;
            }

            for (int i = activeVfx.Count - 1; i >= 0; i--)
            {
                ActiveVfx active = activeVfx[i];
                if (active.OwnerUnitId == unitId && active.FollowsAnchor)
                {
                    ReturnAt(i);
                }
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = activeVfx.Count - 1; i >= 0; i--)
            {
                ActiveVfx active = activeVfx[i];
                if (active.View == null || active.View.Advance(deltaTime))
                {
                    ReturnAt(i);
                }
            }
        }

        public void SetCombatSpeed(float speed)
        {
            float safeSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
            if (Mathf.Approximately(combatSpeed, safeSpeed))
            {
                return;
            }

            combatSpeed = safeSpeed;
            for (int i = 0; i < activeVfx.Count; i++)
            {
                PooledVfxView view = activeVfx[i].View;
                if (view != null)
                {
                    view.SetCombatSpeed(combatSpeed);
                }
            }
        }

        public void ReleaseAll()
        {
            for (int i = activeVfx.Count - 1; i >= 0; i--)
            {
                ReturnAt(i);
            }
        }

        public void ResetDiagnostics()
        {
            poolMissCount = 0;
            skippedSpawnCount = 0;
            peakActiveCount = activeVfx.Count;
        }

        // Call after a battle or loading transition, never from the combat hot path.
        public void TrimRetired()
        {
            for (int i = retiredViews.Count - 1; i >= 0; i--)
            {
                PooledVfxView view = retiredViews[i];
                if (view != null)
                {
                    Object.Destroy(view.gameObject);
                }
            }

            retiredViews.Clear();
        }

        internal int GetAvailableCount(PooledVfxView prefab)
        {
            return prefab != null && availableByPrefab.TryGetValue(prefab, out Stack<PooledVfxView> pool)
                ? pool.Count
                : 0;
        }

        private PooledVfxView GetFromPool(PooledVfxView prefab)
        {
            Stack<PooledVfxView> pool = GetPool(prefab);
            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            poolMissCount++;
            return CreateInstance(prefab);
        }

        private PooledVfxView CreateInstance(PooledVfxView prefab)
        {
            PooledVfxView view = Object.Instantiate(prefab, PoolRoot);
            view.AssignPoolIdentity(nextInstanceId);
            nextInstanceId = nextInstanceId == int.MaxValue ? 1 : nextInstanceId + 1;
            createdInstanceCount++;
            view.gameObject.SetActive(false);
            return view;
        }

        private void ReturnAt(int activeIndex)
        {
            ActiveVfx active = activeVfx[activeIndex];
            activeVfx.RemoveAt(activeIndex);
            DecrementActiveCount(active.Definition);

            if (active.View == null)
            {
                return;
            }

            active.View.Release();
            active.View.transform.SetParent(PoolRoot, false);
            Stack<PooledVfxView> pool = GetPool(active.Definition.Prefab);
            if (pool.Count < active.Definition.MaxRetainedCount)
            {
                pool.Push(active.View);
            }
            else
            {
                retiredViews.Add(active.View);
            }
        }

        private Stack<PooledVfxView> GetPool(PooledVfxView prefab)
        {
            if (!availableByPrefab.TryGetValue(prefab, out Stack<PooledVfxView> pool))
            {
                pool = new Stack<PooledVfxView>(4);
                availableByPrefab.Add(prefab, pool);
            }

            return pool;
        }

        private int GetActiveCount(VfxDefinition definition)
        {
            return activeCountByDefinition.TryGetValue(definition, out int count) ? count : 0;
        }

        private void IncrementActiveCount(VfxDefinition definition)
        {
            activeCountByDefinition.TryGetValue(definition, out int count);
            activeCountByDefinition[definition] = count + 1;
        }

        private void DecrementActiveCount(VfxDefinition definition)
        {
            if (!activeCountByDefinition.TryGetValue(definition, out int count) || count <= 1)
            {
                activeCountByDefinition.Remove(definition);
                return;
            }

            activeCountByDefinition[definition] = count - 1;
        }

        private readonly struct ActiveVfx
        {
            public readonly PooledVfxView View;
            public readonly VfxDefinition Definition;
            public readonly int OwnerUnitId;
            public readonly bool FollowsAnchor;

            public ActiveVfx(PooledVfxView view, VfxDefinition definition, int ownerUnitId, bool followsAnchor)
            {
                View = view;
                Definition = definition;
                OwnerUnitId = ownerUnitId;
                FollowsAnchor = followsAnchor;
            }
        }
    }
}
