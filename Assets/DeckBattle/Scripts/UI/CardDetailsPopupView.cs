using TMPro;
using UnityEngine;
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
        [SerializeField] private TextMeshProUGUI critText;
        [SerializeField] private TextMeshProUGUI cooldownText;
        [SerializeField] private TextMeshProUGUI manaText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private TextMeshProUGUI armorPenetrationText;
        [SerializeField] private TextMeshProUGUI typeText;
        [SerializeField] private TextMeshProUGUI rarityText;
        [SerializeField] private GameObject unitDetailsRoot;
        [SerializeField] private GameObject spellDetailsRoot;
        [SerializeField] private TextMeshProUGUI spellTargetText;
        [SerializeField] private TextMeshProUGUI spellEffectText;
        [SerializeField] private TextMeshProUGUI spellAmountText;
        [SerializeField] private TextMeshProUGUI spellDescriptionText;

        private CardRuntimeState shownCard;
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            EnsureLayout();
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

        public void Hide()
        {
            shownCard = null;
            SetVisible(false);
        }

        public bool IsShownFor(CardRuntimeState card)
        {
            return gameObject.activeSelf && shownCard == card;
        }

        private void Apply(CardRuntimeState card)
        {
            shownCard = card;
            CardDefinition definition = card.Definition;
            UnitDefinition unitDefinition = card.UnitDefinition;
            SpellDefinition spellDefinition = card.SpellDefinition;

            SetText(nameText, definition.DisplayName);
            SetText(apCostText, "AP " + definition.ApCost);
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

        private void ApplyUnitDetails(UnitDefinition definition)
        {
            SetUnitDetailsVisible(true);
            SetSpellDetailsVisible(false);
            SetText(hpText, "HP " + definition.MaxHp);
            SetText(attackText, "Attack " + definition.Attack);
            SetText(powerText, "Power " + definition.Power);
            SetText(attackRangeText, "Range " + definition.AttackRange);
            SetText(critText, "Crit " + FormatPercent(definition.CritChance) + " x" + FormatNumber(definition.CritMultiplier));
            SetText(cooldownText, "Cooldown " + FormatNumber(definition.AttackCooldown) + "s");
            SetText(manaText, "Mana " + definition.ManaThreshold + " / +" + definition.ManaPerAttack + " atk / +" + definition.ManaPerDamageTaken + " hit");
            SetText(armorText, "Armor " + FormatPercent(definition.Armor));
            SetText(armorPenetrationText, "Pen " + FormatPercent(definition.ArmorPenetration));
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
            SetText(critText, string.Empty);
            SetText(cooldownText, string.Empty);
            SetText(manaText, string.Empty);
            SetText(armorText, string.Empty);
            SetText(armorPenetrationText, string.Empty);
        }

        private void SetUnitDetailsVisible(bool visible)
        {
            SetGameObjectActive(unitDetailsRoot, visible);
            SetTextActive(hpText, visible);
            SetTextActive(attackText, visible);
            SetTextActive(powerText, visible);
            SetTextActive(attackRangeText, visible);
            SetTextActive(critText, visible);
            SetTextActive(cooldownText, visible);
            SetTextActive(manaText, visible);
            SetTextActive(armorText, visible);
            SetTextActive(armorPenetrationText, visible);
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

        private static string FormatPercent(float value)
        {
            return FormatNumber(value) + "%";
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

        private static string FormatNumber(float value)
        {
            return value.ToString("0.#");
        }

        private void EnsureLayout()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
            }

            if (backgroundImage == null)
            {
                backgroundImage = gameObject.AddComponent<Image>();
            }

            backgroundImage.color = new Color(0.08f, 0.10f, 0.12f, 0.94f);
            backgroundImage.raycastTarget = false;

            if (cardArtImage == null)
            {
                cardArtImage = CreateImage("CardArt", new Vector2(0.03f, 0.14f), new Vector2(0.23f, 0.86f), new Color(0.13f, 0.16f, 0.18f, 1f), transform);
            }

            if (unitDetailsRoot == null)
            {
                unitDetailsRoot = CreateRoot("UnitDetails");
            }

            if (spellDetailsRoot == null)
            {
                spellDetailsRoot = CreateRoot("SpellDetails");
            }

            Transform unitParent = unitDetailsRoot != null ? unitDetailsRoot.transform : transform;
            Transform spellParent = spellDetailsRoot != null ? spellDetailsRoot.transform : transform;

            if (nameText == null)
            {
                nameText = CreateText("Name", new Vector2(0.27f, 0.68f), new Vector2(0.62f, 0.9f), 30, TextAlignmentOptions.Left, "Unit", transform);
            }

            if (apCostText == null)
            {
                apCostText = CreateText("ApCost", new Vector2(0.64f, 0.68f), new Vector2(0.77f, 0.9f), 26, TextAlignmentOptions.Center, "AP 1", transform);
            }

            if (typeText == null)
            {
                typeText = CreateText("Type", new Vector2(0.79f, 0.68f), new Vector2(0.94f, 0.9f), 22, TextAlignmentOptions.Right, "Type", transform);
            }

            if (rarityText == null)
            {
                rarityText = CreateText("Rarity", new Vector2(0.27f, 0.54f), new Vector2(0.48f, 0.66f), 20, TextAlignmentOptions.Left, "Rarity", transform);
            }

            if (hpText == null)
            {
                hpText = CreateText("Hp", new Vector2(0.27f, 0.39f), new Vector2(0.42f, 0.52f), 21, TextAlignmentOptions.Left, "HP 1", unitParent);
            }

            if (attackText == null)
            {
                attackText = CreateText("Attack", new Vector2(0.43f, 0.39f), new Vector2(0.6f, 0.52f), 21, TextAlignmentOptions.Left, "Attack 1", unitParent);
            }

            if (powerText == null)
            {
                powerText = CreateText("Power", new Vector2(0.61f, 0.39f), new Vector2(0.77f, 0.52f), 21, TextAlignmentOptions.Left, "Power 1", unitParent);
            }

            if (attackRangeText == null)
            {
                attackRangeText = CreateText("Range", new Vector2(0.78f, 0.39f), new Vector2(0.94f, 0.52f), 21, TextAlignmentOptions.Left, "Range 1", unitParent);
            }

            if (armorText == null)
            {
                armorText = CreateText("Armor", new Vector2(0.27f, 0.24f), new Vector2(0.44f, 0.37f), 20, TextAlignmentOptions.Left, "Armor 0%", unitParent);
            }

            if (armorPenetrationText == null)
            {
                armorPenetrationText = CreateText("ArmorPenetration", new Vector2(0.45f, 0.24f), new Vector2(0.62f, 0.37f), 20, TextAlignmentOptions.Left, "Pen 0%", unitParent);
            }

            if (critText == null)
            {
                critText = CreateText("Crit", new Vector2(0.63f, 0.24f), new Vector2(0.94f, 0.37f), 20, TextAlignmentOptions.Left, "Crit 0% x2", unitParent);
            }

            if (cooldownText == null)
            {
                cooldownText = CreateText("Cooldown", new Vector2(0.27f, 0.09f), new Vector2(0.48f, 0.22f), 20, TextAlignmentOptions.Left, "Cooldown 1s", unitParent);
            }

            if (manaText == null)
            {
                manaText = CreateText("Mana", new Vector2(0.49f, 0.09f), new Vector2(0.94f, 0.22f), 20, TextAlignmentOptions.Left, "Mana 100", unitParent);
            }

            if (spellTargetText == null)
            {
                spellTargetText = CreateText("SpellTarget", new Vector2(0.27f, 0.39f), new Vector2(0.54f, 0.52f), 21, TextAlignmentOptions.Left, "Target", spellParent);
            }

            if (spellEffectText == null)
            {
                spellEffectText = CreateText("SpellEffect", new Vector2(0.55f, 0.39f), new Vector2(0.94f, 0.52f), 21, TextAlignmentOptions.Left, "Effect", spellParent);
            }

            if (spellAmountText == null)
            {
                spellAmountText = CreateText("SpellAmount", new Vector2(0.27f, 0.24f), new Vector2(0.48f, 0.37f), 20, TextAlignmentOptions.Left, "Amount", spellParent);
            }

            if (spellDescriptionText == null)
            {
                spellDescriptionText = CreateText("SpellDescription", new Vector2(0.49f, 0.09f), new Vector2(0.94f, 0.37f), 20, TextAlignmentOptions.Left, "Description", spellParent);
                spellDescriptionText.enableWordWrapping = true;
            }
        }

        private GameObject CreateRoot(string objectName)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(transform, false);

            RectTransform childTransform = (RectTransform)child.transform;
            childTransform.anchorMin = Vector2.zero;
            childTransform.anchorMax = Vector2.one;
            childTransform.anchoredPosition = Vector2.zero;
            childTransform.sizeDelta = Vector2.zero;
            return child;
        }

        private Image CreateImage(string objectName, Vector2 anchorMin, Vector2 anchorMax, Color color, Transform parent)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);

            RectTransform childTransform = (RectTransform)child.transform;
            childTransform.anchorMin = anchorMin;
            childTransform.anchorMax = anchorMax;
            childTransform.anchoredPosition = Vector2.zero;
            childTransform.sizeDelta = Vector2.zero;

            Image image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private TextMeshProUGUI CreateText(string objectName, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment, string initialText, Transform parent)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);

            RectTransform childTransform = (RectTransform)child.transform;
            childTransform.anchorMin = anchorMin;
            childTransform.anchorMax = anchorMax;
            childTransform.anchoredPosition = Vector2.zero;
            childTransform.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            text.text = initialText;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
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
