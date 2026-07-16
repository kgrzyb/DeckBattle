using UnityEngine;

namespace DeckBattle
{
    public sealed class PooledBattleEffect : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private float duration = 0.18f;
        [SerializeField] private float startScale = 0.25f;
        [SerializeField] private float endScale = 0.8f;
        [SerializeField] private Color color = new Color(1f, 0.9f, 0.35f, 0.7f);

        private MaterialPropertyBlock propertyBlock;
        private float remaining;
        private float durationReciprocal;

        public bool IsPlaying
        {
            get { return remaining > 0f; }
        }

        private void Awake()
        {
            if (meshRenderer == null)
            {
                meshRenderer = GetComponentInChildren<MeshRenderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            ApplyColor();
        }

        private void Update()
        {
            if (remaining <= 0f)
            {
                return;
            }

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            float normalized = 1f - remaining * durationReciprocal;
            float scale = Mathf.Lerp(startScale, endScale, normalized);
            transform.localScale = new Vector3(scale, scale, scale);
        }

        public void Play(Vector3 position)
        {
            gameObject.SetActive(true);
            transform.position = position;
            remaining = Mathf.Max(0.01f, duration);
            durationReciprocal = 1f / remaining;
            transform.localScale = new Vector3(startScale, startScale, startScale);
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (meshRenderer == null)
            {
                return;
            }

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
