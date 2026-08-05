using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class FloatingDamageTextViewTests
    {
        [Test]
        public void PlayAndTick_AppliesNumberStyleMotionAndFade()
        {
            GameObject root = CreateView(out FloatingDamageTextView view, out TextMeshProUGUI text);
            try
            {
                FloatingDamageTextStyle style = new FloatingDamageTextStyle(
                    FloatingDamageTextType.Critical,
                    new Color(1f, 0.8f, 0.2f, 1f),
                    1f,
                    40f,
                    new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.5f, 1f),
                        new Keyframe(1f, 0f)),
                    new AnimationCurve(
                        new Keyframe(0f, 1.4f),
                        new Keyframe(0.5f, 2f),
                        new Keyframe(1f, 0.8f)),
                    42f,
                    0.5f);

                view.Play(25, new Vector2(10f, 20f), style, -1f);

                Assert.IsTrue(view.IsPlaying);
                Assert.AreEqual("25", text.text);
                Assert.AreEqual(42f, text.fontSize);
                Assert.AreEqual(new Vector2(10f, 20f), root.GetComponent<RectTransform>().anchoredPosition);
                Assert.AreEqual(1.4f, root.transform.localScale.x, 0.0001f);

                Assert.IsFalse(view.Tick(0.5f));
                Vector2 peakPosition = root.GetComponent<RectTransform>().anchoredPosition;
                Assert.Greater(peakPosition.y, 20f);
                Assert.AreEqual(60f, peakPosition.y, 0.0001f);
                Assert.Less(peakPosition.x, 10f);
                Assert.AreEqual(2f, root.transform.localScale.x, 0.0001f);

                Assert.IsFalse(view.Tick(0.25f));
                Vector2 descendingPosition = root.GetComponent<RectTransform>().anchoredPosition;
                Assert.Less(descendingPosition.y, peakPosition.y);
                Assert.Less(text.color.a, 1f);

                Assert.IsTrue(view.Tick(0.25f));
                Assert.IsFalse(view.IsPlaying);
                Assert.AreEqual(20f, root.GetComponent<RectTransform>().anchoredPosition.y, 0.0001f);
                Assert.AreEqual(0.8f, root.transform.localScale.x, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Release_ResetsViewForPooling()
        {
            GameObject root = CreateView(out FloatingDamageTextView view, out TextMeshProUGUI text);
            try
            {
                view.Play(10, new Vector2(5f, 7f), FloatingDamageTextStyle.Default(FloatingDamageTextType.Normal));

                view.Release();

                Assert.IsFalse(root.activeSelf);
                Assert.IsFalse(view.IsPlaying);
                Assert.AreEqual(string.Empty, text.text);
                Assert.AreEqual(Vector2.zero, root.GetComponent<RectTransform>().anchoredPosition);
                Assert.AreEqual(1f, root.transform.localScale.x, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateView(out FloatingDamageTextView view, out TextMeshProUGUI text)
        {
            GameObject root = new GameObject("FloatingDamageText", typeof(RectTransform), typeof(FloatingDamageTextView));
            GameObject textObject = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            view = root.GetComponent<FloatingDamageTextView>();
            return root;
        }
    }
}
