using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    [Category("Vfx")]
    public sealed class BattleVfxPoolTests
    {
        [Test]
        public void Prewarm_CreatesConfiguredInactiveInstances()
        {
            VfxTestContext context = new VfxTestContext(3, 4, VfxLifetimeMode.Duration);
            try
            {
                context.Pool.Prewarm(context.Definition);

                Assert.AreEqual(3, context.Pool.GetAvailableCount(context.Prefab));
                Assert.AreEqual(3, context.Pool.CreatedInstanceCount);
                Assert.AreEqual(0, context.Pool.ActiveCount);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void DurationEffect_ReturnsToPoolAndIsReused()
        {
            VfxTestContext context = new VfxTestContext(0, 2, VfxLifetimeMode.Duration);
            try
            {
                VfxHandle first = context.Pool.Play(context.Definition, VfxSpawnRequest.AtWorld(Vector3.one));
                Assert.IsTrue(first.IsValid);
                Assert.AreEqual(1, context.Pool.ActiveCount);
                Assert.AreEqual(1, context.Pool.CreatedInstanceCount);

                context.Pool.Tick(0.11f);
                Assert.AreEqual(0, context.Pool.ActiveCount);
                Assert.AreEqual(1, context.Pool.GetAvailableCount(context.Prefab));

                VfxHandle second = context.Pool.Play(context.Definition, VfxSpawnRequest.AtWorld(Vector3.zero));
                Assert.IsTrue(second.IsValid);
                Assert.AreEqual(1, context.Pool.CreatedInstanceCount);
                Assert.AreNotEqual(first.Generation, second.Generation);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void StaleHandle_CannotReleaseReusedInstance()
        {
            VfxTestContext context = new VfxTestContext(0, 1, VfxLifetimeMode.Duration);
            try
            {
                VfxHandle first = context.Pool.Play(context.Definition, VfxSpawnRequest.AtWorld(Vector3.zero));
                context.Pool.Tick(0.11f);

                context.Definition.LifetimeMode = VfxLifetimeMode.Manual;
                VfxHandle second = context.Pool.Play(context.Definition, VfxSpawnRequest.AtWorld(Vector3.one));

                Assert.IsFalse(context.Pool.Release(first));
                Assert.AreEqual(1, context.Pool.ActiveCount);
                Assert.IsTrue(context.Pool.Release(second));
                Assert.AreEqual(0, context.Pool.ActiveCount);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void ReleaseOwnedByUnit_ReleasesOnlyEffectsFollowingThatUnit()
        {
            VfxTestContext context = new VfxTestContext(0, 2, VfxLifetimeMode.Manual);
            GameObject firstAnchor = new GameObject("FirstAnchor");
            GameObject secondAnchor = new GameObject("SecondAnchor");
            try
            {
                context.Pool.Play(context.Definition, new VfxSpawnRequest(firstAnchor.transform, Vector3.zero, Quaternion.identity, Vector3.one, 10));
                VfxHandle second = context.Pool.Play(context.Definition, new VfxSpawnRequest(secondAnchor.transform, Vector3.zero, Quaternion.identity, Vector3.one, 20));

                context.Pool.ReleaseOwnedByUnit(10);

                Assert.AreEqual(1, context.Pool.ActiveCount);
                Assert.IsTrue(context.Pool.Release(second));
            }
            finally
            {
                Object.DestroyImmediate(firstAnchor);
                Object.DestroyImmediate(secondAnchor);
                context.Dispose();
            }
        }

        [Test]
        public void ActiveLimit_SkipsAdditionalEffectWithoutCreatingInstance()
        {
            VfxTestContext context = new VfxTestContext(0, 1, VfxLifetimeMode.Manual);
            try
            {
                VfxHandle first = context.Pool.Play(context.Definition, VfxSpawnRequest.AtWorld(Vector3.zero));
                VfxHandle second = context.Pool.Play(context.Definition, VfxSpawnRequest.AtWorld(Vector3.one));

                Assert.IsTrue(first.IsValid);
                Assert.IsFalse(second.IsValid);
                Assert.AreEqual(1, context.Pool.ActiveCount);
                Assert.AreEqual(1, context.Pool.CreatedInstanceCount);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void Diagnostics_RecordPoolMissPeakAndSkippedSpawn()
        {
            VfxTestContext context = new VfxTestContext(0, 1, VfxLifetimeMode.Manual);
            try
            {
                VfxHandle first = context.Pool.Play(context.Definition, VfxSpawnRequest.AtWorld(Vector3.zero));
                VfxHandle second = context.Pool.Play(context.Definition, VfxSpawnRequest.AtWorld(Vector3.one));

                Assert.IsTrue(first.IsValid);
                Assert.IsFalse(second.IsValid);
                Assert.AreEqual(1, context.Pool.PoolMissCount);
                Assert.AreEqual(1, context.Pool.PeakActiveCount);
                Assert.AreEqual(1, context.Pool.SkippedSpawnCount);

                context.Pool.Release(first);
                context.Pool.ResetDiagnostics();
                Assert.AreEqual(0, context.Pool.PoolMissCount);
                Assert.AreEqual(0, context.Pool.SkippedSpawnCount);
                Assert.AreEqual(0, context.Pool.PeakActiveCount);
            }
            finally
            {
                context.Dispose();
            }
        }

        private sealed class VfxTestContext
        {
            public readonly GameObject PoolObject;
            public readonly GameObject PrefabObject;
            public readonly BattleVfxPool Pool;
            public readonly PooledVfxView Prefab;
            public readonly VfxDefinition Definition;

            public VfxTestContext(int prewarmCount, int maxActiveCount, VfxLifetimeMode lifetimeMode)
            {
                PoolObject = new GameObject("VfxPool", typeof(BattleVfxPool));
                PrefabObject = new GameObject("VfxPrefab", typeof(PooledVfxView));
                Prefab = PrefabObject.GetComponent<PooledVfxView>();
                PrefabObject.SetActive(false);
                Definition = ScriptableObject.CreateInstance<VfxDefinition>();
                Definition.Prefab = Prefab;
                Definition.PrewarmCount = prewarmCount;
                Definition.MaxActiveCount = maxActiveCount;
                Definition.MaxRetainedCount = maxActiveCount;
                Definition.FallbackLifetime = 0.1f;
                Definition.LifetimeMode = lifetimeMode;
                Pool = PoolObject.GetComponent<BattleVfxPool>();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Definition);
                Object.DestroyImmediate(PrefabObject);
                Object.DestroyImmediate(PoolObject);
            }
        }
    }
}
