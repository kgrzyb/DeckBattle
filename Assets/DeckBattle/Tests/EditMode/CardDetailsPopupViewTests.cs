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
            GameObject root = new GameObject("CardDetailsPopup", typeof(RectTransform), typeof(CardDetailsPopupView));

            try
            {
                CardDetailsPopupView view = root.GetComponent<CardDetailsPopupView>();
                typeof(CardDetailsPopupView)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);

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
    }
}
