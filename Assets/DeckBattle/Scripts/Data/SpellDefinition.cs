using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "SpellDefinition", menuName = "Deck Battle/Spell Definition")]
    public sealed class SpellDefinition : CardDefinition
    {
        public SpellEffectKind EffectKind = SpellEffectKind.BuffAttackNextCombat;
        public SpellTargetingKind TargetingKind = SpellTargetingKind.FriendlyUnit;
        public int Amount = 1;

        public override CardKind CardKind
        {
            get { return DeckBattle.CardKind.Spell; }
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            SetCardKind(DeckBattle.CardKind.Spell);
            Amount = Mathf.Max(0, Amount);
        }
    }
}
