using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class CombatSpecBoundaryTests
    {
        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void Simulation_CopiesCombatValuesFromDefinitionAtCreation()
        {
            UnitDefinition definition = TestDefinitions.CreateUnit("combat-spec", 1);
            definition.MaxHp = 11;
            definition.Attack = 4;
            definition.ManaThreshold = 30;
            definition.AttacksPerSecond = 2.5f;

            BattleSimulation simulation = BattleSimulation.Create(
                new HexBoard(3, 3, 1f),
                new[] { new UnitSpawnData(1, definition, BattleSide.Player, new HexCoord(0, 0)) });

            definition.MaxHp = 99;
            definition.Attack = 99;
            definition.ManaThreshold = 99;

            UnitCombatSpec combatSpec = simulation.Units[0].CombatSpec;
            Assert.AreEqual(11, combatSpec.MaxHp);
            Assert.AreEqual(4, combatSpec.Attack);
            Assert.AreEqual(30, combatSpec.ManaThreshold);
            Assert.That(combatSpec.AttackCooldown, Is.EqualTo(0.4f).Within(0.000001f));
        }

        [Test]
        public void SimulationRuntimeState_DoesNotRetainUnityPresentationReferences()
        {
            Assert.IsNull(typeof(UnitRuntimeState).GetField("Definition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(ProjectileRuntimeState).GetField("ProjectileDefinition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(ProjectileRuntimeState).GetField("AttackerDefinition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(BattleEvent).GetField("ProjectilePrefab", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(BattleEvent).GetField("SpawnHeight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            Assert.IsNull(typeof(BattleEvent).GetField("HitHeight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        [Test]
        public void PresentationCatalog_MapsEveryConfiguredUnitAndProjectile()
        {
            BattlePresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<BattlePresentationCatalog>(
                "Assets/DeckBattle/Data/Presentation/BattlePresentationCatalog.asset");
            Assert.IsNotNull(catalog);

            string[] unitGuids = AssetDatabase.FindAssets("t:UnitDefinition", new[] { "Assets/DeckBattle/Data/Units" });
            Assert.Greater(unitGuids.Length, 0);
            for (int i = 0; i < unitGuids.Length; i++)
            {
                UnitDefinition definition = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(unitGuids[i]));
                Assert.IsTrue(catalog.TryGetUnitPrefab(BattlePresentationId.ForUnit(definition), out UnitView prefab), definition.name);
                Assert.IsNotNull(prefab, definition.name);
            }

            string[] projectileGuids = AssetDatabase.FindAssets("t:ProjectileDefinition", new[] { "Assets/DeckBattle/Data/Projectiles" });
            Assert.Greater(projectileGuids.Length, 0);
            for (int i = 0; i < projectileGuids.Length; i++)
            {
                ProjectileDefinition definition = AssetDatabase.LoadAssetAtPath<ProjectileDefinition>(AssetDatabase.GUIDToAssetPath(projectileGuids[i]));
                Assert.IsTrue(
                    catalog.TryGetProjectile(BattlePresentationId.ForProjectile(definition), out ProjectileView prefab, out float spawnHeight, out float hitHeight),
                    definition.name);
                Assert.IsNotNull(prefab, definition.name);
                Assert.AreEqual(definition.SpawnHeight, spawnHeight, definition.name);
                Assert.AreEqual(definition.HitHeight, hitHeight, definition.name);
            }
        }
    }
}
