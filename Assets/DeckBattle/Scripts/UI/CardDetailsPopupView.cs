using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class CardDetailsPopupView : MonoBehaviour
    {
        [SerializeField] private float topMarginPixels = 168f;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image cardArtImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI apCostText;
        [SerializeField] private List<StatView> statViews = new List<StatView>();
        [SerializeField] private TextMeshProUGUI specialHeaderText;
        [SerializeField] private TextMeshProUGUI specialDescriptionText;
        [SerializeField] private TextMeshProUGUI onPlayHeaderText;
        [FormerlySerializedAs("onPlayEffectText")]
        [SerializeField] private TextMeshProUGUI onPlayDescriptionText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private TextMeshProUGUI rarityText;
        [SerializeField] private GameObject unitDetailsRoot;
        [SerializeField] private GameObject specialDetailsRoot;
        [SerializeField] private GameObject onPlayDetailsRoot;
        [SerializeField] private GameObject spellDetailsRoot;
        [SerializeField] private TextMeshProUGUI spellTargetText;
        [SerializeField] private TextMeshProUGUI spellEffectText;
        [SerializeField] private TextMeshProUGUI spellAmountText;
        [SerializeField] private TextMeshProUGUI spellDescriptionText;

        private CardRuntimeState shownCard;
        private UnitDefinition shownUnitDefinition;
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            Hide();
        }

        public void Show(CardRuntimeState card)
        {
            if (card == null || card.Definition == null)
            {
                Hide();
                return;
            }

            if (shownCard != card)
            {
                Apply(card);
            }

            ApplySafeArea();
            SetVisible(true);
        }

        public void Show(UnitDefinition unitDefinition)
        {
            if (unitDefinition == null)
            {
                Hide();
                return;
            }

            if (shownUnitDefinition != unitDefinition || shownCard != null)
            {
                Apply(unitDefinition);
            }

            ApplySafeArea();
            SetVisible(true);
        }

        public void Hide()
        {
            shownCard = null;
            shownUnitDefinition = null;
            SetVisible(false);
        }

        public bool IsShownFor(CardRuntimeState card)
        {
            return gameObject.activeSelf && shownCard == card;
        }

        public bool IsShowingCardDetails
        {
            get { return gameObject.activeSelf && shownCard != null; }
        }

        private void Apply(CardRuntimeState card)
        {
            shownCard = card;
            shownUnitDefinition = null;
            CardDefinition definition = card.Definition;
            UnitDefinition unitDefinition = card.UnitDefinition;
            SpellDefinition spellDefinition = card.SpellDefinition;

            SetText(nameText, definition.DisplayName);
            SetText(apCostText, definition.ApCost.ToString());
            SetText(typeText, definition.CardKind.ToString());
            SetText(rarityText, definition.Rarity.ToString());
            if (unitDefinition != null)
            {
                ApplyUnitDetails(unitDefinition);
            }
            else
            {
                ApplySpellDetails(spellDefinition);
            }

            if (cardArtImage != null)
            {
                cardArtImage.sprite = definition.CardArt;
                cardArtImage.enabled = definition.CardArt != null;
            }
        }

        private void Apply(UnitDefinition definition)
        {
            shownCard = null;
            shownUnitDefinition = definition;

            SetText(nameText, definition.DisplayName);
            SetText(apCostText, definition.ApCost.ToString());
            SetText(typeText, definition.CardKind.ToString());
            SetText(rarityText, definition.Rarity.ToString());
            ApplyUnitDetails(definition);

            if (cardArtImage != null)
            {
                cardArtImage.sprite = definition.CardArt;
                cardArtImage.enabled = definition.CardArt != null;
            }
        }

        private void ApplyUnitDetails(UnitDefinition definition)
        {
            SetUnitDetailsVisible(true);
            SetSpellDetailsVisible(false);
            ApplyStats(definition);
            ApplyAbilityDescriptions(definition);
        }

        private void ApplySpellDetails(SpellDefinition definition)
        {
            bool hasSpell = definition != null;
            SetUnitDetailsVisible(false);
            SetSpellDetailsVisible(hasSpell);
            ClearUnitDetails();
            SetText(spellTargetText, hasSpell ? "Target " + FormatTargetingKind(definition.TargetingKind) : string.Empty);
            SetText(spellEffectText, hasSpell ? "Effect " + FormatEffectKind(definition.EffectKind) : string.Empty);
            SetText(spellAmountText, hasSpell ? "Amount " + definition.Amount : string.Empty);
            SetText(spellDescriptionText, hasSpell ? FormatSpellDescription(definition) : string.Empty);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        private void ClearUnitDetails()
        {
            ClearStats();
            SetText(specialHeaderText, string.Empty);
            SetText(specialDescriptionText, string.Empty);
            SetText(onPlayHeaderText, string.Empty);
            SetText(onPlayDescriptionText, string.Empty);
        }

        private void SetUnitDetailsVisible(bool visible)
        {
            SetGameObjectActive(unitDetailsRoot, visible);
            SetStatsVisible(visible);
            SetAbilityDetailsVisible(specialDetailsRoot, specialHeaderText, specialDescriptionText, visible);
            SetAbilityDetailsVisible(onPlayDetailsRoot, onPlayHeaderText, onPlayDescriptionText, visible);
        }

        private void ApplyStats(UnitDefinition definition)
        {
            for (int i = 0; i < statViews.Count; i++)
            {
                StatView statView = statViews[i];
                if (statView != null)
                {
                    statView.Apply(definition);
                }
            }
        }

        private void ClearStats()
        {
            for (int i = 0; i < statViews.Count; i++)
            {
                StatView statView = statViews[i];
                if (statView != null)
                {
                    statView.Clear();
                }
            }
        }

        private void SetStatsVisible(bool visible)
        {
            for (int i = 0; i < statViews.Count; i++)
            {
                StatView statView = statViews[i];
                if (statView != null)
                {
                    statView.SetVisible(visible);
                }
            }
        }

        private void SetSpellDetailsVisible(bool visible)
        {
            SetGameObjectActive(spellDetailsRoot, visible);
            SetTextActive(spellTargetText, visible);
            SetTextActive(spellEffectText, visible);
            SetTextActive(spellAmountText, visible);
            SetTextActive(spellDescriptionText, visible);
        }

        private static void SetGameObjectActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private static void SetTextActive(TextMeshProUGUI text, bool active)
        {
            if (text != null && text.gameObject.activeSelf != active)
            {
                text.gameObject.SetActive(active);
            }
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static string FormatTargetingKind(SpellTargetingKind targetingKind)
        {
            if (targetingKind == SpellTargetingKind.None)
            {
                return "Bez celu";
            }

            if (targetingKind == SpellTargetingKind.FriendlyUnit)
            {
                return "Wlasna jednostka";
            }

            return targetingKind.ToString();
        }

        private static string FormatEffectKind(SpellEffectKind effectKind)
        {
            if (effectKind == SpellEffectKind.BuffAttackNextCombat)
            {
                return "Buff attack next combat";
            }

            if (effectKind == SpellEffectKind.None)
            {
                return "Brak efektu";
            }

            return effectKind.ToString();
        }

        private static string FormatSpellDescription(SpellDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            if (definition.EffectKind == SpellEffectKind.BuffAttackNextCombat)
            {
                return "+" + definition.Amount + " attack in next combat.";
            }

            if (definition.EffectKind == SpellEffectKind.None)
            {
                return "No target effect.";
            }

            return string.Empty;
        }

        private void ApplyAbilityDescriptions(UnitDefinition definition)
        {
            string specialDescription = CardDescriptionTemplateFormatter.FormatSpecial(definition);
            bool hasSpecialDescription = !string.IsNullOrWhiteSpace(specialDescription);
            SetText(specialHeaderText, hasSpecialDescription ? "SPECIAL" : string.Empty);
            SetText(specialDescriptionText, specialDescription);
            SetAbilityDetailsVisible(specialDetailsRoot, specialHeaderText, specialDescriptionText, hasSpecialDescription);

            string onPlayDescription = CardDescriptionTemplateFormatter.FormatOnPlay(definition);
            bool hasOnPlayDescription = !string.IsNullOrWhiteSpace(onPlayDescription);
            SetText(onPlayHeaderText, hasOnPlayDescription ? "ON PLAY" : string.Empty);
            SetText(onPlayDescriptionText, onPlayDescription);
            SetAbilityDetailsVisible(onPlayDetailsRoot, onPlayHeaderText, onPlayDescriptionText, hasOnPlayDescription);
        }

        private static void SetAbilityDetailsVisible(
            GameObject root,
            TextMeshProUGUI headerText,
            TextMeshProUGUI descriptionText,
            bool visible)
        {
            SetGameObjectActive(root, visible);
            SetTextActive(headerText, visible);
            SetTextActive(descriptionText, visible);
        }

        private void ApplySafeArea()
        {
            if (rectTransform == null)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            float topInset = Mathf.Max(0f, Screen.height - safeArea.yMax);
            float scale = 1f;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.scaleFactor > 0f)
            {
                scale = canvas.scaleFactor;
            }

            Vector2 position = rectTransform.anchoredPosition;
            position.y = -(topMarginPixels + topInset / scale);
            rectTransform.anchoredPosition = position;
        }
    }
}
