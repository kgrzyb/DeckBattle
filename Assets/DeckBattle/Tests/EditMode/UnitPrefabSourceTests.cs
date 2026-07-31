using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class UnitPrefabSourceTests
    {
        [Test]
        public void ControllerAndView_DoNotOwnUnitViewLookupDictionaries()
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

            Assert.IsNull(typeof(BattleController).GetField("unitViewByRuntimeId", Fields));
            Assert.IsNull(typeof(BattleView).GetField("unitViewByUnitId", Fields));
        }

        [Test]
        public void Registry_GetOrCreate_UsesCatalogPrefabAndPreventsDuplicates()
        {
            GameObject parentObject = new GameObject("UnitRoot");
            GameObject prefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            definition.UnitPrefab = prefabObject.GetComponent<UnitView>();
            BattlePresentationCatalog catalog = CreateCatalog(BattlePresentationId.ForUnit(definition), definition.UnitPrefab);
            var registry = new UnitViewRegistry(catalog, parentObject.transform, parentObject);
            var state = new UnitPresentationState(1, BattlePresentationId.ForUnit(definition), BattleSide.Player, default, 10, 10, 0, 100);

            try
            {
                UnitView view = registry.GetOrCreate(state);
                UnitView duplicate = registry.GetOrCreate(state);

                Assert.IsNotNull(view);
                Assert.AreNotSame(definition.UnitPrefab, view);
                Assert.AreSame(view, duplicate);
                Assert.AreEqual(parentObject.transform, view.transform.parent);
            }
            finally
            {
                registry.ReleaseAll();
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        public void Registry_Release_RemovesItsUnitView()
        {
            GameObject parentObject = new GameObject("UnitRoot");
            GameObject prefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            definition.UnitPrefab = prefabObject.GetComponent<UnitView>();
            int presentationId = BattlePresentationId.ForUnit(definition);
            BattlePresentationCatalog catalog = CreateCatalog(presentationId, definition.UnitPrefab);
            var registry = new UnitViewRegistry(catalog, parentObject.transform, parentObject);
            var state = new UnitPresentationState(1, presentationId, BattleSide.Player, default, 10, 10, 0, 100);

            try
            {
                registry.GetOrCreate(state);
                registry.Release(state.UnitId);

                Assert.IsFalse(registry.TryGet(state.UnitId, out UnitView _));
            }
            finally
            {
                registry.ReleaseAll();
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        public void Catalog_MissingUnitEntry_ReturnsFalse()
        {
            BattlePresentationCatalog catalog = ScriptableObject.CreateInstance<BattlePresentationCatalog>();

            try
            {
                Assert.IsFalse(catalog.TryGetUnitPrefab(17, out UnitView prefab));
                Assert.IsNull(prefab);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        private static BattlePresentationCatalog CreateCatalog(int presentationId, UnitView prefab)
        {
            BattlePresentationCatalog catalog = ScriptableObject.CreateInstance<BattlePresentationCatalog>();
            FieldInfo field = typeof(BattlePresentationCatalog).GetField("units", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(catalog, new[] { new UnitPresentationEntry { PresentationId = presentationId, Prefab = prefab } });
            typeof(BattlePresentationCatalog).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(catalog, null);
            return catalog;
        }
    }
}
