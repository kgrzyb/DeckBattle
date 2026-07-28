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
        [SerializeField] private Image shieldBarImage;
        [SerializeField] private TMP_Text unitNameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text manaText;
        [SerializeField] private RectTransform statusIconRoot;
        [SerializeField, Range(1, 4)] private int maxVisibleStatusIcons = 4;

        private readonly StatusIconSlot[] statusIconSlots = new StatusIconSlot[4];
        private int unitId;
        private int shownHp = -1;
        private int shownMaxHp = -1;
        private int shownMana = -1;
        private int shownMaxMana = -1;
        private int shownStatusVersion = -1;
        private int shownTotalShield = -1;

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

        public void Bind(int nextUnitId, Transform target, string displayName, int currentHp, int maxHp, int currentMana, int maxMana)
        {
            unitId = nextUnitId;
            shownHp = -1;
            shownMaxHp = -1;
            shownMana = -1;
            shownMaxMana = -1;

            SetUnitName(displayName);
            SetHealth(currentHp, maxHp);
            SetMana(currentMana, maxMana);
            SetStatuses(null, 0);
            SetVisible(target != null && currentHp > 0);
        }

        public void SetStatuses(UnitStatusCollection statuses, int totalShield)
        {
            EnsureStatusIconSlots();
            int visibleCount = 0;
            int hiddenCount = 0;
            int statusCount = statuses != null ? statuses.Count : 0;
            for (int priority = 0; priority <= 4; priority++)
            {
                for (int i = 0; i < statusCount; i++)
                {
                    StatusInstance status = statuses[i];
                    if (status.Kind == StatusKind.Shield)
                    {
                        continue;
                    }

                    if (GetPriority(status.Kind) != priority)
                    {
                        continue;
                    }

                    if (visibleCount < maxVisibleStatusIcons)
                    {
                        statusIconSlots[visibleCount].Set(status.Kind, status.Stacks, status.Kind == StatusKind.Shield ? status.RemainingShield : 0);
                        visibleCount++;
                    }
                    else
                    {
                        hiddenCount++;
                    }
                }
            }

            for (int i = visibleCount; i < statusIconSlots.Length; i++)
            {
                statusIconSlots[i].SetVisible(false);
            }

            if (hiddenCount > 0 && visibleCount > 0)
            {
                statusIconSlots[visibleCount - 1].SetOverflow(hiddenCount + 1);
            }

            shownStatusVersion = statusCount;
            SetShield(totalShield);
        }

        public void SetShield(int totalShield)
        {
            int clampedShield = Mathf.Max(0, totalShield);
            if (shownTotalShield == clampedShield)
            {
                return;
            }

            shownTotalShield = clampedShield;
            EnsureShieldBar();
            if (shieldBarImage != null)
            {
                shieldBarImage.gameObject.SetActive(clampedShield > 0);
            }
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
            shownStatusVersion = -1;
            shownTotalShield = -1;
            SetStatuses(null, 0);
            SetVisible(false);
        }

        private void EnsureStatusIconSlots()
        {
            if (statusIconSlots[0] != null)
            {
                return;
            }

            RectTransform root = statusIconRoot != null ? statusIconRoot : RectTransform;
            for (int i = 0; i < statusIconSlots.Length; i++)
            {
                var iconObject = new GameObject("StatusIcon" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform iconTransform = iconObject.GetComponent<RectTransform>();
                iconTransform.SetParent(root, false);
                iconTransform.anchorMin = new Vector2(0.5f, 0.5f);
                iconTransform.anchorMax = new Vector2(0.5f, 0.5f);
                iconTransform.pivot = new Vector2(0.5f, 0.5f);
                iconTransform.sizeDelta = new Vector2(15f, 15f);
                iconTransform.anchoredPosition = new Vector2(-27f + (18f * i), -26f);
                Image image = iconObject.GetComponent<Image>();
                image.raycastTarget = false;

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                RectTransform labelTransform = labelObject.GetComponent<RectTransform>();
                labelTransform.SetParent(iconTransform, false);
                labelTransform.anchorMin = Vector2.zero;
                labelTransform.anchorMax = Vector2.one;
                labelTransform.offsetMin = Vector2.zero;
                labelTransform.offsetMax = Vector2.zero;
                TMP_Text label = labelObject.GetComponent<TMP_Text>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 8f;
                label.raycastTarget = false;
                statusIconSlots[i] = new StatusIconSlot(iconObject, image, label);
                statusIconSlots[i].SetVisible(false);
            }
        }

        private void EnsureShieldBar()
        {
            if (shieldBarImage != null)
            {
                return;
            }

            RectTransform hpBackground = hpFillTransform != null ? hpFillTransform.parent as RectTransform : null;
            RectTransform root = hpBackground != null ? hpBackground : RectTransform;
            var shieldObject = new GameObject("ShieldBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform shieldTransform = shieldObject.GetComponent<RectTransform>();
            shieldTransform.SetParent(root, false);
            shieldTransform.anchorMin = new Vector2(0.25f, 1f);
            shieldTransform.anchorMax = new Vector2(0.75f, 1f);
            shieldTransform.pivot = new Vector2(0.5f, 0f);
            shieldTransform.offsetMin = new Vector2(0f, 1f);
            shieldTransform.offsetMax = new Vector2(0f, 3f);
            shieldBarImage = shieldObject.GetComponent<Image>();
            shieldBarImage.color = Color.white;
            shieldBarImage.raycastTarget = false;
            shieldObject.SetActive(false);
        }

        private static int GetPriority(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Stun:
                case StatusKind.Sleep:
                case StatusKind.Root:
                case StatusKind.Silence: return 0;
                case StatusKind.Invulnerability: return 1;
                case StatusKind.Mark:
                case StatusKind.Taunt:
                case StatusKind.Untargetable:
                case StatusKind.Guard: return 2;
                case StatusKind.Burn:
                case StatusKind.Poison:
                case StatusKind.Bleed: return 3;
                default: return 4;
            }
        }

        private sealed class StatusIconSlot
        {
            private readonly GameObject gameObject;
            private readonly Image image;
            private readonly TMP_Text label;

            public StatusIconSlot(GameObject gameObject, Image image, TMP_Text label)
            {
                this.gameObject = gameObject;
                this.image = image;
                this.label = label;
            }

            public void Set(StatusKind kind, int stacks, int shield)
            {
                image.color = GetColor(kind);
                if (kind == StatusKind.Shield && shield > 0)
                {
                    label.SetText("S{0}", shield);
                }
                else if (kind == StatusKind.Bleed && stacks > 1)
                {
                    label.SetText("B{0}", stacks);
                }
                else
                {
                    label.SetText(GetAbbreviation(kind));
                }
                SetVisible(true);
            }

            public void SetOverflow(int count)
            {
                image.color = Color.gray;
                label.SetText("+{0}", count);
            }

            public void SetVisible(bool visible)
            {
                if (gameObject.activeSelf != visible)
                {
                    gameObject.SetActive(visible);
                }
            }

            private static Color GetColor(StatusKind kind)
            {
                switch (kind)
                {
                    case StatusKind.Stun:
                    case StatusKind.Sleep:
                    case StatusKind.Root:
                    case StatusKind.Silence: return new Color(0.85f, 0.45f, 0.2f);
                    case StatusKind.Burn:
                    case StatusKind.Poison:
                    case StatusKind.Bleed: return new Color(0.8f, 0.2f, 0.2f);
                    case StatusKind.Invulnerability:
                    case StatusKind.Shield: return new Color(0.25f, 0.65f, 1f);
                    default: return new Color(0.5f, 0.75f, 0.4f);
                }
            }

            private static string GetAbbreviation(StatusKind kind)
            {
                switch (kind)
                {
                    case StatusKind.Stun: return "S";
                    case StatusKind.Slow: return "L";
                    case StatusKind.Sleep: return "Z";
                    case StatusKind.Root: return "R";
                    case StatusKind.Silence: return "!";
                    case StatusKind.Burn: return "B";
                    case StatusKind.Poison: return "P";
                    case StatusKind.Bleed: return "B";
                    case StatusKind.Weaken: return "W";
                    case StatusKind.Exposed: return "E";
                    case StatusKind.Shred: return "X";
                    case StatusKind.Shield: return "S";
                    case StatusKind.Regen: return "+";
                    case StatusKind.Invulnerability: return "I";
                    case StatusKind.Empower: return "E";
                    case StatusKind.Haste: return "H";
                    case StatusKind.Criticality: return "C";
                    case StatusKind.Fearless: return "F";
                    case StatusKind.Lifesteal: return "V";
                    case StatusKind.Mark: return "M";
                    case StatusKind.Taunt: return "T";
                    case StatusKind.Untargetable: return "U";
                    case StatusKind.Guard: return "G";
                    default: return "?";
                }
            }
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

        private void SetUnitName(string displayName)
        {
            if (unitNameText == null)
            {
                return;
            }

            unitNameText.SetText(string.IsNullOrEmpty(displayName) ? "Unit" : displayName);
        }
    }
}
