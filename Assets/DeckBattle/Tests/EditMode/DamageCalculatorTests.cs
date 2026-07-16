using NUnit.Framework;

namespace DeckBattle.Tests
{
    public sealed class DamageCalculatorTests
    {
        [Test]
        public void CalculateDamage_WithoutArmorMatchesAttack()
        {
            UnitDefinition attacker = CreateUnit("attacker", 6);
            UnitDefinition target = CreateUnit("target", 1);

            int damage = DamageCalculator.CalculateDamage(attacker, target, new DeterministicRandom(1), out bool isCritical);

            Assert.AreEqual(6, damage);
            Assert.IsFalse(isCritical);
        }

        [Test]
        public void CalculateDamage_ArmorReducesDamage()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10);
            UnitDefinition target = CreateUnit("target", 1);
            target.Armor = 50f;

            int damage = DamageCalculator.CalculateDamage(attacker, target, new DeterministicRandom(1), out bool _);

            Assert.AreEqual(5, damage);
        }

        [Test]
        public void CalculateDamage_ArmorPenetrationReducesEffectiveArmor()
        {
            UnitDefinition attacker = CreateUnit("attacker", 10);
            UnitDefinition target = CreateUnit("target", 1);
            attacker.ArmorPenetration = 50f;
            target.Armor = 50f;

            int damage = DamageCalculator.CalculateDamage(attacker, target, new DeterministicRandom(1), out bool _);

            Assert.AreEqual(8, damage);
        }

        [Test]
        public void CalculateDamage_CriticalHitAppliesMultiplier()
        {
            UnitDefinition attacker = CreateUnit("attacker", 4);
            UnitDefinition target = CreateUnit("target", 1);
            attacker.CritChance = 100f;
            attacker.CritMultiplier = 2.5f;

            int damage = DamageCalculator.CalculateDamage(attacker, target, new DeterministicRandom(1), out bool isCritical);

            Assert.AreEqual(10, damage);
            Assert.IsTrue(isCritical);
        }

        [Test]
        public void CalculateDamage_CriticalHitUsesDeterministicRandom()
        {
            UnitDefinition attacker = CreateUnit("attacker", 4);
            UnitDefinition target = CreateUnit("target", 1);
            attacker.CritChance = 50f;

            int first = DamageCalculator.CalculateDamage(attacker, target, new DeterministicRandom(123), out bool firstCrit);
            int second = DamageCalculator.CalculateDamage(attacker, target, new DeterministicRandom(123), out bool secondCrit);

            Assert.AreEqual(first, second);
            Assert.AreEqual(firstCrit, secondCrit);
        }

        private static UnitDefinition CreateUnit(string unitId, int attack)
        {
            UnitDefinition definition = TestDefinitions.CreateUnit(unitId, 1);
            definition.Attack = attack;
            return definition;
        }
    }
}
