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

        [Test]
        public void Lookup_UsesIndependentRunSpeedMultipliersForDefinitionsSharingPrefab()
        {
            GameObject prefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            UnitDefinition first = TestDefinitions.CreateUnit("first", 1);
            UnitDefinition second = TestDefinitions.CreateUnit("second", 1);
            first.UnitPrefab = prefabObject.GetComponent<UnitView>();
            second.UnitPrefab = first.UnitPrefab;
            first.RunAnimationSpeedMultiplier = 0.8f;
            second.RunAnimationSpeedMultiplier = 1.25f;
            var lookup = new BattlePresentationLookup();

            try
            {
                lookup.Rebuild(new[] { first, second }, null);

                Assert.IsTrue(lookup.TryGetUnitViewData(
                    BattlePresentationId.ForUnit(first),
                    out UnitView firstPrefab,
                    out float firstMultiplier));
                Assert.IsTrue(lookup.TryGetUnitViewData(
                    BattlePresentationId.ForUnit(second),
                    out UnitView secondPrefab,
                    out float secondMultiplier));
                Assert.AreSame(first.UnitPrefab, firstPrefab);
                Assert.AreSame(second.UnitPrefab, secondPrefab);
                Assert.AreEqual(0.8f, firstMultiplier);
                Assert.AreEqual(1.25f, secondMultiplier);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        public void Registry_AppliesRunSpeedMultiplierFromPresentationLookup()
        {
            GameObject parentObject = new GameObject("UnitRoot");
            GameObject prefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            definition.UnitPrefab = prefabObject.GetComponent<UnitView>();
            definition.RunAnimationSpeedMultiplier = 1.25f;
            BattlePresentationLookup lookup = CreateLookup(definition);
            var registry = new UnitViewRegistry(lookup, parentObject.transform, parentObject);
            var state = new UnitPresentationState(
                1,
                BattlePresentationId.ForUnit(definition),
                BattleSide.Player,
                default,
                10,
                10,
                0,
                100);

            try
            {
                UnitView view = registry.GetOrCreate(state);

                Assert.IsNotNull(view);
                Assert.AreEqual(1.25f, view.RunAnimationSpeedMultiplier);
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
        public void Registry_AppliesGlobalAnimationCrossFadeDurationToExistingAndNewViews()
        {
            GameObject parentObject = new GameObject("UnitRoot");
            GameObject prefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            definition.UnitPrefab = prefabObject.GetComponent<UnitView>();
            BattlePresentationLookup lookup = CreateLookup(definition);
            var registry = new UnitViewRegistry(lookup, parentObject.transform, parentObject);
            var firstState = new UnitPresentationState(1, BattlePresentationId.ForUnit(definition), BattleSide.Player, default, 10, 10, 0, 100);
            var secondState = new UnitPresentationState(2, BattlePresentationId.ForUnit(definition), BattleSide.Enemy, default, 10, 10, 0, 100);

            try
            {
                UnitView firstView = registry.GetOrCreate(firstState);
                registry.SetAnimationCrossFadeDuration(0.2f);
                UnitView secondView = registry.GetOrCreate(secondState);

                Assert.AreEqual(0.2f, firstView.AnimationCrossFadeDuration);
                Assert.AreEqual(0.2f, secondView.AnimationCrossFadeDuration);
            }
            finally
            {
                registry.ReleaseAll();
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(prefabObject);
            }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RunSpeedMultiplier_InvalidValueFallsBackToOne(float multiplier)
        {
            Assert.AreEqual(1f, UnitView.ResolveRunAnimationSpeedMultiplier(multiplier));
        }

        private static BattlePresentationLookup CreateLookup(UnitDefinition definition)
        {
            var lookup = new BattlePresentationLookup();
            lookup.Rebuild(new[] { definition }, null);
            return lookup;
        }
    }
}
