using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class CardDetailsPopupViewTests
    {
        [Test]
        public void ShowSpell_HidesUnitStatFieldsAndShowsSpellFields()
        {
            CardDetailsPopupView view = CreateView(out GameObject root);

            try
            {
                SpellDefinition spell = TestDefinitions.CreateSpell(
                    "focus",
                    1,
                    SpellEffectKind.None,
                    SpellTargetingKind.None);
                var card = new CardRuntimeState(1, spell);

                view.Show(card);

                Assert.IsFalse(root.transform.Find("UnitDetails/Stat_HP").gameObject.activeSelf);
                Assert.IsFalse(root.transform.Find("UnitDetails/Stat_Attack").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("SpellDetails/SpellTarget").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("SpellDetails/SpellEffect").gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShowUnitDefinition_ShowsUnitStatFieldsAndHidesSpellFields()
        {
            CardDetailsPopupView view = CreateView(out GameObject root);

            try
            {
                UnitDefinition unit = TestDefinitions.CreateUnit("swordsman", 2);

                view.Show(unit);

                Assert.IsFalse(view.IsShowingCardDetails);
                Assert.IsTrue(root.transform.Find("UnitDetails/Stat_HP").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("UnitDetails/Stat_Attack").gameObject.activeSelf);
                Assert.IsFalse(root.transform.Find("SpellDetails/SpellTarget").gameObject.activeSelf);
                Assert.IsFalse(root.transform.Find("SpellDetails/SpellEffect").gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ShowUnitDefinition_ShowsValuesWithoutStatNamesAndComputedAbilityDescriptions()
        {
            CardDetailsPopupView view = CreateView(out GameObject root);

            try
            {
                UnitDefinition unit = TestDefinitions.CreateUnit("scout", 3);
                unit.Attack = 30;
                unit.AttackRange = 3;
                unit.CritChance = 0.15f;
                unit.CritMultiplier = 2.5f;
                unit.ManaThreshold = 100;
                unit.ManaPerTick = 3;
                UnitSpecialDefinition special = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitSpecialDefinition>());
                special.StrikeCount = 10;
                special.AttackDamageMultiplier = 2f;
                special.DescriptionTemplate = "{damagePerHit} damage.";
                unit.Special = special;
                UnitOnPlayEffectDefinition onPlay = TestDefinitions.Track(ScriptableObject.CreateInstance<UnitOnPlayEffectDefinition>());
                onPlay.DescriptionTemplate = "On Play: {step1.attackBonus} Attack.";
                onPlay.Steps = new[]
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
                unit.OnPlayEffect = onPlay;

                view.Show(unit);

                Assert.AreEqual("3", root.transform.Find("ApCost").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("30", FindStatText(root, "Stat_Attack").text);
                Assert.AreEqual("3 hex", FindStatText(root, "Stat_AttackRange").text);
                Assert.AreEqual("15%", FindStatText(root, "Stat_CritChance").text);
                Assert.AreEqual("2.5×", FindStatText(root, "Stat_CritMultiplier").text);
                Assert.AreEqual("100", FindStatText(root, "Stat_ManaThreshold").text);
                Assert.AreEqual("+ 3", FindStatText(root, "Stat_ManaPerTick").text);
                Assert.AreEqual("SPECIAL", root.transform.Find("UnitDetails/SpecialDetails/SpecialHeader").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("60 damage.", root.transform.Find("UnitDetails/SpecialDetails/SpecialDescription").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("ON PLAY", root.transform.Find("UnitDetails/OnPlayDetails/OnPlayHeader").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("On Play: +8 Attack.", root.transform.Find("UnitDetails/OnPlayDetails/OnPlayDescription").GetComponent<TMPro.TextMeshProUGUI>().text);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Awake_DoesNotCreateObjectsOrComponents()
        {
            GameObject root = new GameObject("CardDetailsPopup", typeof(RectTransform), typeof(CardDetailsPopupView));

            try
            {
                int componentCountBefore = root.GetComponents<Component>().Length;
                int childCountBefore = root.transform.childCount;

                typeof(CardDetailsPopupView)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(root.GetComponent<CardDetailsPopupView>(), null);

                Assert.AreEqual(componentCountBefore, root.GetComponents<Component>().Length);
                Assert.AreEqual(childCountBefore, root.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CardDetailsPopupView CreateView(out GameObject root)
        {
            root = new GameObject("CardDetailsPopup", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image));
            root.SetActive(false);
            CardDetailsPopupView view = root.AddComponent<CardDetailsPopupView>();

            GameObject unitDetails = CreateRoot("UnitDetails", root.transform);
            GameObject spellDetails = CreateRoot("SpellDetails", root.transform);
            GameObject specialDetails = CreateRoot("SpecialDetails", unitDetails.transform);
            GameObject onPlayDetails = CreateRoot("OnPlayDetails", unitDetails.transform);
            var statViews = new List<StatView>
            {
                CreateStat("Stat_HP", unitDetails.transform, UnitStatType.Hp),
                CreateStat("Stat_Attack", unitDetails.transform, UnitStatType.Attack),
                CreateStat("Stat_AttackRange", unitDetails.transform, UnitStatType.AttackRange),
                CreateStat("Stat_CritChance", unitDetails.transform, UnitStatType.CritChance),
                CreateStat("Stat_CritMultiplier", unitDetails.transform, UnitStatType.CritMultiplier),
                CreateStat("Stat_ManaThreshold", unitDetails.transform, UnitStatType.ManaThreshold),
                CreateStat("Stat_ManaPerTick", unitDetails.transform, UnitStatType.ManaPerTick)
            };

            SetField(view, "canvasGroup", root.GetComponent<CanvasGroup>());
            SetField(view, "backgroundImage", root.GetComponent<UnityEngine.UI.Image>());
            SetField(view, "apCostText", CreateText("ApCost", root.transform));
            SetField(view, "statViews", statViews);
            SetField(view, "specialHeaderText", CreateText("SpecialHeader", specialDetails.transform));
            SetField(view, "specialDescriptionText", CreateText("SpecialDescription", specialDetails.transform));
            SetField(view, "onPlayHeaderText", CreateText("OnPlayHeader", onPlayDetails.transform));
            SetField(view, "onPlayDescriptionText", CreateText("OnPlayDescription", onPlayDetails.transform));
            SetField(view, "spellTargetText", CreateText("SpellTarget", spellDetails.transform));
            SetField(view, "spellEffectText", CreateText("SpellEffect", spellDetails.transform));
            SetField(view, "unitDetailsRoot", unitDetails);
            SetField(view, "specialDetailsRoot", specialDetails);
            SetField(view, "onPlayDetailsRoot", onPlayDetails);
            SetField(view, "spellDetailsRoot", spellDetails);
            InvokeAwake(view);
            return view;
        }

        private static GameObject CreateRoot(string name, Transform parent)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return root;
        }

        private static TMPro.TextMeshProUGUI CreateText(string name, Transform parent)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            return textObject.GetComponent<TMPro.TextMeshProUGUI>();
        }

        private static StatView CreateStat(string name, Transform parent, UnitStatType statType)
        {
            GameObject statObject = new GameObject(name, typeof(RectTransform), typeof(StatView));
            statObject.transform.SetParent(parent, false);
            StatView statView = statObject.GetComponent<StatView>();

            GameObject iconObject = new GameObject("icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            iconObject.transform.SetParent(statObject.transform, false);
            TMPro.TextMeshProUGUI valueText = CreateText("text", statObject.transform);

            SetField(statView, "statType", statType);
            SetField(statView, "icon", iconObject.GetComponent<UnityEngine.UI.Image>());
            SetField(statView, "valueText", valueText);
            return statView;
        }

        private static TMPro.TextMeshProUGUI FindStatText(GameObject root, string statName)
        {
            return root.transform.Find("UnitDetails/" + statName + "/text").GetComponent<TMPro.TextMeshProUGUI>();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void InvokeAwake(CardDetailsPopupView view)
        {
            typeof(CardDetailsPopupView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
        }
    }
}
