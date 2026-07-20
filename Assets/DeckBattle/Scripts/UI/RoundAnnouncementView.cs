using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace DeckBattle
{
    public sealed class RoundAnnouncementView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Round Start")]
        [SerializeField] private float roundStartFadeInSeconds = 0.18f;
        [SerializeField] private float roundStartHoldSeconds = 0.7f;
        [SerializeField] private float roundStartFadeOutSeconds = 0.18f;

        [Header("Round Result")]
        [SerializeField] private float roundResultFadeInSeconds = 0.16f;
        [SerializeField] private float roundResultHoldSeconds = 0.85f;
        [SerializeField] private float roundResultFadeOutSeconds = 0.18f;

        [Header("Motion")]
        [SerializeField] private float startScale = 0.88f;
        [SerializeField] private float shownScale = 1f;
        [SerializeField] private float endScale = 1.04f;
        [SerializeField] private Ease showEase = Ease.OutBack;
        [SerializeField] private Ease hideEase = Ease.InQuad;

        private Sequence activeSequence;

        private void Awake()
        {
            ResolveReferences();
            HideImmediate();
        }

        private void OnValidate()
        {
            roundStartFadeInSeconds = Mathf.Max(0f, roundStartFadeInSeconds);
            roundStartHoldSeconds = Mathf.Max(0f, roundStartHoldSeconds);
            roundStartFadeOutSeconds = Mathf.Max(0f, roundStartFadeOutSeconds);
            roundResultFadeInSeconds = Mathf.Max(0f, roundResultFadeInSeconds);
            roundResultHoldSeconds = Mathf.Max(0f, roundResultHoldSeconds);
            roundResultFadeOutSeconds = Mathf.Max(0f, roundResultFadeOutSeconds);
            startScale = Mathf.Max(0.01f, startScale);
            shownScale = Mathf.Max(0.01f, shownScale);
            endScale = Mathf.Max(0.01f, endScale);
        }

        private void OnDisable()
        {
            KillActiveSequence();
        }

        public IEnumerator PlayRoundStart(int roundNumber)
        {
            yield return PlayMessage(
                "Round " + Mathf.Max(1, roundNumber),
                roundStartFadeInSeconds,
                roundStartHoldSeconds,
                roundStartFadeOutSeconds);
        }

        public IEnumerator PlayRoundResult(RoundResolutionResult result)
        {
            if (result == null)
            {
                yield break;
            }

            yield return PlayMessage(
                FormatRoundResult(result),
                roundResultFadeInSeconds,
                roundResultHoldSeconds,
                roundResultFadeOutSeconds);
        }

        public void HideImmediate()
        {
            KillActiveSequence();
            ResolveReferences();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (contentRoot != null)
            {
                contentRoot.localScale = Vector3.one * shownScale;
            }

            gameObject.SetActive(false);
        }

        public static string FormatRoundResult(RoundResolutionResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            if (result.PlayerDamageDealt == result.EnemyDamageDealt)
            {
                return "Draw";
            }

            return result.PlayerDamageDealt > result.EnemyDamageDealt
                ? "Round Won"
                : "Round Lost";
        }

        private IEnumerator PlayMessage(string message, float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
        {
            ResolveReferences();
            if (canvasGroup == null || contentRoot == null || messageText == null)
            {
                yield break;
            }

            KillActiveSequence();
            gameObject.SetActive(true);
            messageText.text = message;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            contentRoot.localScale = Vector3.one * startScale;

            bool finished = false;
            activeSequence = DOTween.Sequence()
                .SetUpdate(false)
                .Append(DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 1f, fadeInSeconds))
                .Join(contentRoot.DOScale(shownScale, fadeInSeconds).SetEase(showEase))
                .AppendInterval(holdSeconds)
                .Append(DOTween.To(() => canvasGroup.alpha, value => canvasGroup.alpha = value, 0f, fadeOutSeconds))
                .Join(contentRoot.DOScale(endScale, fadeOutSeconds).SetEase(hideEase))
                .OnComplete(() => finished = true)
                .OnKill(() => finished = true);

            while (!finished)
            {
                yield return null;
            }

            if (activeSequence != null && !activeSequence.IsActive())
            {
                activeSequence = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            if (contentRoot != null)
            {
                contentRoot.localScale = Vector3.one * shownScale;
            }

            gameObject.SetActive(false);
        }

        private void ResolveReferences()
        {
            if (contentRoot == null)
            {
                contentRoot = GetComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (messageText == null)
            {
                messageText = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void KillActiveSequence()
        {
            if (activeSequence == null)
            {
                return;
            }

            activeSequence.Kill();
            activeSequence = null;
        }
    }
}
