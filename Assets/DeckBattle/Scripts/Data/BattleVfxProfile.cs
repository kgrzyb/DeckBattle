using System;
using UnityEngine;

namespace DeckBattle
{
    public enum VfxSpawnSubject
    {
        Source = 0,
        Target = 1,
        SourceHex = 2,
        TargetHex = 3,
        World = 4
    }

    [Serializable]
    public struct BattleVfxBinding
    {
        public BattleVfxCue Cue;
        public VfxDefinition Effect;
        public VfxSpawnSubject Subject;
        public UnitVfxAnchor Anchor;
        public bool FollowAnchor;
        public bool FaceTarget;
        public Vector3 LocalPosition;
        public Vector3 LocalEulerAngles;
        public Vector3 LocalScale;

        public Quaternion LocalRotation
        {
            get { return Quaternion.Euler(LocalEulerAngles); }
        }

        public Vector3 ResolvedLocalScale
        {
            get { return LocalScale == Vector3.zero ? Vector3.one : LocalScale; }
        }
    }

    [CreateAssetMenu(fileName = "BattleVfxProfile", menuName = "Deck Battle/Battle VFX Profile")]
    public sealed class BattleVfxProfile : ScriptableObject
    {
        [SerializeField] private BattleVfxBinding[] bindings = Array.Empty<BattleVfxBinding>();

        [NonSerialized] private BattleVfxBinding[] bindingsByCue;

        public BattleVfxBinding[] Bindings
        {
            get { return bindings; }
        }

        public bool TryGet(BattleVfxCue cue, out BattleVfxBinding binding)
        {
            EnsureLookup();
            int index = (int)cue;
            if (index <= 0 || index >= bindingsByCue.Length)
            {
                binding = default;
                return false;
            }

            binding = bindingsByCue[index];
            return binding.Cue == cue && binding.Effect != null;
        }

        private void OnEnable()
        {
            BuildLookup();
        }

        private void OnValidate()
        {
            BuildLookup();
        }

        private void EnsureLookup()
        {
            if (bindingsByCue == null)
            {
                BuildLookup();
            }
        }

        private void BuildLookup()
        {
            int count = (int)BattleVfxCue.Death + 1;
            bindingsByCue = new BattleVfxBinding[count];
            for (int i = 0; i < bindings.Length; i++)
            {
                BattleVfxBinding binding = bindings[i];
                int index = (int)binding.Cue;
                if (index <= 0 || index >= bindingsByCue.Length || binding.Effect == null)
                {
                    continue;
                }

                if (bindingsByCue[index].Cue == BattleVfxCue.None)
                {
                    bindingsByCue[index] = binding;
                    continue;
                }

                Debug.LogError("Duplicate battle VFX binding for cue " + binding.Cue + ".", this);
            }
        }
    }
}
