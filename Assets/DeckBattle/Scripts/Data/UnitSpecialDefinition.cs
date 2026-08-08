using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "UnitSpecialDefinition", menuName = "Deck Battle/Unit Special Definition")]
    public sealed class UnitSpecialDefinition : ScriptableObject
    {
        public const int MaxStrikeCount = 10;

        public string SpecialId;
        public UnitSpecialKind Kind;
        [TextArea] public string DescriptionTemplate;
        [Min(0f)] public float WindupDuration = 0.25f;
        [Min(0f)] public float CastDuration;
        public StatusDefinition AppliedStatus;
        [Range(1, MaxStrikeCount)] public int StrikeCount = 1;
        [Min(0f)] public float AttackDamageMultiplier = 1f;

        private void OnValidate()
        {
            WindupDuration = Mathf.Max(0f, WindupDuration);
            CastDuration = Mathf.Max(0f, CastDuration);
            StrikeCount = Mathf.Clamp(StrikeCount, 1, MaxStrikeCount);
            AttackDamageMultiplier = Mathf.Max(0f, AttackDamageMultiplier);
        }
    }
}
