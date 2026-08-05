using UnityEngine;

namespace DeckBattle
{
    public sealed class StatusVfxView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField, Min(0.01f)] private float oneShotLifetime = 0.5f;

        private float oneShotLifetimeRemaining;
        private float combatSpeed = 1f;
        private bool isOneShot;

        public bool IsOneShotComplete { get { return isOneShot && oneShotLifetimeRemaining <= 0f; } }

        private void Awake()
        {
            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }
        }

        public void PlayOneShot(Transform pivot, StatusPresentationEntry entry)
        {
            Attach(pivot, entry);
            isOneShot = true;
            oneShotLifetimeRemaining = Mathf.Max(0.01f, oneShotLifetime);
            PlayParticles();
        }

        public void BeginActive(Transform pivot, StatusPresentationEntry entry)
        {
            Attach(pivot, entry);
            isOneShot = false;
            oneShotLifetimeRemaining = 0f;
            PlayParticles();
        }

        public void AdvanceOneShot(float deltaTime)
        {
            if (!isOneShot || oneShotLifetimeRemaining <= 0f)
            {
                return;
            }

            oneShotLifetimeRemaining = Mathf.Max(0f, oneShotLifetimeRemaining - Mathf.Max(0f, deltaTime));
        }

        public void SetCombatSpeed(float speed)
        {
            combatSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
            ApplyParticleSimulationSpeed();
        }

        public void Release()
        {
            StopAndClearParticles();

            isOneShot = false;
            oneShotLifetimeRemaining = 0f;
            SetCombatSpeed(1f);
            gameObject.SetActive(false);
        }

        private void Attach(Transform pivot, StatusPresentationEntry entry)
        {
            transform.SetParent(pivot, false);
            transform.localPosition = entry.LocalPosition;
            transform.localRotation = Quaternion.Euler(entry.LocalEulerAngles);
            transform.localScale = entry.LocalScale == Vector3.zero ? Vector3.one : entry.LocalScale;
            gameObject.SetActive(true);
        }

        private void PlayParticles()
        {
            ApplyParticleSimulationSpeed();
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                {
                    particleSystems[i].Clear(true);
                    particleSystems[i].Play(true);
                }
            }
        }

        private void ApplyParticleSimulationSpeed()
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particleSystem.main;
                main.simulationSpeed = combatSpeed;
            }
        }

        private void StopAndClearParticles()
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                {
                    particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
