using UnityEngine;

namespace DeckBattle
{
    public sealed class StatusVfxView : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particleSystems;
        [SerializeField, Min(0.01f)] private float oneShotLifetime = 0.5f;

        private float releaseTime;
        private bool isOneShot;

        public bool IsOneShotComplete { get { return isOneShot && Time.time >= releaseTime; } }

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
            releaseTime = Time.time + Mathf.Max(0.01f, oneShotLifetime);
            PlayParticles();
        }

        public void BeginActive(Transform pivot, StatusPresentationEntry entry)
        {
            Attach(pivot, entry);
            isOneShot = false;
            releaseTime = 0f;
            PlayParticles();
        }

        public void Release()
        {
            StopAndClearParticles();

            isOneShot = false;
            releaseTime = 0f;
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
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                {
                    particleSystems[i].Clear(true);
                    particleSystems[i].Play(true);
                }
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
