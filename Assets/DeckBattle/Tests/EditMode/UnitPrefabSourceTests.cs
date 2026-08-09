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
        public void Registry_GetOrCreate_UsesDefinitionLookupAndPreventsDuplicates()
        {
            GameObject parentObject = new GameObject("UnitRoot");
            GameObject prefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            definition.UnitPrefab = prefabObject.GetComponent<UnitView>();
            BattlePresentationLookup lookup = CreateLookup(definition);
            var registry = new UnitViewRegistry(lookup, parentObject.transform, parentObject);
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
            BattlePresentationLookup lookup = CreateLookup(definition);
            var registry = new UnitViewRegistry(lookup, parentObject.transform, parentObject);
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
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        public void Lookup_MissingUnitDefinition_ReturnsFalse()
        {
            var lookup = new BattlePresentationLookup();

            Assert.IsFalse(lookup.TryGetUnitPrefab(17, out UnitView prefab));
            Assert.IsNull(prefab);
        }

        private static BattlePresentationLookup CreateLookup(UnitDefinition definition)
        {
            var lookup = new BattlePresentationLookup();
            lookup.Rebuild(new[] { definition }, null);
            return lookup;
        }
    }
}
