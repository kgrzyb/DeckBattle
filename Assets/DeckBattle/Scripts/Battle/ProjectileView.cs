using UnityEngine;

namespace DeckBattle
{
    public sealed class ProjectileView : MonoBehaviour
    {
        private Vector3 from;
        private Vector3 fallbackTarget;
        private Transform target;
        private ProjectileView poolPrefab;
        private float duration;
        private float elapsed;
        private bool isPlaying;

        public bool IsPlaying
        {
            get { return isPlaying; }
        }

        internal ProjectileView PoolPrefab
        {
            get { return poolPrefab; }
        }

        internal void SetPoolPrefab(ProjectileView prefab)
        {
            poolPrefab = prefab;
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            Vector3 destination = ResolveDestination();
            transform.position = Vector3.Lerp(from, destination, t);

            Vector3 direction = destination - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            if (t >= 1f)
            {
                isPlaying = false;
            }
        }

        public void Play(Vector3 fromPosition, Transform targetTransform, Vector3 fallbackTargetPosition, float travelDuration)
        {
            from = fromPosition;
            target = targetTransform;
            fallbackTarget = fallbackTargetPosition;
            duration = Mathf.Max(0f, travelDuration);
            elapsed = 0f;
            isPlaying = true;
            gameObject.SetActive(true);
            transform.position = from;
        }

        public void Play(Vector3 fromPosition, Vector3 targetPosition, float travelDuration)
        {
            Play(fromPosition, null, targetPosition, travelDuration);
        }

        public void Release()
        {
            isPlaying = false;
            target = null;
            gameObject.SetActive(false);
        }

        private Vector3 ResolveDestination()
        {
            if (target == null)
            {
                return fallbackTarget;
            }

            Vector3 destination = target.position;
            destination.y = fallbackTarget.y;
            fallbackTarget = destination;
            return destination;
        }
    }
}
