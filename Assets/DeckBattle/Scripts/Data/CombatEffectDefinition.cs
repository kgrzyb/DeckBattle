using System;
using UnityEngine;

namespace DeckBattle
{
    public enum CombatEffectKind
    {
        Status = 0,
        Damage = 1,
        Heal = 2,
        Drain = 3,
        ResetWinddown = 4,
        ModifyBaseAttackPercent = 5
    }

    [Serializable]
    public struct CombatEffectDefinition
    {
        public CombatEffectKind Kind;
        public StatusApplicationDefinition StatusApplication;
        [Min(0)] public int Amount;
        [Min(0f)] public float Percent;
    }
}
