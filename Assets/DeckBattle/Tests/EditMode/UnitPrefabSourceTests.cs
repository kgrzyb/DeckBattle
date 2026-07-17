using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DeckBattle.Tests
{
    public sealed class UnitPrefabSourceTests
    {
        [Test]
        public void BattleController_CreateUnitView_UsesUnitDefinitionPrefab()
        {
            GameObject controllerObject = new GameObject("Controller", typeof(BattleController));
            GameObject prefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            definition.UnitPrefab = prefabObject.GetComponent<UnitView>();

            try
            {
                UnitView view = InvokeCreateUnitView<BattleController>(controllerObject.GetComponent<BattleController>(), definition);

                Assert.IsNotNull(view);
                Assert.AreNotSame(definition.UnitPrefab, view);
                Assert.AreEqual(controllerObject.transform, view.transform.parent);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        public void BattleView_CreateUnitView_UsesUnitDefinitionPrefab()
        {
            GameObject viewObject = new GameObject("BattleView", typeof(BattleView));
            GameObject prefabObject = new GameObject("UnitPrefab", typeof(UnitView));
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);
            definition.UnitPrefab = prefabObject.GetComponent<UnitView>();

            try
            {
                UnitView view = InvokeCreateUnitView<BattleView>(viewObject.GetComponent<BattleView>(), definition);

                Assert.IsNotNull(view);
                Assert.AreNotSame(definition.UnitPrefab, view);
                Assert.AreEqual(viewObject.transform, view.transform.parent);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(prefabObject);
            }
        }

        [Test]
        public void BattleController_CreateUnitView_RejectsMissingUnitDefinitionPrefab()
        {
            GameObject controllerObject = new GameObject("Controller", typeof(BattleController));
            UnitDefinition definition = TestDefinitions.CreateUnit("unit", 1);

            try
            {
                LogAssert.Expect(LogType.Error, "Runtime unit definition is missing UnitPrefab.");

                UnitView view = InvokeCreateUnitView<BattleController>(controllerObject.GetComponent<BattleController>(), definition);

                Assert.IsNull(view);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(controllerObject);
            }
        }

        private static UnitView InvokeCreateUnitView<T>(T target, UnitDefinition definition)
        {
            MethodInfo method = typeof(T).GetMethod("CreateUnitView", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (UnitView)method.Invoke(target, new object[] { definition });
        }
    }
}
