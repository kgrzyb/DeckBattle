using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "UnitSpecialDefinition", menuName = "Deck Battle/Unit Special Definition")]
    public sealed class UnitSpecialDefinition : ScriptableObject
    {
        public string SpecialId;
        public UnitSpecialKind Kind;
        [Min(0f)] public float WindupDuration = 0.25f;
        public StatusDefinition AppliedStatus;

        private void OnValidate()
        {
            WindupDuration = Mathf.Max(0f, WindupDuration);
        }
    }
}
