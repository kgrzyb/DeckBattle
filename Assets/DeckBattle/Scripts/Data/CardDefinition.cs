using UnityEngine;
using UnityEngine.Serialization;

namespace DeckBattle
{
    public class CardDefinition : ScriptableObject
    {
        [FormerlySerializedAs("UnitId")] public string CardId;
        public string DisplayName;
        public UnitRarity Rarity;
        public int ApCost = 1;
        public Sprite CardArt;
        [SerializeField] private CardKind cardKind = DeckBattle.CardKind.Unit;

        public virtual CardKind CardKind
        {
            get { return cardKind; }
        }

        public string Id
        {
            get { return CardId; }
            set { CardId = value; }
        }

        protected void SetCardKind(CardKind value)
        {
            cardKind = value;
        }

        protected virtual void OnValidate()
        {
            if (CardId != null)
            {
                CardId = CardId.Trim();
            }

            ApCost = Mathf.Max(0, ApCost);
        }
    }
}
