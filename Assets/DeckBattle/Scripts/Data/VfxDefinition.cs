using UnityEngine;

namespace DeckBattle
{
    public enum VfxLifetimeMode
    {
        Duration = 0,
        ParticleSystemAlive = 1,
        Manual = 2
    }

    [CreateAssetMenu(fileName = "VfxDefinition", menuName = "Deck Battle/VFX Definition")]
    public sealed class VfxDefinition : ScriptableObject
    {
        public PooledVfxView Prefab;
        public VfxLifetimeMode LifetimeMode = VfxLifetimeMode.Duration;
        [Min(0.01f)] public float FallbackLifetime = 0.5f;
        [Min(0)] public int PrewarmCount = 2;
        [Min(1)] public int MaxActiveCount = 16;
        [Min(0)] public int MaxRetainedCount = 8;
        public bool ScaleWithCombatSpeed = true;

        private void OnValidate()
        {
            FallbackLifetime = Mathf.Max(0.01f, FallbackLifetime);
            PrewarmCount = Mathf.Max(0, PrewarmCount);
            MaxActiveCount = Mathf.Max(1, MaxActiveCount);
            MaxRetainedCount = Mathf.Max(0, MaxRetainedCount);
            if (PrewarmCount > MaxActiveCount)
            {
                PrewarmCount = MaxActiveCount;
            }
        }
    }
}
