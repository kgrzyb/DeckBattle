using System;

namespace DeckBattle
{
    public sealed class UnitStatusCollection
    {
        private readonly StatusInstance[] instances;
        private int count;
        private int nextApplicationSequenceId;

        public UnitStatusCollection(int capacity)
        {
            instances = new StatusInstance[Math.Max(1, capacity)];
        }

        public int Count { get { return count; } }
        public int Capacity { get { return instances.Length; } }
        public StatusInstance this[int index] { get { return instances[index]; } }

        public bool TryFind(StatusKind kind, int sourceUnitId, out int index)
        {
            for (int i = 0; i < count; i++)
            {
                if (instances[i].Kind == kind && instances[i].SourceUnitId == sourceUnitId)
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        public bool TryFind(StatusKind kind, out int index)
        {
            for (int i = 0; i < count; i++)
            {
                if (instances[i].Kind == kind)
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        public bool TryAdd(StatusInstance instance, out int index)
        {
            if (count == instances.Length)
            {
                index = -1;
                return false;
            }
            instance.ApplicationSequenceId = ++nextApplicationSequenceId;
            instances[count] = instance;
            index = count++;
            return true;
        }

        public void Set(int index, StatusInstance instance) { instances[index] = instance; }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            int last = count - 1;
            for (int i = index; i < last; i++) instances[i] = instances[i + 1];
            instances[last] = default;
            count = last;
        }

        public void Clear()
        {
            Array.Clear(instances, 0, count);
            count = 0;
            nextApplicationSequenceId = 0;
        }
    }
}
