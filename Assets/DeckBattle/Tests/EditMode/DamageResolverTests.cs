using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class DamageResolverTests
    {
        [TearDown] public void TearDown() { TestDefinitions.DestroyCreatedObjects(); }

        [Test]
        public void ShieldAbsorbsDamageBeforeHpAndSleepRemains()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[1];
            Apply(simulation, target, StatusKind.Shield, StatusCategory.Beneficial, 3f);
            Apply(simulation, target, StatusKind.Sleep, StatusCategory.HarmfulCrowdControl, 0f);

            HitResolutionResult result = DamageResolver.Resolve(simulation, target, new DamageRequest(simulation.Units[0], 3), null);

            Assert.AreEqual(0, result.Damage);
            Assert.AreEqual(target.Definition.MaxHp, target.CurrentHp);
            Assert.AreEqual(1, target.Statuses.Count);
            Assert.AreEqual(StatusKind.Sleep, target.Statuses[0].Kind);
        }

        [Test]
        public void DrainRemovesManaWithoutCreatingAStatus()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[1];
            target.CurrentMana = 8;
            StatusDefinition drain = Definition(StatusKind.Drain, StatusCategory.HarmfulStatReduction, 5f);
            drain.StackingRule = StatusStackingRule.InstantOnly;

            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, target, new StatusApplicationRequest(drain, 1)));
            Assert.AreEqual(3, target.CurrentMana);
            Assert.AreEqual(0, target.Statuses.Count);
        }

        private static void Apply(BattleSimulation simulation, UnitRuntimeState target, StatusKind kind, StatusCategory category, float magnitude)
        {
            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, target, new StatusApplicationRequest(Definition(kind, category, magnitude), 1)));
        }

        private static StatusDefinition Definition(StatusKind kind, StatusCategory category, float magnitude)
        {
            StatusDefinition definition = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            definition.Kind = kind; definition.Category = category; definition.DefaultDuration = 5f; definition.DefaultMagnitude = magnitude; definition.StackingRule = StatusStackingRule.RefreshPerSource;
            return definition;
        }

        private static BattleSimulation CreateSimulation()
        {
            UnitDefinition first = TestDefinitions.CreateUnit("first", 1);
            UnitDefinition second = TestDefinitions.CreateUnit("second", 1);
            return BattleSimulation.Create(new HexBoard(3, 3, 1f), new[] { new UnitSpawnData(1, first, BattleSide.Player, new HexCoord(0, 0)), new UnitSpawnData(2, second, BattleSide.Enemy, new HexCoord(1, 0)) });
        }
    }
}
