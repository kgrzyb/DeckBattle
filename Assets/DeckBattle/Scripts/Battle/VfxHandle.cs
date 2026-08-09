namespace DeckBattle
{
    public readonly struct VfxHandle
    {
        internal readonly int InstanceId;
        internal readonly int Generation;

        internal VfxHandle(int instanceId, int generation)
        {
            InstanceId = instanceId;
            Generation = generation;
        }

        public bool IsValid
        {
            get { return InstanceId > 0 && Generation > 0; }
        }
    }
}
