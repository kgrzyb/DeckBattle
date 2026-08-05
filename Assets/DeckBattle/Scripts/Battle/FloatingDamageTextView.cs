using TMPro;
using UnityEngine;

namespace DeckBattle
{
    public sealed class FloatingDamageTextView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private FloatingDamageTextStyle[] styles;

        private FloatingDamageTextStyle activeStyle;
        private Vector2 startPosition;
        private float horizontalDirection;
        private float elapsed;
        private float durationReciprocal;
        private bool isPlaying;

        public bool IsPlaying { get { return isPlaying; } }

        public void Play(int amount, Vector2 anchoredPosition, FloatingDamageTextType type)
        {
            Play(amount, anchoredPosition, ResolveStyle(type), 1f);
        }

        public void Play(int amount, Vector2 anchoredPosition, FloatingDamageTextStyle style)
        {
            Play(amount, anchoredPosition, style, 1f);
        }

        public void Play(
            int amount,
            Vector2 anchoredPosition,
            FloatingDamageTextType type,
            float horizontalDirection)
        {
            Play(amount, anchoredPosition, ResolveStyle(type), horizontalDirection);
        }

        public void Play(
            int amount,
            Vector2 anchoredPosition,
            FloatingDamageTextStyle style,
            float horizontalDirection)
        {
            ResolveReferences();
            activeStyle = style;
            startPosition = anchoredPosition;
            this.horizontalDirection = horizontalDirection < 0f ? -1f : 1f;
            elapsed = 0f;
            durationReciprocal = 1f / Mathf.Max(0.01f, activeStyle.Duration);
            isPlaying = true;

            gameObject.SetActive(true);
            rectTransform.anchoredPosition = startPosition;
            rectTransform.localScale = Vector3.one * activeStyle.EvaluateScale(0f);
            if (valueText != null)
            {
                valueText.SetText("{0}", amount);
                valueText.fontSize = activeStyle.FontSize;
                valueText.color = activeStyle.Color;
            }
        }

        public bool Tick(float deltaTime)
        {
            if (!isPlaying)
            {
                return true;
            }

            elapsed = Mathf.Min(activeStyle.Duration, elapsed + Mathf.Max(0f, deltaTime));
            float normalized = Mathf.Clamp01(elapsed * durationReciprocal);
            rectTransform.anchoredPosition = startPosition + CalculateOffset(normalized);
            float scale = activeStyle.EvaluateScale(normalized);
            rectTransform.localScale = Vector3.one * scale;
            ApplyFade(normalized);

            isPlaying = normalized < 1f;
            return !isPlaying;
        }

        public void Release()
        {
            isPlaying = false;
            elapsed = 0f;
            durationReciprocal = 0f;
            horizontalDirection = 0f;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }

            if (valueText != null)
            {
                valueText.SetText(string.Empty);
                valueText.color = Color.white;
            }

            gameObject.SetActive(false);
        }

        private void ApplyFade(float normalized)
        {
            if (valueText == null)
            {
                return;
            }

            float fadeStart = activeStyle.FadeStartNormalized;
            float alpha = fadeStart >= 1f
                ? 1f
                : 1f - Mathf.InverseLerp(fadeStart, 1f, normalized);
            Color color = activeStyle.Color;
            color.a *= Mathf.Clamp01(alpha);
            valueText.color = color;
        }

        private Vector2 CalculateOffset(float normalized)
        {
            float horizontal = horizontalDirection
                * activeStyle.HorizontalDriftPixels
                * Mathf.Sin(normalized * Mathf.PI * 0.5f);
            float vertical = activeStyle.EvaluateVerticalOffset(normalized);
            return new Vector2(horizontal, vertical);
        }

        private FloatingDamageTextStyle ResolveStyle(FloatingDamageTextType type)
        {
            if (styles != null)
            {
                for (int i = 0; i < styles.Length; i++)
                {
                    if (styles[i].Type == type && styles[i].IsValid)
                    {
                        return styles[i];
                    }
                }
            }

            return FloatingDamageTextStyle.Default(type);
        }

        private void ResolveReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (valueText == null)
            {
                valueText = GetComponentInChildren<TMP_Text>(true);
            }
        }
    }
}
