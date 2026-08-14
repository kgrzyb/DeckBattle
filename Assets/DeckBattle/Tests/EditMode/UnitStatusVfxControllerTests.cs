using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class UnitStatusVfxControllerTests
    {
        [Test]
        public void HandleStatusEvent_AppliedStatusPlaysConfiguredOneShotThroughSharedPool()
        {
            GameObject controllerObject = new GameObject("StatusVfxController", typeof(UnitStatusVfxController));
            GameObject poolObject = new GameObject("BattleVfxPool", typeof(BattleVfxPool));
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject prefabObject = new GameObject("ApplyPooledVfx", typeof(PooledVfxView));
            StatusPresentationCatalog catalog = ScriptableObject.CreateInstance<StatusPresentationCatalog>();
            VfxDefinition definition = ScriptableObject.CreateInstance<VfxDefinition>();
            try
            {
                prefabObject.SetActive(false);
                definition.Prefab = prefabObject.GetComponent<PooledVfxView>();
                definition.LifetimeMode = VfxLifetimeMode.Duration;
                definition.FallbackLifetime = 0.1f;
                definition.PrewarmCount = 0;
                definition.MaxActiveCount = 2;
                definition.MaxRetainedCount = 2;
                SetEntries(catalog, new[]
                {
                    new StatusPresentationEntry
                    {
                        Kind = StatusKind.Haste,
                        Mode = StatusPresentationMode.IconAndVfx,
                        ApplyVfxDefinition = definition
                    }
                });

                UnitStatusVfxController controller = controllerObject.GetComponent<UnitStatusVfxController>();
                BattleVfxPool pool = poolObject.GetComponent<BattleVfxPool>();
                controller.Initialize(catalog, pool);
                controller.BindPresentationUnit(1, unitObject.GetComponent<UnitView>());

                controller.HandleStatusEvent(BattleEvent.StatusApplied(1, 2, StatusKind.Haste, 1, 3f));

                Assert.AreEqual(1, pool.ActiveCount);
                pool.Tick(0.2f);
                Assert.AreEqual(0, pool.ActiveCount);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(unitObject);
                Object.DestroyImmediate(poolObject);
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void HandleStatusEvent_UsesSharedBattleVfxPoolWhenDefinitionIsConfigured()
        {
            GameObject controllerObject = new GameObject("StatusVfxController", typeof(UnitStatusVfxController));
            GameObject poolObject = new GameObject("BattleVfxPool", typeof(BattleVfxPool));
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject prefabObject = new GameObject("ActivePooledVfx", typeof(PooledVfxView));
            StatusPresentationCatalog catalog = ScriptableObject.CreateInstance<StatusPresentationCatalog>();
            VfxDefinition definition = ScriptableObject.CreateInstance<VfxDefinition>();
            try
            {
                prefabObject.SetActive(false);
                definition.Prefab = prefabObject.GetComponent<PooledVfxView>();
                definition.LifetimeMode = VfxLifetimeMode.Manual;
                definition.PrewarmCount = 0;
                definition.MaxActiveCount = 2;
                definition.MaxRetainedCount = 2;
                SetEntries(catalog, new[]
                {
                    new StatusPresentationEntry
                    {
                        Kind = StatusKind.Haste,
                        Mode = StatusPresentationMode.Vfx,
                        ActiveVfxDefinition = definition
                    }
                });

                UnitStatusVfxController controller = controllerObject.GetComponent<UnitStatusVfxController>();
                BattleVfxPool pool = poolObject.GetComponent<BattleVfxPool>();
                controller.Initialize(catalog, pool);
                controller.BindPresentationUnit(1, unitObject.GetComponent<UnitView>());

                controller.HandleStatusEvent(BattleEvent.StatusApplied(1, 2, StatusKind.Haste, 1, 3f));
                Assert.AreEqual(1, pool.ActiveCount);

                controller.Release(1);
                Assert.AreEqual(0, pool.ActiveCount);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(unitObject);
                Object.DestroyImmediate(poolObject);
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void HandleStatusEvent_ParentsActiveVfxToConfiguredUnitAnchor()
        {
            GameObject controllerObject = new GameObject("StatusVfxController", typeof(UnitStatusVfxController));
            GameObject poolObject = new GameObject("BattleVfxPool", typeof(BattleVfxPool));
            GameObject unitObject = new GameObject("Unit");
            GameObject anchorObject = new GameObject("Overhead");
            GameObject prefabObject = new GameObject("ActivePooledVfx", typeof(PooledVfxView));
            StatusPresentationCatalog catalog = ScriptableObject.CreateInstance<StatusPresentationCatalog>();
            VfxDefinition definition = ScriptableObject.CreateInstance<VfxDefinition>();
            try
            {
                unitObject.SetActive(false);
                anchorObject.transform.SetParent(unitObject.transform, false);
                UnitVfxAnchors anchors = unitObject.AddComponent<UnitVfxAnchors>();
                SetPrivateField(anchors, "overhead", anchorObject.transform);
                UnitView unitView = unitObject.AddComponent<UnitView>();
                unitObject.SetActive(true);

                prefabObject.SetActive(false);
                definition.Prefab = prefabObject.GetComponent<PooledVfxView>();
                definition.LifetimeMode = VfxLifetimeMode.Manual;
                definition.PrewarmCount = 0;
                definition.MaxActiveCount = 2;
                definition.MaxRetainedCount = 2;
                SetEntries(catalog, new[]
                {
                    new StatusPresentationEntry
                    {
                        Kind = StatusKind.Haste,
                        Mode = StatusPresentationMode.Vfx,
                        ActiveVfxDefinition = definition,
                        ActiveAnchor = UnitVfxAnchor.Overhead
                    }
                });

                UnitStatusVfxController controller = controllerObject.GetComponent<UnitStatusVfxController>();
                BattleVfxPool pool = poolObject.GetComponent<BattleVfxPool>();
                controller.Initialize(catalog, pool);
                controller.BindPresentationUnit(1, unitView);

                controller.HandleStatusEvent(BattleEvent.StatusApplied(1, 2, StatusKind.Haste, 1, 3f));

                PooledVfxView[] views = Object.FindObjectsOfType<PooledVfxView>(true);
                PooledVfxView activeView = null;
                for (int i = 0; i < views.Length; i++)
                {
                    if (views[i] != definition.Prefab && views[i].gameObject.activeSelf)
                    {
                        activeView = views[i];
                        break;
                    }
                }

                Assert.IsNotNull(activeView);
                Assert.AreSame(anchorObject.transform, activeView.transform.parent);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prefabObject);
                Object.DestroyImmediate(unitObject);
                Object.DestroyImmediate(poolObject);
                Object.DestroyImmediate(controllerObject);
            }
        }

        private static void SetEntries(StatusPresentationCatalog catalog, StatusPresentationEntry[] entries)
        {
            typeof(StatusPresentationCatalog)
                .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(catalog, entries);
            typeof(StatusPresentationCatalog)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(catalog, null);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }
    }
}
