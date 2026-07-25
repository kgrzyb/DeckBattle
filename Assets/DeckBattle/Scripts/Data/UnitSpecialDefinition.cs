using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "UnitSpecialDefinition", menuName = "Deck Battle/Unit Special Definition")]
    public sealed class UnitSpecialDefinition : ScriptableObject
    {
        public string SpecialId;
        public UnitSpecialKind Kind;
        public float Duration;
        public float AttackCooldownMultiplier = 1f;

        private void OnValidate()
        {
            Duration = Mathf.Max(0f, Duration);
            AttackCooldownMultiplier = Mathf.Max(0.01f, AttackCooldownMultiplier);
        }
    }
}
