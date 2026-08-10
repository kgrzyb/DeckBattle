using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    [Category("Vfx")]
    public sealed class BattleVfxPresenterTests
    {
        [Test]
        public void Constructor_PrewarmsUsableDefaultProfileEffects()
        {
            VfxPresenterTestContext context = new VfxPresenterTestContext(BattleVfxCue.AttackFired);
            try
            {
                Assert.AreEqual(context.Definition.PrewarmCount, context.Pool.GetAvailableCount(context.Definition.Prefab));
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void Handle_AttackFiredSpawnsConfiguredDefaultVfx()
        {
            VfxPresenterTestContext context = new VfxPresenterTestContext(BattleVfxCue.AttackFired);
            try
            {
                context.Presenter.Handle(BattleEvent.AttackFired(
                    1,
                    2,
                    3,
                    0f,
                    new HexCoord(0, 0),
                    new HexCoord(1, 0)));

                Assert.AreEqual(1, context.Pool.ActiveCount);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void Handle_DamageUsesCriticalCueWhenEventIsCritical()
        {
            VfxPresenterTestContext context = new VfxPresenterTestContext(BattleVfxCue.CriticalImpact);
            try
            {
                context.Presenter.Handle(BattleEvent.UnitDamaged(2, 4, 6, new HexCoord(1, 0), true));

                Assert.AreEqual(1, context.Pool.ActiveCount);
            }
            finally
            {
                context.Dispose();
            }
        }

        [Test]
        public void Handle_ManualAttackWindupIsReleasedWhenSequenceIsCancelled()
        {
            VfxPresenterTestContext context = new VfxPresenterTestContext(BattleVfxCue.AttackWindup);
            try
            {
                context.Presenter.Handle(BattleEvent.AttackWindupStarted(
                    1,
                    2,
                    3,
                    0.5f,
                    new HexCoord(1, 0)));
                Assert.AreEqual(1, context.Pool.ActiveCount);

                context.Presenter.Handle(BattleEvent.AttackWindupCancelled(1, 2, 3));
                Assert.AreEqual(0, context.Pool.ActiveCount);
            }
            finally
            {
                context.Dispose();
            }
        }

        private sealed class VfxPresenterTestContext
        {
            public readonly GameObject PoolObject;
            public readonly GameObject PrefabObject;
            public readonly BattleVfxPool Pool;
            public readonly VfxDefinition Definition;
            public readonly BattleVfxProfile Profile;
            public readonly BattleVfxPresenter Presenter;

            public VfxPresenterTestContext(BattleVfxCue cue)
            {
                PoolObject = new GameObject("VfxPool", typeof(BattleVfxPool));
                PrefabObject = new GameObject("VfxPrefab", typeof(PooledVfxView));
                PrefabObject.SetActive(false);
                Definition = ScriptableObject.CreateInstance<VfxDefinition>();
                Definition.Prefab = PrefabObject.GetComponent<PooledVfxView>();
                Definition.LifetimeMode = VfxLifetimeMode.Manual;
                Definition.MaxActiveCount = 2;
                Definition.MaxRetainedCount = 2;
                Profile = ScriptableObject.CreateInstance<BattleVfxProfile>();
                SetBindings(Profile, new[]
                {
                    new BattleVfxBinding
                    {
                        Cue = cue,
                        Effect = Definition,
                        Subject = VfxSpawnSubject.World,
                        Anchor = UnitVfxAnchor.Body,
                        LocalScale = Vector3.one
                    }
                });

                Pool = PoolObject.GetComponent<BattleVfxPool>();
                Presenter = new BattleVfxPresenter(
                    null,
                    null,
                    null,
                    new Dictionary<int, UnitPresentationState>(),
                    Pool,
                    Profile);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Profile);
                Object.DestroyImmediate(Definition);
                Object.DestroyImmediate(PrefabObject);
                Object.DestroyImmediate(PoolObject);
            }

            private static void SetBindings(BattleVfxProfile profile, BattleVfxBinding[] bindings)
            {
                typeof(BattleVfxProfile)
                    .GetField("bindings", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(profile, bindings);
                typeof(BattleVfxProfile)
                    .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(profile, null);
            }
        }
    }
}
