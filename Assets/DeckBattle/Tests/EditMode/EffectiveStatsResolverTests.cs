using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class EffectiveStatsResolverTests
    {
        [TearDown]
        public void TearDown() { TestDefinitions.DestroyCreatedObjects(); }

        [Test]
        public void HasteAndSlow_ChangeOnlyTheNextAttackCooldown()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState unit = simulation.Units[0];
            StatusDefinition haste = CreateStatus(StatusKind.Haste, StatusCategory.Beneficial, 0.25f);
            StatusDefinition slow = CreateStatus(StatusKind.Slow, StatusCategory.HarmfulCrowdControl, 0.5f);

            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, unit, new StatusApplicationRequest(haste, 1)));
            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, unit, new StatusApplicationRequest(slow, 2)));

            Assert.That(simulation.Tuning.GetAttackCooldown(unit.CombatSpec, unit), Is.EqualTo(1.25f).Within(0.0001f));
        }

        [Test]
        public void Untargetable_CannotBeSelectedAsANewTarget()
        {
            BattleSimulation simulation = CreateSimulation();
            UnitRuntimeState target = simulation.Units[1];
            StatusDefinition untargetable = CreateStatus(StatusKind.Untargetable, StatusCategory.Beneficial, 0f);
            Assert.AreEqual(StatusApplicationResult.Applied, StatusResolver.TryApply(simulation, target, new StatusApplicationRequest(untargetable, 2)));

            var workspace = new TargetSelector.Workspace(simulation.Board.Width * simulation.Board.Height);
            Assert.IsFalse(TargetSelector.TrySelectTarget(simulation, simulation.Units[0], workspace, out _));
        }

        private static StatusDefinition CreateStatus(StatusKind kind, StatusCategory category, float magnitude)
        {
            StatusDefinition definition = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            definition.Kind = kind;
            definition.Category = category;
            definition.StackingRule = StatusStackingRule.RefreshPerSource;
            definition.DefaultDuration = 5f;
            definition.DefaultMagnitude = magnitude;
            return definition;
        }

        private static BattleSimulation CreateSimulation()
        {
            UnitDefinition attacker = TestDefinitions.CreateUnit("attacker", 1);
            attacker.AttacksPerSecond = 1f;
            UnitDefinition target = TestDefinitions.CreateUnit("target", 1);
            target.AttacksPerSecond = 1f / 999f;
            return BattleSimulation.Create(new HexBoard(5, 6, 1f), new[]
            {
                new UnitSpawnData(1, attacker, BattleSide.Player, new HexCoord(1, 1)),
                new UnitSpawnData(2, target, BattleSide.Enemy, new HexCoord(2, 1))
            });
        }
    }
}
