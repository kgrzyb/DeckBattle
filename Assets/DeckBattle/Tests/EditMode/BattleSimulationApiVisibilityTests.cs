using System.Reflection;
using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleSimulationApiVisibilityTests
    {
        [Test]
        public void StateMutationMethods_AreInternal()
        {
            AssertInternal("AdvanceTime", typeof(float));
            AssertInternal("MoveUnit", typeof(UnitRuntimeState), typeof(HexCoord));
            AssertInternal("StartUnitMovement", typeof(UnitRuntimeState), typeof(HexCoord));
            AssertInternal("CompleteUnitMovement", typeof(UnitRuntimeState));
            AssertInternal("DefeatUnit", typeof(UnitRuntimeState));
            AssertInternal(
                "SpawnProjectile",
                typeof(UnitRuntimeState),
                typeof(UnitRuntimeState),
                typeof(ProjectileCombatSpec),
                typeof(int),
                typeof(bool),
                typeof(ProjectileImpactCombatSpec));
            AssertInternal("RemoveProjectileAt", typeof(int));
            AssertInternal("CompleteBattle", typeof(BattleSide), typeof(bool));
        }

        private static void AssertInternal(string methodName, params System.Type[] parameterTypes)
        {
            MethodInfo method = typeof(BattleSimulation).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);

            Assert.IsNotNull(method, methodName);
            Assert.IsTrue(method.IsAssembly, methodName + " must be internal.");
        }

        [Test]
        public void PublicApi_DoesNotExposeStateMutationMethods()
        {
            MethodInfo[] publicMethods = typeof(BattleSimulation).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < publicMethods.Length; i++)
            {
                string methodName = publicMethods[i].Name;
                Assert.IsFalse(
                    methodName == "AdvanceTime"
                    || methodName == "MoveUnit"
                    || methodName == "StartUnitMovement"
                    || methodName == "CompleteUnitMovement"
                    || methodName == "DefeatUnit"
                    || methodName == "SpawnProjectile"
                    || methodName == "RemoveProjectileAt"
                    || methodName == "CompleteBattle",
                    "Public mutation method: " + methodName);
            }
        }
    }
}
