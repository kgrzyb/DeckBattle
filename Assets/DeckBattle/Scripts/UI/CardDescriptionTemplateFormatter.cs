using System;
using System.Text;

namespace DeckBattle
{
    public static class CardDescriptionTemplateFormatter
    {
        public static string FormatSpecial(UnitDefinition unitDefinition)
        {
            if (unitDefinition == null || unitDefinition.Special == null)
            {
                return string.Empty;
            }

            UnitSpecialDefinition special = unitDefinition.Special;
            return Format(special.DescriptionTemplate, token => ResolveSpecialToken(unitDefinition, special, token));
        }

        public static string FormatOnPlay(UnitDefinition unitDefinition)
        {
            if (unitDefinition == null || unitDefinition.OnPlayEffect == null)
            {
                return string.Empty;
            }

            UnitOnPlayEffectDefinition effect = unitDefinition.OnPlayEffect;
            return Format(effect.DescriptionTemplate, token => ResolveOnPlayToken(unitDefinition, effect, token));
        }

        public static bool IsSpecialTemplateValid(UnitSpecialDefinition definition)
        {
            return definition == null || IsTemplateValid(definition.DescriptionTemplate, token => IsSpecialToken(token));
        }

        public static bool IsOnPlayTemplateValid(UnitOnPlayEffectDefinition definition)
        {
            return definition == null || IsTemplateValid(definition.DescriptionTemplate, token => IsOnPlayToken(definition, token));
        }

        private static string Format(string template, Func<string, TokenResolution> resolveToken)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            int tokenStart = template.IndexOf('{');
            if (tokenStart < 0)
            {
                return template;
            }

            var builder = new StringBuilder(template.Length + 16);
            int index = 0;
            while (index < template.Length)
            {
                int openingBrace = template.IndexOf('{', index);
                if (openingBrace < 0)
                {
                    builder.Append(template, index, template.Length - index);
                    break;
                }

                builder.Append(template, index, openingBrace - index);
                int closingBrace = template.IndexOf('}', openingBrace + 1);
                if (closingBrace < 0)
                {
                    builder.Append(template, openingBrace, template.Length - openingBrace);
                    break;
                }

                string token = template.Substring(openingBrace + 1, closingBrace - openingBrace - 1);
                TokenResolution resolution = resolveToken(token);
                if (resolution.IsKnown)
                {
                    builder.Append(resolution.Value);
                }
                else
                {
                    builder.Append('{').Append(token).Append('}');
                }

                index = closingBrace + 1;
            }

            return builder.ToString();
        }

        private static bool IsTemplateValid(string template, Func<string, bool> isKnownToken)
        {
            if (string.IsNullOrEmpty(template))
            {
                return true;
            }

            int index = 0;
            while (index < template.Length)
            {
                int openingBrace = template.IndexOf('{', index);
                if (openingBrace < 0)
                {
                    return true;
                }

                int closingBrace = template.IndexOf('}', openingBrace + 1);
                if (closingBrace < 0)
                {
                    return false;
                }

                string token = template.Substring(openingBrace + 1, closingBrace - openingBrace - 1);
                if (!isKnownToken(token))
                {
                    return false;
                }

                index = closingBrace + 1;
            }

            return true;
        }

        private static TokenResolution ResolveSpecialToken(UnitDefinition unitDefinition, UnitSpecialDefinition special, string token)
        {
            switch (token)
            {
                case "damagePerHit":
                    return TokenResolution.Known(DamageCalculator.CalculateBaseDamagePreview(unitDefinition.Attack, special.AttackDamageMultiplier).ToString());
                case "attackDamagePercent":
                    return TokenResolution.Known(FormatPercent(special.AttackDamageMultiplier));
                case "effectRadius":
                    return TokenResolution.Known(Math.Max(0, special.EffectRadius).ToString());
                case "totalDamage":
                    return TokenResolution.Known(CalculateTotalDamage(unitDefinition, special).ToString());
                case "strikeCount":
                    return TokenResolution.Known(Math.Max(1, special.StrikeCount).ToString());
                case "castDuration":
                    return TokenResolution.Known(FormatDuration(special.CastDuration));
                case "status":
                    return TokenResolution.Known(StatusReferenceFormatter.Format(special.AppliedStatus));
                case "statusDuration":
                    return TokenResolution.Known(FormatSpecialStatusDuration(special));
                case "statusMagnitude":
                    return TokenResolution.Known(special.AppliedStatus != null ? FormatNumber(special.AppliedStatus.DefaultMagnitude) : string.Empty);
                case "statusMagnitudePercent":
                    return TokenResolution.Known(special.AppliedStatus != null ? FormatPercent(special.AppliedStatus.DefaultMagnitude) : string.Empty);
                default:
                    return TokenResolution.Unknown;
            }
        }

        private static TokenResolution ResolveOnPlayToken(UnitDefinition unitDefinition, UnitOnPlayEffectDefinition effect, string token)
        {
            if (!TryGetStep(effect, token, out UnitEffectStepDefinition step, out string property))
            {
                return TokenResolution.Unknown;
            }

            CombatEffectDefinition combatEffect = step.Effect;
            switch (property)
            {
                case "amount":
                    return TokenResolution.Known(Math.Max(0, combatEffect.Amount).ToString());
                case "percent":
                    return TokenResolution.Known(FormatPercent(combatEffect.Percent));
                case "attackBonus":
                    return TokenResolution.Known(FormatSigned(DamageCalculator.CalculateBaseAttackBonusPreview(unitDefinition.Attack, combatEffect.Percent)));
                case "attackAfterEffect":
                    return TokenResolution.Known(DamageCalculator.CalculateBaseDamagePreview(unitDefinition.Attack, 1f + Math.Max(0f, combatEffect.Percent)).ToString());
                case "status":
                    return TokenResolution.Known(StatusReferenceFormatter.Format(combatEffect.StatusApplication.Status));
                case "statusDuration":
                    return TokenResolution.Known(FormatStatusDuration(combatEffect.StatusApplication));
                case "statusMagnitude":
                    return TokenResolution.Known(FormatStatusMagnitude(combatEffect.StatusApplication));
                case "statusMagnitudePercent":
                    return TokenResolution.Known(FormatStatusMagnitudePercent(combatEffect.StatusApplication));
                case "target":
                    return TokenResolution.Known(FormatTarget(step.Target));
                default:
                    return TokenResolution.Unknown;
            }
        }

        private static bool IsSpecialToken(string token)
        {
            return token == "damagePerHit"
                || token == "attackDamagePercent"
                || token == "effectRadius"
                || token == "totalDamage"
                || token == "strikeCount"
                || token == "castDuration"
                || token == "status"
                || token == "statusDuration"
                || token == "statusMagnitude"
                || token == "statusMagnitudePercent";
        }

        private static bool IsOnPlayToken(UnitOnPlayEffectDefinition effect, string token)
        {
            return TryGetStep(effect, token, out _, out string property)
                && (property == "amount"
                    || property == "percent"
                    || property == "attackBonus"
                    || property == "attackAfterEffect"
                    || property == "status"
                    || property == "statusDuration"
                    || property == "statusMagnitude"
                    || property == "statusMagnitudePercent"
                    || property == "target");
        }

        private static bool TryGetStep(UnitOnPlayEffectDefinition effect, string token, out UnitEffectStepDefinition step, out string property)
        {
            step = default;
            property = null;
            if (effect == null || effect.Steps == null || string.IsNullOrEmpty(token) || !token.StartsWith("step", StringComparison.Ordinal))
            {
                return false;
            }

            int separatorIndex = token.IndexOf('.', 4);
            if (separatorIndex <= 4 || separatorIndex >= token.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(token.Substring(4, separatorIndex - 4), out int oneBasedIndex))
            {
                return false;
            }

            int stepIndex = oneBasedIndex - 1;
            if (stepIndex < 0 || stepIndex >= effect.Steps.Length)
            {
                return false;
            }

            step = effect.Steps[stepIndex];
            property = token.Substring(separatorIndex + 1);
            return true;
        }

        private static int CalculateTotalDamage(UnitDefinition unitDefinition, UnitSpecialDefinition special)
        {
            int damagePerHit = DamageCalculator.CalculateBaseDamagePreview(unitDefinition.Attack, special.AttackDamageMultiplier);
            long total = (long)damagePerHit * Math.Max(1, special.StrikeCount);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        private static string FormatSpecialStatusDuration(UnitSpecialDefinition special)
        {
            if (special == null || special.AppliedStatus == null)
            {
                return string.Empty;
            }

            if (special.AppliedStatusLifetimeMode == StatusLifetimeMode.UntilCombatEnds)
            {
                return "do koĹ„ca walki";
            }

            float duration = special.AppliedStatusLifetimeMode == StatusLifetimeMode.OverrideSeconds
                ? special.AppliedStatusDurationOverride
                : special.AppliedStatus.DefaultDuration;
            return FormatDuration(duration);
        }

        private static string FormatStatusDuration(StatusApplicationDefinition application)
        {
            if (application.Status == null)
            {
                return string.Empty;
            }

            if (application.LifetimeMode == StatusLifetimeMode.UntilCombatEnds)
            {
                return "do końca walki";
            }

            float duration = application.LifetimeMode == StatusLifetimeMode.OverrideSeconds
                ? application.DurationOverride
                : application.Status.DefaultDuration;
            return FormatDuration(duration);
        }

        private static string FormatStatusMagnitude(StatusApplicationDefinition application)
        {
            if (application.Status == null)
            {
                return string.Empty;
            }

            float magnitude = application.MagnitudeOverride > 0f
                ? application.MagnitudeOverride
                : application.Status.DefaultMagnitude;
            return FormatNumber(magnitude);
        }

        private static string FormatStatusMagnitudePercent(StatusApplicationDefinition application)
        {
            if (application.Status == null)
            {
                return string.Empty;
            }

            float magnitude = application.MagnitudeOverride > 0f
                ? application.MagnitudeOverride
                : application.Status.DefaultMagnitude;
            return FormatPercent(magnitude);
        }

        private static string FormatTarget(EffectTargetDefinition target)
        {
            switch (target.Kind)
            {
                case EffectTargetKind.Self:
                    return "siebie";
                case EffectTargetKind.AllFriendlyUnits:
                    return "wszystkie sojusznicze jednostki";
                case EffectTargetKind.AllEnemyUnits:
                    return "wszystkie wrogie jednostki";
                case EffectTargetKind.FriendlyUnitsInRadius:
                    return "sojusznicze jednostki w promieniu " + Math.Max(0, target.Radius);
                case EffectTargetKind.EnemyUnitsInRadius:
                    return "wrogie jednostki w promieniu " + Math.Max(0, target.Radius);
                case EffectTargetKind.AllUnitsInRadius:
                    return "jednostki w promieniu " + Math.Max(0, target.Radius);
                default:
                    return string.Empty;
            }
        }

        private static string FormatDuration(float value)
        {
            return FormatNumber(Math.Max(0f, value)) + " s";
        }

        private static string FormatPercent(float value)
        {
            return FormatNumber(Math.Max(0f, value) * 100f) + "%";
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        private readonly struct TokenResolution
        {
            public static readonly TokenResolution Unknown = new TokenResolution(false, string.Empty);

            public readonly bool IsKnown;
            public readonly string Value;

            private TokenResolution(bool isKnown, string value)
            {
                IsKnown = isKnown;
                Value = value;
            }

            public static TokenResolution Known(string value)
            {
                return new TokenResolution(true, value ?? string.Empty);
            }
        }
    }
}
