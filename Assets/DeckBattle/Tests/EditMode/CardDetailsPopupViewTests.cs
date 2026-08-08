using NUnit.Framework;
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

                Assert.IsFalse(root.transform.Find("UnitDetails/Hp").gameObject.activeSelf);
                Assert.IsFalse(root.transform.Find("UnitDetails/Attack").gameObject.activeSelf);
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
                Assert.IsTrue(root.transform.Find("UnitDetails/Hp").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("UnitDetails/Attack").gameObject.activeSelf);
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
                unit.CritChance = 15f;
                unit.CritMultiplier = 2.5f;
                unit.ManaThreshold = 100;
                unit.ManaPerAttack = 25;
                unit.ManaPerDamageTaken = 10;
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
                Assert.AreEqual("30", root.transform.Find("UnitDetails/Attack").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("15%", root.transform.Find("UnitDetails/CritChance").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("2.5×", root.transform.Find("UnitDetails/CritMultiplier").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("100", root.transform.Find("UnitDetails/ManaThreshold").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("+25", root.transform.Find("UnitDetails/ManaPerAttack").GetComponent<TMPro.TextMeshProUGUI>().text);
                Assert.AreEqual("+10", root.transform.Find("UnitDetails/ManaPerDamageTaken").GetComponent<TMPro.TextMeshProUGUI>().text);
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

            SetField(view, "canvasGroup", root.GetComponent<CanvasGroup>());
            SetField(view, "backgroundImage", root.GetComponent<UnityEngine.UI.Image>());
            SetField(view, "apCostText", CreateText("ApCost", root.transform));
            SetField(view, "hpText", CreateText("Hp", unitDetails.transform));
            SetField(view, "attackText", CreateText("Attack", unitDetails.transform));
            SetField(view, "critChanceText", CreateText("CritChance", unitDetails.transform));
            SetField(view, "critMultiplierText", CreateText("CritMultiplier", unitDetails.transform));
            SetField(view, "manaThresholdText", CreateText("ManaThreshold", unitDetails.transform));
            SetField(view, "manaPerAttackText", CreateText("ManaPerAttack", unitDetails.transform));
            SetField(view, "manaPerDamageTakenText", CreateText("ManaPerDamageTaken", unitDetails.transform));
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

        private static void SetField(CardDetailsPopupView view, string fieldName, object value)
        {
            typeof(CardDetailsPopupView)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, value);
        }

        private static void InvokeAwake(CardDetailsPopupView view)
        {
            typeof(CardDetailsPopupView)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(view, null);
        }
    }
}
