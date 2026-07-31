using System;

namespace DeckBattle
{
    public readonly struct StatusApplicationRequest
    {
        public readonly StatusCombatSpec CombatSpec;
        public readonly int SourceUnitId;
        public readonly int LinkedUnitId;
        public readonly float Duration;
        public readonly float Magnitude;
        public readonly float Interval;
        public readonly int Stacks;

        public StatusApplicationRequest(StatusDefinition definition, int sourceUnitId, float duration = -1f, float magnitude = -1f, float interval = -1f, int stacks = 1, int linkedUnitId = 0)
            : this(StatusCombatSpec.FromDefinition(definition), sourceUnitId, duration, magnitude, interval, stacks, linkedUnitId)
        {
        }

        public StatusApplicationRequest(StatusCombatSpec combatSpec, int sourceUnitId, float duration = -1f, float magnitude = -1f, float interval = -1f, int stacks = 1, int linkedUnitId = 0)
        {
            CombatSpec = combatSpec;
            SourceUnitId = sourceUnitId;
            LinkedUnitId = linkedUnitId;
            Duration = duration;
            Magnitude = magnitude;
            Interval = interval;
            Stacks = stacks;
        }
    }
}
