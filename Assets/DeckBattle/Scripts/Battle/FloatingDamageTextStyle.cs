using System;
using UnityEngine;

namespace DeckBattle
{
    [Serializable]
    public struct FloatingDamageTextStyle
    {
        [SerializeField] private FloatingDamageTextType type;
        [SerializeField] private Color color;
        [SerializeField, Min(0.01f)] private float duration;
        [SerializeField, Min(0f)] private float risePixels;
        [SerializeField] private AnimationCurve verticalMotionCurve;
        [SerializeField, Min(0f)] private float horizontalDriftPixels;
        [SerializeField] private AnimationCurve scaleCurve;
        [SerializeField, Min(0.01f)] private float fontSize;
        [SerializeField, Range(0f, 1f)] private float fadeStartNormalized;

        public FloatingDamageTextType Type { get { return type; } }
        public Color Color { get { return color; } }
        public float Duration { get { return duration; } }
        public float RisePixels { get { return risePixels; } }
        public float HorizontalDriftPixels { get { return horizontalDriftPixels; } }
        public float FontSize { get { return fontSize; } }
        public float FadeStartNormalized { get { return fadeStartNormalized; } }
        public bool IsValid
        {
            get
            {
                return duration > 0f
                    && verticalMotionCurve != null
                    && verticalMotionCurve.length > 0
                    && scaleCurve != null
                    && scaleCurve.length > 0
                    && fontSize > 0f;
            }
        }

        public FloatingDamageTextStyle(
            FloatingDamageTextType type,
            Color color,
            float duration,
            float risePixels,
            AnimationCurve verticalMotionCurve,
            AnimationCurve scaleCurve,
            float fontSize,
            float fadeStartNormalized,
            float horizontalDriftPixels = 24f)
        {
            this.type = type;
            this.color = color;
            this.duration = Mathf.Max(0.01f, duration);
            this.risePixels = Mathf.Max(0f, risePixels);
            this.verticalMotionCurve = verticalMotionCurve ?? CreateDefaultVerticalMotionCurve();
            this.horizontalDriftPixels = Mathf.Max(0f, horizontalDriftPixels);
            this.scaleCurve = scaleCurve ?? AnimationCurve.Constant(0f, 1f, 1f);
            this.fontSize = Mathf.Max(0.01f, fontSize);
            this.fadeStartNormalized = Mathf.Clamp01(fadeStartNormalized);
        }

        public static FloatingDamageTextStyle Default(FloatingDamageTextType type)
        {
            switch (type)
            {
                case FloatingDamageTextType.Critical:
                    return new FloatingDamageTextStyle(
                        FloatingDamageTextType.Critical,
                        new Color(1f, 0.76f, 0.2f, 1f),
                        0.8f,
                        40f,
                        CreateDefaultVerticalMotionCurve(),
                        AnimationCurve.Linear(0f, 1.3f, 1f, 1.1f),
                        46f,
                        0.6f,
                        34f);
                default:
                    return new FloatingDamageTextStyle(
                        FloatingDamageTextType.Normal,
                        new Color(1f, 0.35f, 0.3f, 1f),
                        0.65f,
                        40f,
                        CreateDefaultVerticalMotionCurve(),
                        AnimationCurve.Linear(0f, 0.9f, 1f, 1f),
                        36f,
                        0.55f,
                        26f);
            }
        }

        public float EvaluateScale(float normalized)
        {
            return scaleCurve != null && scaleCurve.length > 0
                ? Mathf.Max(0f, scaleCurve.Evaluate(Mathf.Clamp01(normalized)))
                : 1f;
        }

        public float EvaluateVerticalOffset(float normalized)
        {
            float verticalNormalized = verticalMotionCurve != null && verticalMotionCurve.length > 0
                ? Mathf.Clamp01(verticalMotionCurve.Evaluate(Mathf.Clamp01(normalized)))
                : 0f;
            return risePixels * verticalNormalized;
        }

        private static AnimationCurve CreateDefaultVerticalMotionCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.45f, 1f),
                new Keyframe(1f, 0f));
        }
    }
}
