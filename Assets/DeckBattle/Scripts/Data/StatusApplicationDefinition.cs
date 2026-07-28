using System;
using UnityEngine;

namespace DeckBattle
{
    [Serializable]
    public struct StatusApplicationDefinition
    {
        public StatusDefinition Status;
        [Min(0f)] public float DurationOverride;
        [Min(0f)] public float MagnitudeOverride;
        [Min(0f)] public float IntervalOverride;
        [Min(0)] public int StacksOverride;
    }
}
