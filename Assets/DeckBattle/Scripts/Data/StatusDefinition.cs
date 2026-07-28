using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "StatusDefinition", menuName = "Deck Battle/Status Definition")]
    public sealed class StatusDefinition : ScriptableObject
    {
        public StatusKind Kind;
        public StatusCategory Category;
        public StatusStackingRule StackingRule = StatusStackingRule.RefreshPerSource;
        [Min(0.01f)] public float DefaultDuration = 1f;
        [Min(0f)] public float DefaultInterval;
        [Min(0f)] public float DefaultMagnitude;
        [Min(1)] public int MaxStacks = 1;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public Color DisplayColor = Color.white;

        private void OnValidate()
        {
            DefaultDuration = Mathf.Max(0.01f, DefaultDuration);
            DefaultInterval = Mathf.Max(0f, DefaultInterval);
            DefaultMagnitude = Mathf.Max(0f, DefaultMagnitude);
            MaxStacks = Mathf.Max(1, MaxStacks);
        }
    }
}
