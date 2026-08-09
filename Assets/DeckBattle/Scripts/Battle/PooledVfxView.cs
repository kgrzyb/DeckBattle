using UnityEngine;

namespace DeckBattle
{
    // Passive VFX prefab view. BattleVfxPool owns its update and lifetime.
    public sealed class PooledVfxView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField] private TrailRenderer[] trailRenderers;
        [SerializeField] private Animator[] animators;

        private float[] particleBaseSimulationSpeeds;
        private float[] animatorBaseSpeeds;
        private float remainingLifetime;
        private float combatSpeed = 1f;
        private bool scaleWithCombatSpeed;
        private bool isPlaying;
        private bool cachedReferences;
        private VfxLifetimeMode lifetimeMode;
        private int instanceId;
        private int generation;

        public bool IsPlaying
        {
            get { return isPlaying; }
        }

        internal int InstanceId
        {
            get { return instanceId; }
        }

        internal int Generation
        {
            get { return generation; }
        }

        private void Awake()
        {
            CacheReferences();
        }

        internal void AssignPoolIdentity(int id)
        {
            instanceId = id;
        }

        internal int Play(
            VfxDefinition definition,
            in VfxSpawnRequest request,
            Transform poolRoot,
            float currentCombatSpeed)
        {
            CacheReferences();
            StopAndClearVisuals();
            ResetAnimators();

            scaleWithCombatSpeed = definition.ScaleWithCombatSpeed;
            lifetimeMode = definition.LifetimeMode;
            remainingLifetime = Mathf.Max(0.01f, definition.FallbackLifetime);
            generation = generation == int.MaxValue ? 1 : generation + 1;

            if (request.FollowAnchor)
            {
                transform.SetParent(request.Anchor, false);
                transform.localPosition = request.LocalPosition;
                transform.localRotation = request.LocalRotation;
                transform.localScale = request.LocalScale;
            }
            else
            {
                transform.SetParent(poolRoot, false);
                transform.SetPositionAndRotation(request.WorldPosition, request.WorldRotation);
                transform.localScale = request.LocalScale;
            }

            gameObject.SetActive(true);
            isPlaying = true;
            SetCombatSpeed(currentCombatSpeed);
            PlayVisuals();
            return generation;
        }

        internal bool Advance(float deltaTime)
        {
            if (!isPlaying || lifetimeMode == VfxLifetimeMode.Manual)
            {
                return false;
            }

            float effectiveDeltaTime = Mathf.Max(0f, deltaTime) * (scaleWithCombatSpeed ? combatSpeed : 1f);
            remainingLifetime = Mathf.Max(0f, remainingLifetime - effectiveDeltaTime);
            if (lifetimeMode == VfxLifetimeMode.Duration)
            {
                return remainingLifetime <= 0f;
            }

            // A misconfigured ParticleSystemAlive definition still gets its fallback
            // lifetime when the prefab contains no ParticleSystem at all.
            return remainingLifetime <= 0f
                || (particleSystems.Length > 0 && !AreParticlesAlive());
        }

        internal void SetCombatSpeed(float speed)
        {
            combatSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
            float visualSpeed = scaleWithCombatSpeed ? combatSpeed : 1f;
            ApplyVisualSpeed(visualSpeed);
        }

        internal void Release()
        {
            CacheReferences();
            StopAndClearVisuals();
            ResetAnimators();
            ApplyVisualSpeed(1f);
            isPlaying = false;
            remainingLifetime = 0f;
            combatSpeed = 1f;
            gameObject.SetActive(false);
        }

        private void CacheReferences()
        {
            if (cachedReferences)
            {
                return;
            }

            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }

            if (trailRenderers == null || trailRenderers.Length == 0)
            {
                trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
            }

            if (animators == null || animators.Length == 0)
            {
                animators = GetComponentsInChildren<Animator>(true);
            }

            particleBaseSimulationSpeeds = new float[particleSystems.Length];
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                particleBaseSimulationSpeeds[i] = particleSystem != null
                    ? particleSystem.main.simulationSpeed
                    : 1f;
            }

            animatorBaseSpeeds = new float[animators.Length];
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                animatorBaseSpeeds[i] = animator != null ? animator.speed : 1f;
            }

            cachedReferences = true;
        }

        private bool AreParticlesAlive()
        {
            if (particleSystems.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem != null && particleSystem.IsAlive(true))
                {
                    return true;
                }
            }

            return false;
        }

        private void PlayVisuals()
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Clear(true);
                    particleSystem.Play(true);
                }
            }
        }

        private void StopAndClearVisuals()
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem != null)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            for (int i = 0; i < trailRenderers.Length; i++)
            {
                TrailRenderer trailRenderer = trailRenderers[i];
                if (trailRenderer != null)
                {
                    trailRenderer.Clear();
                }
            }
        }

        private void ResetAnimators()
        {
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator != null)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
            }
        }

        private void ApplyVisualSpeed(float speed)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                main.simulationSpeed = particleBaseSimulationSpeeds[i] * speed;
            }

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator != null)
                {
                    animator.speed = animatorBaseSpeeds[i] * speed;
                }
            }
        }
    }
}
