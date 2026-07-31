using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class BattleRuntimeTuningTests
    {
        [Test]
        public void PursuitLimit_DefaultsToTwoAndClampsNegativeValues()
        {
            Assert.AreEqual(2, BattleRuntimeTuning.Default.MaxPursuitStepsAfterAttack);
            Assert.AreEqual(2, new BattleRuntimeTuning(1f, 0).MaxPursuitStepsAfterAttack);
            Assert.AreEqual(0, new BattleRuntimeTuning(1f, 0, 0.4f, -1).MaxPursuitStepsAfterAttack);
        }

        [Test]
        public void AttackRangeBonus_AllowsUnitToAttackFromTunedRange()
        {
            UnitDefinition player = CreateUnit("player", 5, 5, 1, 1f);
            UnitDefinition enemy = CreateUnit("enemy", 3, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 0))
                },
                new BattleRuntimeTuning(1f, 1));
            var loop = new BattleTickLoop(simulation, 1f);
            var events = new BattleEventQueue();

            BattleTickResult windup = loop.Tick(events);
            BattleTickResult result = loop.Tick(events);

            Assert.AreEqual(0, windup.Attacks);
            Assert.AreEqual(2, result.Attacks);
            Assert.AreEqual(0, result.Moves);
            Assert.IsTrue(simulation.Units[1].IsDefeated);
        }

        [Test]
        public void AttackCooldownMultiplier_AdjustsCycleScheduledAtWindupStart()
        {
            UnitDefinition player = CreateUnit("player", 10, 1, 3, 1f);
            UnitDefinition enemy = CreateUnit("enemy", 10, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 0))
                },
                new BattleRuntimeTuning(2f, 0));
            simulation.Units[0].SetTarget(simulation.Units[1]);
            simulation.Units[0].NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, 0.25f);

            loop.Tick(new BattleEventQueue());

            Assert.AreEqual(UnitAttackPhase.Windup, simulation.Units[0].AttackPhase);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(2.25d).Within(0.000001d));
        }

        [Test]
        public void Haste_AdjustsCycleScheduledAtWindupStart()
        {
            UnitDefinition player = CreateUnit("player", 10, 1, 3, 1f);
            UnitDefinition enemy = CreateUnit("enemy", 5, 1, 1, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(5, 6, 1f),
                new[]
                {
                    new UnitSpawnData(1, player, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, enemy, BattleSide.Enemy, new HexCoord(2, 0))
                });
            simulation.Units[0].SetTarget(simulation.Units[1]);
            StatusResolver.TryApply(
                simulation,
                simulation.Units[0],
                new StatusApplicationRequest(CreateHasteStatus(0.5f), simulation.Units[0].UnitId));
            simulation.Units[0].NextAttackTime = 0d;
            var loop = new BattleTickLoop(simulation, 0.25f);

            loop.Tick(new BattleEventQueue());

            Assert.AreEqual(UnitAttackPhase.Windup, simulation.Units[0].AttackPhase);
            Assert.That(simulation.Units[0].NextAttackTime, Is.EqualTo(0.75d).Within(0.000001d));
        }

        private static UnitDefinition CreateUnit(string unitId, int hp, int attack, int attackRange, float attackCooldown)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.MaxHp = hp;
            definition.Attack = attack;
            definition.AttackRange = attackRange;
            definition.AttackCooldown = attackCooldown;
            return definition;
        }

        private static StatusDefinition CreateHasteStatus(float magnitude)
        {
            StatusDefinition status = TestDefinitions.Track(UnityEngine.ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Haste;
            status.Category = StatusCategory.Beneficial;
            status.DefaultDuration = 10f;
            status.DefaultMagnitude = magnitude;
            return status;
        }
    }
}
