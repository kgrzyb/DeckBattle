using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "ProjectileDefinition", menuName = "Deck Battle/Projectile Definition")]
    public sealed class ProjectileDefinition : ScriptableObject
    {
        public string ProjectileId;
        public float Speed = 6f;
        public ProjectileView ProjectilePrefab;
        public float SpawnHeight = 0.5f;
        public float HitHeight = 0.5f;

        private void OnValidate()
        {
            Speed = Mathf.Max(0.01f, Speed);
            SpawnHeight = Mathf.Max(0f, SpawnHeight);
            HitHeight = Mathf.Max(0f, HitHeight);
        }
    }
}
