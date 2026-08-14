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
        public void FormatSpecial_UsesOverriddenStatusDuration()
        {
            UnitDefinition unit = TestDefinitions.CreateUnit("mega-arrow", 1);
            StatusDefinition status = TestDefinitions.Track(ScriptableObject.CreateInstance<StatusDefinition>());
            status.Kind = StatusKind.Stun;
            status.DisplayName = "Stun";
            status.DefaultDuration = 2f;
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.AppliedStatus = status;
            special.AppliedStatusLifetimeMode = StatusLifetimeMode.OverrideSeconds;
            special.AppliedStatusDurationOverride = 1f;
            special.DescriptionTemplate = "{status} for {statusDuration}.";
            unit.Special = special;

            Assert.AreEqual("Stun for 1 s.", CardDescriptionTemplateFormatter.FormatSpecial(unit));
        }

        [Test]
        public void FormatSpecial_UsesAttackDamagePercentAndEffectRadius()
        {
            UnitDefinition unit = TestDefinitions.CreateUnit("slam-unit", 1);
            UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
            special.AttackDamageMultiplier = 1f;
            special.EffectRadius = 1;
            special.DescriptionTemplate = "Deals {attackDamagePercent} within {effectRadius}.";
            unit.Special = special;

            string description = CardDescriptionTemplateFormatter.FormatSpecial(unit);

            Assert.AreEqual("Deals 100% within 1.", description);
            Assert.IsTrue(CardDescriptionTemplateFormatter.IsSpecialTemplateValid(special));
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

        [Test]
        public void Content_AJ4XUsesConfiguredSlam()
        {
            UnitDefinition aj4X = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/DeckBattle/Data/Units/AJ-4X.asset");

            Assert.IsNotNull(aj4X);
            Assert.IsNotNull(aj4X.Special);
            Assert.AreEqual(UnitSpecialKind.Slam, aj4X.Special.Kind);
            Assert.AreEqual(1f, aj4X.Special.AttackDamageMultiplier);
            Assert.AreEqual(1, aj4X.Special.EffectRadius);
            Assert.IsTrue(CardDescriptionTemplateFormatter.IsSpecialTemplateValid(aj4X.Special));
        }

        [Test]
        public void Content_JuniUsesConfiguredLongshot()
        {
            UnitDefinition juni = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/DeckBattle/Data/Units/Juni.asset");

            Assert.IsNotNull(juni);
            Assert.IsNotNull(juni.Special);
            Assert.AreEqual(UnitSpecialKind.Longshot, juni.Special.Kind);
            Assert.AreEqual(1.5f, juni.Special.AttackDamageMultiplier);
            Assert.AreEqual(20, juni.Special.ExecuteHpThresholdPercent);
            Assert.IsNotNull(juni.Special.Projectile);
            Assert.IsTrue(CardDescriptionTemplateFormatter.IsSpecialTemplateValid(juni.Special));
            StringAssert.Contains("150%", CardDescriptionTemplateFormatter.FormatSpecial(juni));
            StringAssert.Contains("20%", CardDescriptionTemplateFormatter.FormatSpecial(juni));
        }

        [Test]
        public void Content_CptSabatiniUsesConfiguredArrgh()
        {
            UnitDefinition sabatini = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/DeckBattle/Data/Units/Cpt.Sabatini.asset");
            StatusPresentationCatalog statusCatalog = AssetDatabase.LoadAssetAtPath<StatusPresentationCatalog>(
                "Assets/DeckBattle/Data/Statuses/_StatusPresentationCatalog.asset");

            Assert.IsNotNull(sabatini);
            Assert.IsNotNull(sabatini.Special);
            Assert.AreEqual(UnitSpecialKind.Arrgh, sabatini.Special.Kind);
            Assert.AreEqual(0.3f, sabatini.Special.EffectDelay);
            Assert.AreEqual(StatusLifetimeMode.OverrideSeconds, sabatini.Special.AppliedStatusLifetimeMode);
            Assert.AreEqual(5f, sabatini.Special.AppliedStatusDurationOverride);
            Assert.IsNotNull(sabatini.Special.AppliedStatus);
            Assert.AreEqual(StatusKind.Empower, sabatini.Special.AppliedStatus.Kind);
            Assert.IsTrue(CardDescriptionTemplateFormatter.IsSpecialTemplateValid(sabatini.Special));
            Assert.AreEqual(
                "Grant all allied units Empower for 5 s, increasing damage dealt by 50%.",
                CardDescriptionTemplateFormatter.FormatSpecial(sabatini));

            Assert.IsNotNull(statusCatalog);
            Assert.IsTrue(statusCatalog.TryGet(StatusKind.Empower, out StatusPresentationEntry empowerPresentation));
            Assert.IsTrue(empowerPresentation.ShowsIcon);
            Assert.IsTrue(empowerPresentation.UsesVfx);
            Assert.IsNotNull(empowerPresentation.Icon);
        }
    }
}
