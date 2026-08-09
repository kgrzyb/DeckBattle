using System;
using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "StatusPresentationCatalog", menuName = "Deck Battle/Status Presentation Catalog")]
    public sealed class StatusPresentationCatalog : ScriptableObject
    {
        [SerializeField] private StatusPresentationEntry[] entries = Array.Empty<StatusPresentationEntry>();

        [NonSerialized] private StatusPresentationEntry[] entriesByKind;

        public StatusPresentationEntry[] Entries { get { return entries; } }

        public bool TryGet(StatusKind kind, out StatusPresentationEntry entry)
        {
            EnsureLookup();
            int index = (int)kind;
            if (index < 0 || index >= entriesByKind.Length || entriesByKind[index] == null)
            {
                entry = null;
                return false;
            }

            entry = entriesByKind[index];
            return true;
        }

        private void OnEnable() { BuildLookup(); }
        private void OnValidate() { BuildLookup(); }

        private void EnsureLookup()
        {
            if (entriesByKind == null)
            {
                BuildLookup();
            }
        }

        private void BuildLookup()
        {
            int size = Mathf.Max(1, (int)StatusKind.Guard + 1);
            entriesByKind = new StatusPresentationEntry[size];
            for (int i = 0; i < entries.Length; i++)
            {
                StatusPresentationEntry entry = entries[i];
                if (entry == null || entry.Kind == StatusKind.None)
                {
                    continue;
                }

                int index = (int)entry.Kind;
                if (entriesByKind[index] == null)
                {
                    entriesByKind[index] = entry;
                }
                else
                {
                    Debug.LogError("Duplicate status presentation entry for " + entry.Kind + ".", this);
                }
            }
        }
    }

    [Serializable]
    public sealed class StatusPresentationEntry
    {
        public StatusKind Kind;
        public StatusPresentationMode Mode;
        [Range(0, 100)] public int Priority = 50;
        public Sprite Icon;
        public VfxDefinition ApplyVfxDefinition;
        public VfxDefinition ActiveVfxDefinition;
        public VfxDefinition RemoveVfxDefinition;
        public Vector3 LocalPosition;
        public Vector3 LocalEulerAngles;
        public Vector3 LocalScale = Vector3.one;
    }
}
