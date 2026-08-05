using System;
using UnityEngine;

namespace DeckBattle
{
    public enum StatusLifetimeMode
    {
        UseDefinitionDuration = 0,
        OverrideSeconds = 1,
        UntilCombatEnds = 2
    }

    [Serializable]
    public struct StatusApplicationDefinition
    {
        public StatusDefinition Status;
        public StatusLifetimeMode LifetimeMode;
        [Min(0f)] public float DurationOverride;
        [Min(0f)] public float MagnitudeOverride;
        [Min(0f)] public float IntervalOverride;
        [Min(0)] public int StacksOverride;
    }
}
