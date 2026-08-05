using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class BattleEffectPresenter
    {
        private readonly PooledBattleEffect attackEffectPrefab;
        private readonly PooledBattleEffect damageEffectPrefab;
        private readonly Transform effectRoot;
        private readonly List<PooledBattleEffect> activeAttackEffects = new List<PooledBattleEffect>(8);
        private readonly List<PooledBattleEffect> activeDamageEffects = new List<PooledBattleEffect>(8);
        private readonly Stack<PooledBattleEffect> pooledAttackEffects = new Stack<PooledBattleEffect>(8);
        private readonly Stack<PooledBattleEffect> pooledDamageEffects = new Stack<PooledBattleEffect>(8);
        private float combatSpeed = 1f;

        public BattleEffectPresenter(PooledBattleEffect attackEffectPrefab, PooledBattleEffect damageEffectPrefab, Transform effectRoot)
        {
            this.attackEffectPrefab = attackEffectPrefab;
            this.damageEffectPrefab = damageEffectPrefab;
            this.effectRoot = effectRoot;
        }

        public void Tick()
        {
            ReleaseCompleted(activeAttackEffects, pooledAttackEffects);
            ReleaseCompleted(activeDamageEffects, pooledDamageEffects);
        }

        public void SetCombatSpeed(float speed)
        {
            float safeSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
            if (Mathf.Approximately(combatSpeed, safeSpeed))
            {
                return;
            }

            combatSpeed = safeSpeed;
            SetActiveCombatSpeed(activeAttackEffects, combatSpeed);
            SetActiveCombatSpeed(activeDamageEffects, combatSpeed);
        }

        public void PlayAttack(Vector3 position)
        {
            Spawn(attackEffectPrefab, position, activeAttackEffects, pooledAttackEffects);
        }

        public void PlayDamage(Vector3 position)
        {
            Spawn(damageEffectPrefab, position, activeDamageEffects, pooledDamageEffects);
        }

        public void Clear()
        {
            ReleaseAll(activeAttackEffects, pooledAttackEffects);
            ReleaseAll(activeDamageEffects, pooledDamageEffects);
        }

        private void Spawn(PooledBattleEffect prefab, Vector3 position, List<PooledBattleEffect> activeEffects, Stack<PooledBattleEffect> pooledEffects)
        {
            if (prefab == null)
            {
                return;
            }

            PooledBattleEffect effect = pooledEffects.Count > 0 ? pooledEffects.Pop() : Object.Instantiate(prefab, effectRoot);
            effect.SetCombatSpeed(combatSpeed);
            effect.Play(position);
            activeEffects.Add(effect);
        }

        private static void ReleaseCompleted(List<PooledBattleEffect> activeEffects, Stack<PooledBattleEffect> pooledEffects)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                PooledBattleEffect effect = activeEffects[i];
                if (effect != null && effect.IsPlaying)
                {
                    continue;
                }

                if (effect != null)
                {
                    effect.SetCombatSpeed(1f);
                    effect.gameObject.SetActive(false);
                    pooledEffects.Push(effect);
                }

                activeEffects.RemoveAt(i);
            }
        }

        private static void ReleaseAll(List<PooledBattleEffect> activeEffects, Stack<PooledBattleEffect> pooledEffects)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                PooledBattleEffect effect = activeEffects[i];
                if (effect != null)
                {
                    effect.SetCombatSpeed(1f);
                    effect.gameObject.SetActive(false);
                    pooledEffects.Push(effect);
                }
            }

            activeEffects.Clear();
        }

        private static void SetActiveCombatSpeed(List<PooledBattleEffect> activeEffects, float speed)
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                PooledBattleEffect effect = activeEffects[i];
                if (effect != null)
                {
                    effect.SetCombatSpeed(speed);
                }
            }
        }
    }
}
