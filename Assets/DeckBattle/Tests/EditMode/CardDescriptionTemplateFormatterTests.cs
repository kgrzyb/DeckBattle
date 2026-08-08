using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class CardDescriptionTemplateFormatterTests
    {
        [TearDown]
        public void TearDown()
        {
            TestDefinitions.DestroyCreatedObjects();
        }

        [Test]
        public void FormatSpecial_UsesCalculatedDamagePerHitAndTotal()
        {
            UnitDefinition unit = TestDefinitions.CreateUnit("scout", 1);
            unit.Attack = 30;
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.StrikeCount = 10;
            special.AttackDamageMultiplier = 2f;
            special.DescriptionTemplate = "{strikeCount} hits for {damagePerHit}, {totalDamage} total.";
            unit.Special = special;

            string description = CardDescriptionTemplateFormatter.FormatSpecial(unit);

            Assert.AreEqual("10 hits for 60, 600 total.", description);
        }

        [Test]
        public void FormatSpecial_UsesStatusDisplayNameAndConfiguredValues()
        {
            UnitDefinition unit = TestDefinitions.CreateUnit("haste-unit", 1);
            StatusDefinition status = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Haste;
            status.DisplayName = "Haste";
            status.DefaultDuration = 5f;
            status.DefaultMagnitude = 0.2f;
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.AppliedStatus = status;
            special.DescriptionTemplate = "{status} for {statusDuration}: {statusMagnitudePercent}.";
            unit.Special = special;

            string description = CardDescriptionTemplateFormatter.FormatSpecial(unit);

            Assert.AreEqual("Haste for 5 s: 20%.", description);
        }

        [Test]
        public void FormatOnPlay_UsesCalculatedBaseAttackValues()
        {
            UnitDefinition unit = TestDefinitions.CreateUnit("focused", 1);
            unit.Attack = 30;
            UnitOnPlayEffectDefinition effect = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitOnPlayEffectDefinition>());
            effect.DescriptionTemplate = "{step1.attackBonus}; {step1.attackAfterEffect}; {step1.percent}.";
            effect.Steps = new[]
            {
                new UnitEffectStepDefinition
                {
                    Target = new EffectTargetDefinition { Kind = EffectTargetKind.Self },
                    Effect = new CombatEffectDefinition
                    {
                        Kind = CombatEffectKind.ModifyBaseAttackPercent,
                        Percent = 0.25f
                    }
                }
            };
            unit.OnPlayEffect = effect;

            string description = CardDescriptionTemplateFormatter.FormatOnPlay(unit);

            Assert.AreEqual("+8; 38; 25%.", description);
        }

        [Test]
        public void TemplateValidation_RejectsUnknownAndUnclosedTokens()
        {
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.DescriptionTemplate = "Damage: {unknown}.";
            Assert.IsFalse(CardDescriptionTemplateFormatter.IsSpecialTemplateValid(special));

            special.DescriptionTemplate = "Damage: {damagePerHit";
            Assert.IsFalse(CardDescriptionTemplateFormatter.IsSpecialTemplateValid(special));
        }

        [Test]
        public void StatusReferenceFormatter_UsesKindWhenDisplayNameIsEmpty()
        {
            StatusDefinition status = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Haste;

            Assert.AreEqual("Haste", StatusReferenceFormatter.Format(status));
        }

        [Test]
        public void Content_ProductionTemplatesUseSupportedTokens()
        {
            string[] specialGuids = AssetDatabase.FindAssets("t:UnitSpecialDefinition", new[] { "Assets/DeckBattle/Data/Specials" });
            for (int i = 0; i < specialGuids.Length; i++)
            {
                UnitSpecialDefinition special = AssetDatabase.LoadAssetAtPath<UnitSpecialDefinition>(AssetDatabase.GUIDToAssetPath(specialGuids[i]));
                Assert.IsTrue(CardDescriptionTemplateFormatter.IsSpecialTemplateValid(special), special.name);
            }

            string[] onPlayGuids = AssetDatabase.FindAssets("t:UnitOnPlayEffectDefinition", new[] { "Assets/DeckBattle/Data/UnitEffects" });
            for (int i = 0; i < onPlayGuids.Length; i++)
            {
                UnitOnPlayEffectDefinition onPlay = AssetDatabase.LoadAssetAtPath<UnitOnPlayEffectDefinition>(AssetDatabase.GUIDToAssetPath(onPlayGuids[i]));
                Assert.IsTrue(CardDescriptionTemplateFormatter.IsOnPlayTemplateValid(onPlay), onPlay.name);
            }
        }
    }
}
