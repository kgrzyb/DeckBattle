using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleEventQueue
    {
        private readonly List<BattleEvent> events;

        public BattleEventQueue()
            : this(32)
        {
        }

        public BattleEventQueue(int capacity)
        {
            events = new List<BattleEvent>(Math.Max(1, capacity));
        }

        public int Count
        {
            get { return events.Count; }
        }

        public IReadOnlyList<BattleEvent> Events
        {
            get { return events; }
        }

        public BattleEvent this[int index]
        {
            get { return events[index]; }
        }

        public void Enqueue(BattleEvent battleEvent)
        {
            events.Add(battleEvent);
        }

        public void Clear()
        {
            events.Clear();
        }
    }
}
