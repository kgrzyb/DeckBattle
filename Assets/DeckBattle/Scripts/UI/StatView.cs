using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle
{
    public enum UnitStatType
    {
        Hp = 0,
        Attack = 1,
        Power = 2,
        AttackRange = 3,
        CritChance = 4,
        CritMultiplier = 5,
        AttackSpeed = 6,
        ManaThreshold = 7,
        ManaPerSecond = 8,
        Armor = 10,
        ArmorPenetration = 11
    }

    public sealed class StatView : MonoBehaviour
    {
        [SerializeField] private UnitStatType statType;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI valueText;

        public UnitStatType StatType
        {
            get { return statType; }
        }

        public void Apply(UnitDefinition definition)
        {
            if (valueText != null)
            {
                valueText.text = definition != null ? FormatValue(definition) : string.Empty;
            }
        }

        public void Clear()
        {
            if (valueText != null)
            {
                valueText.text = string.Empty;
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        private string FormatValue(UnitDefinition definition)
        {
            switch (statType)
            {
                case UnitStatType.Hp:
                    return definition.MaxHp.ToString();
                case UnitStatType.Attack:
                    return definition.Attack.ToString();
                case UnitStatType.Power:
                    return definition.Power.ToString();
                case UnitStatType.AttackRange:
                    return definition.AttackRange + " hex";
                case UnitStatType.CritChance:
                    return FormatCritChance(definition.CritChance);
                case UnitStatType.CritMultiplier:
                    return FormatNumber(definition.CritMultiplier) + "×";
                case UnitStatType.AttackSpeed:
                    return FormatNumber(definition.AttacksPerSecond) + "/s";
                case UnitStatType.ManaThreshold:
                    return definition.ManaThreshold.ToString();
                case UnitStatType.ManaPerSecond:
                    return "+ " + definition.ManaPerSecond + "/s";
                case UnitStatType.Armor:
                    return FormatPercent(definition.Armor);
                case UnitStatType.ArmorPenetration:
                    return FormatPercent(definition.ArmorPenetration);
                default:
                    return string.Empty;
            }
        }

        private static string FormatPercent(float value)
        {
            return FormatNumber(value) + "%";
        }

        private static string FormatCritChance(float value)
        {
            float percentage = Mathf.Abs(value) <= 1f ? value * 100f : value;
            return Mathf.RoundToInt(percentage).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }
}
