using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class UnitStatusOverlayView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private RectTransform hpFillTransform;
        [SerializeField] private Image manaFillImage;
        [SerializeField] private RectTransform manaFillTransform;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text manaText;

        private int unitId;
        private int shownHp = -1;
        private int shownMaxHp = -1;
        private int shownMana = -1;
        private int shownMaxMana = -1;

        public int UnitId
        {
            get { return unitId; }
        }

        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = transform as RectTransform;
                }

                return rectTransform;
            }
        }

        private void Awake()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }
        }

        public void Bind(int nextUnitId, Transform target, int currentHp, int maxHp, int currentMana, int maxMana)
        {
            unitId = nextUnitId;
            shownHp = -1;
            shownMaxHp = -1;
            shownMana = -1;
            shownMaxMana = -1;

            SetHealth(currentHp, maxHp);
            SetMana(currentMana, maxMana);
            SetVisible(target != null && currentHp > 0);
        }

        public void SetHealth(int currentHp, int maximumHp)
        {
            int maxHp = Mathf.Max(1, maximumHp);
            int clampedHp = Mathf.Clamp(currentHp, 0, maxHp);
            if (shownHp == clampedHp && shownMaxHp == maxHp)
            {
                return;
            }

            shownHp = clampedHp;
            shownMaxHp = maxHp;
            SetFill(hpFillImage, hpFillTransform, (float)clampedHp / maxHp);
            SetText(hpText, clampedHp, maxHp);
            SetVisible(clampedHp > 0);
        }

        public void SetMana(int currentMana, int maximumMana)
        {
            int maxMana = Mathf.Max(1, maximumMana);
            int clampedMana = Mathf.Clamp(currentMana, 0, maxMana);
            if (shownMana == clampedMana && shownMaxMana == maxMana)
            {
                return;
            }

            shownMana = clampedMana;
            shownMaxMana = maxMana;
            SetFill(manaFillImage, manaFillTransform, (float)clampedMana / maxMana);
            SetText(manaText, clampedMana, maxMana);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        public void Release()
        {
            unitId = 0;
            shownHp = -1;
            shownMaxHp = -1;
            shownMana = -1;
            shownMaxMana = -1;
            SetVisible(false);
        }

        private static void SetFill(Image image, RectTransform fillTransform, float normalized)
        {
            float clamped = Mathf.Clamp01(normalized);
            if (image != null)
            {
                image.fillAmount = clamped;
                if (fillTransform == null)
                {
                    fillTransform = image.rectTransform;
                }
            }

            if (fillTransform == null)
            {
                return;
            }

            Vector3 scale = fillTransform.localScale;
            scale.x = clamped;
            fillTransform.localScale = scale;
        }

        private static void SetText(TMP_Text text, int current, int maximum)
        {
            if (text == null)
            {
                return;
            }

            text.SetText("{0}/{1}", current, maximum);
        }
    }
}
