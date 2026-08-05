using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BattlePresentationContractTests
    {
        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void Snapshot_UsesStablePresentationIdForUnitsWithTheSameDefinition()
        {
            UnitDefinition definition = TestDefinitions.CreateUnit("presentation-unit", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(3, 3, 1f),
                new[]
                {
                    new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(0, 0)),
                    new UnitSpawnData(2, definition, BattleSide.Player, new HexCoord(1, 0))
                });
            var snapshot = new BattlePresentationSnapshot(2);

            snapshot.Capture(simulation);

            Assert.AreEqual(2, snapshot.Units.Count);
            Assert.AreNotEqual(snapshot.Units[0].UnitId, snapshot.Units[1].UnitId);
            Assert.AreEqual(snapshot.Units[0].PresentationId, snapshot.Units[1].PresentationId);
            Assert.AreNotEqual(0, snapshot.Units[0].PresentationId);
        }

        [Test]
        public void Snapshot_PreservesUnitDisplayNameForCombatPresentation()
        {
            UnitDefinition definition = TestDefinitions.CreateUnit("Shield Bearer", 1);
            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(3, 3, 1f),
                new[] { new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(0, 0)) });
            var snapshot = new BattlePresentationSnapshot(1);

            snapshot.Capture(simulation);

            Assert.AreEqual(definition.DisplayName, snapshot.Units[0].DisplayName);
        }

        [Test]
        public void ProjectileEvent_CarriesStablePresentationId()
        {
            ProjectileDefinition definition = TestDefinitions.Track(ScriptableObject.CreateInstance<ProjectileDefinition>());
            definition.ProjectileId = "presentation-arrow";

            BattleEvent battleEvent = BattleEvent.ProjectileLaunched(
                10,
                1,
                2,
                new HexCoord(0, 0),
                new HexCoord(1, 0),
                0.5f,
                BattlePresentationId.ForProjectile(definition));

            Assert.AreEqual(BattlePresentationId.ForProjectile(definition), battleEvent.PresentationId);
            Assert.AreNotEqual(battleEvent.ProjectileId, battleEvent.PresentationId);
        }

        [Test]
        public void StatusEvents_UpdatePresentationShadowAndShield()
        {
            GameObject battleViewObject = new GameObject("BattleView", typeof(BattleView));
            BattleView battleView = battleViewObject.GetComponent<BattleView>();
            try
            {
                battleView.ProcessCombatTick(
                    default,
                    new[]
                    {
                        BattleEvent.StatusApplied(7, 3, StatusKind.Haste, 2, 4f),
                        BattleEvent.ShieldChanged(7, 5)
                    });

                Dictionary<int, List<StatusPresentationState>> statuses = GetPrivateField<Dictionary<int, List<StatusPresentationState>>>(battleView, "statusStatesByUnitId");
                Dictionary<int, int> shields = GetPrivateField<Dictionary<int, int>>(battleView, "shieldByUnitId");
                Assert.AreEqual(1, statuses[7].Count);
                Assert.AreEqual(StatusKind.Haste, statuses[7][0].Kind);
                Assert.AreEqual(2, statuses[7][0].Stacks);
                Assert.AreEqual(5, shields[7]);

                battleView.ProcessCombatTick(default, new[] { BattleEvent.StatusRemoved(7, 3, StatusKind.Haste, 2) });
                Assert.IsFalse(statuses.ContainsKey(7));
            }
            finally
            {
                Object.DestroyImmediate(battleViewObject);
            }
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }
    }
}
