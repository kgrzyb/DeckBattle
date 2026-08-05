using System;
using UnityEngine;

namespace DeckBattle
{
    public enum EffectTargetKind
    {
        Self = 0,
        AllFriendlyUnits = 1,
        AllEnemyUnits = 2,
        FriendlyUnitsInRadius = 3,
        EnemyUnitsInRadius = 4,
        AllUnitsInRadius = 5,
        SelectedUnit = 6,
        SelectedHex = 7
    }

    [Serializable]
    public struct EffectTargetDefinition
    {
        public EffectTargetKind Kind;
        [Min(0)] public int Radius;
    }

    [Serializable]
    public struct UnitEffectStepDefinition
    {
        public EffectTargetDefinition Target;
        public CombatEffectDefinition Effect;
    }

    [CreateAssetMenu(fileName = "UnitOnPlayEffect", menuName = "Deck Battle/Unit On Play Effect")]
    public sealed class UnitOnPlayEffectDefinition : ScriptableObject
    {
        public string EffectId;
        public string DisplayName;
        [TextArea] public string Description;
        public UnitEffectStepDefinition[] Steps = Array.Empty<UnitEffectStepDefinition>();

        private void OnValidate()
        {
            if (EffectId != null)
            {
                EffectId = EffectId.Trim();
            }

            if (Steps == null)
            {
                Steps = Array.Empty<UnitEffectStepDefinition>();
            }

            for (int i = 0; i < Steps.Length; i++)
            {
                UnitEffectStepDefinition step = Steps[i];
                step.Target.Radius = Mathf.Max(0, step.Target.Radius);
                Steps[i] = step;
            }
        }
    }
}
