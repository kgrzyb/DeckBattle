using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class DeckBuilderCardItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text typeText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private Color availableColor = new Color(0.18f, 0.23f, 0.25f, 0.96f);
        [SerializeField] private Color inDeckColor = new Color(0.40f, 0.32f, 0.16f, 0.96f);
        [SerializeField] private Color lockedColor = new Color(0.13f, 0.15f, 0.16f, 0.88f);

        private string cardId;
        private Action<string> clicked;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        public void Bind(CardDefinition card, bool isInDeck, bool canAdd, Action<string> onClicked)
        {
            cardId = card != null ? card.CardId : string.Empty;
            clicked = onClicked;

            if (nameText != null)
            {
                nameText.text = card != null ? card.DisplayName : string.Empty;
            }

            if (costText != null)
            {
                costText.text = card != null ? card.ApCost.ToString() : string.Empty;
            }

            if (typeText != null)
            {
                typeText.text = card != null ? FormatType(card) : string.Empty;
            }

            if (statsText != null)
            {
                statsText.text = card != null ? FormatStats(card) : string.Empty;
            }

            if (button != null)
            {
                button.interactable = canAdd;
            }

            if (background != null)
            {
                background.color = isInDeck ? inDeckColor : canAdd ? availableColor : lockedColor;
            }
        }

        private static string FormatType(CardDefinition card)
        {
            UnitDefinition unit = card as UnitDefinition;
            if (unit != null)
            {
                return unit.UnitType.ToString();
            }

            return card.CardKind.ToString();
        }

        private static string FormatStats(CardDefinition card)
        {
            UnitDefinition unit = card as UnitDefinition;
            if (unit != null)
            {
                return "HP " + unit.MaxHp + "  ATK " + unit.Attack + "  RNG " + unit.AttackRange;
            }

            SpellDefinition spell = card as SpellDefinition;
            if (spell != null)
            {
                return spell.EffectKind + " " + spell.Amount;
            }

            return card.Rarity.ToString();
        }

        private void HandleClick()
        {
            if (!string.IsNullOrEmpty(cardId) && clicked != null)
            {
                clicked(cardId);
            }
        }
    }
}
