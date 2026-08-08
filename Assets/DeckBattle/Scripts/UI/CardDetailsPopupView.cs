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
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI powerText;
        [SerializeField] private TextMeshProUGUI attackRangeText;
        [FormerlySerializedAs("critText")]
        [SerializeField] private TextMeshProUGUI critChanceText;
        [SerializeField] private TextMeshProUGUI critMultiplierText;
        [FormerlySerializedAs("cooldownText")]
        [SerializeField] private TextMeshProUGUI attackSpeedText;
        [FormerlySerializedAs("manaText")]
        [SerializeField] private TextMeshProUGUI manaThresholdText;
        [SerializeField] private TextMeshProUGUI manaPerAttackText;
        [SerializeField] private TextMeshProUGUI manaPerDamageTakenText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private TextMeshProUGUI armorPenetrationText;
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
        [SerializeField] private Image hpIcon;
        [SerializeField] private Image attackIcon;
        [SerializeField] private Image powerIcon;
        [SerializeField] private Image attackRangeIcon;
        [SerializeField] private Image critChanceIcon;
        [SerializeField] private Image critMultiplierIcon;
        [SerializeField] private Image attackSpeedIcon;
        [SerializeField] private Image manaThresholdIcon;
        [SerializeField] private Image manaPerAttackIcon;
        [SerializeField] private Image manaPerDamageTakenIcon;
        [SerializeField] private Image armorIcon;
        [SerializeField] private Image armorPenetrationIcon;
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
            SetText(hpText, definition.MaxHp.ToString());
            SetText(attackText, definition.Attack.ToString());
            SetText(powerText, definition.Power.ToString());
            SetText(attackRangeText, definition.AttackRange.ToString());
            SetText(critChanceText, FormatPercent(definition.CritChance));
            SetText(critMultiplierText, FormatNumber(definition.CritMultiplier) + "×");
            SetText(attackSpeedText, FormatNumber(definition.AttacksPerSecond) + "/s");
            SetText(manaThresholdText, definition.ManaThreshold.ToString());
            SetText(manaPerAttackText, FormatSigned(definition.ManaPerAttack));
            SetText(manaPerDamageTakenText, FormatSigned(definition.ManaPerDamageTaken));
            SetText(armorText, FormatPercent(definition.Armor));
            SetText(armorPenetrationText, FormatPercent(definition.ArmorPenetration));
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
            SetText(hpText, string.Empty);
            SetText(attackText, string.Empty);
            SetText(powerText, string.Empty);
            SetText(attackRangeText, string.Empty);
            SetText(critChanceText, string.Empty);
            SetText(critMultiplierText, string.Empty);
            SetText(attackSpeedText, string.Empty);
            SetText(manaThresholdText, string.Empty);
            SetText(manaPerAttackText, string.Empty);
            SetText(manaPerDamageTakenText, string.Empty);
            SetText(armorText, string.Empty);
            SetText(armorPenetrationText, string.Empty);
            SetText(specialHeaderText, string.Empty);
            SetText(specialDescriptionText, string.Empty);
            SetText(onPlayHeaderText, string.Empty);
            SetText(onPlayDescriptionText, string.Empty);
        }

        private void SetUnitDetailsVisible(bool visible)
        {
            SetGameObjectActive(unitDetailsRoot, visible);
            SetTextActive(hpText, visible);
            SetTextActive(attackText, visible);
            SetTextActive(powerText, visible);
            SetTextActive(attackRangeText, visible);
            SetTextActive(critChanceText, visible);
            SetTextActive(critMultiplierText, visible);
            SetTextActive(attackSpeedText, visible);
            SetTextActive(manaThresholdText, visible);
            SetTextActive(manaPerAttackText, visible);
            SetTextActive(manaPerDamageTakenText, visible);
            SetTextActive(armorText, visible);
            SetTextActive(armorPenetrationText, visible);
            SetAbilityDetailsVisible(specialDetailsRoot, specialHeaderText, specialDescriptionText, visible);
            SetAbilityDetailsVisible(onPlayDetailsRoot, onPlayHeaderText, onPlayDescriptionText, visible);
            SetImageActive(hpIcon, visible);
            SetImageActive(attackIcon, visible);
            SetImageActive(powerIcon, visible);
            SetImageActive(attackRangeIcon, visible);
            SetImageActive(critChanceIcon, visible);
            SetImageActive(critMultiplierIcon, visible);
            SetImageActive(attackSpeedIcon, visible);
            SetImageActive(manaThresholdIcon, visible);
            SetImageActive(manaPerAttackIcon, visible);
            SetImageActive(manaPerDamageTakenIcon, visible);
            SetImageActive(armorIcon, visible);
            SetImageActive(armorPenetrationIcon, visible);
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

        private static void SetImageActive(Image image, bool active)
        {
            if (image != null && image.gameObject.activeSelf != active)
            {
                image.gameObject.SetActive(active);
            }
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static string FormatPercent(float value)
        {
            return FormatNumber(value) + "%";
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
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

        private static string FormatNumber(float value)
        {
            return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
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
