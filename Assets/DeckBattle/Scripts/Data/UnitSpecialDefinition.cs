using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "UnitSpecialDefinition", menuName = "Deck Battle/Unit Special Definition")]
    public sealed class UnitSpecialDefinition : ScriptableObject
    {
        public string SpecialId;
        public UnitSpecialKind Kind;
        public StatusDefinition AppliedStatus;
    }
}
