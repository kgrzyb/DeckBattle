using System.Collections.Generic;
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
        [SerializeField] private Image hpDamageFillImage;
        [SerializeField] private RectTransform hpDamageFillTransform;
        [SerializeField, Min(0f)] private float damageFillDelay = 0.15f;
        [SerializeField, Min(0.01f)] private float damageFillDuration = 0.3f;
        [SerializeField] private Image manaFillImage;
        [SerializeField] private RectTransform manaFillTransform;
        [SerializeField] private Image shieldBarImage;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text manaText;
        [SerializeField] private RectTransform statusIconRoot;
        [SerializeField] private GameObject statusIconPrefab;
        [SerializeField, Range(1, 4)] private int maxVisibleStatusIcons = 4;

        private readonly StatusIconSlot[] statusIconSlots = new StatusIconSlot[4];
        private readonly StatusKind[] selectedStatusKinds = new StatusKind[16];
        private readonly StatusPresentationEntry[] selectedStatusEntries = new StatusPresentationEntry[16];
        private int unitId;
        private int shownHp = -1;
        private int shownMaxHp = -1;
        private int shownMana = -1;
        private int shownMaxMana = -1;
        private int shownStatusVersion = -1;
        private int shownTotalShield = -1;
        private float shownDamageFill;
        private float damageFillStart;
        private float damageFillTarget;
        private float damageFillDelayRemaining;
        private float damageFillElapsed;
        private bool damageFillAnimating;
        private Color shownHpFillColor;
        private bool hasShownHpFillColor;

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

        public float DamageFillAnimationDuration
        {
            get { return Mathf.Max(0f, damageFillDelay) + Mathf.Max(0.01f, damageFillDuration); }
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
            ResetDamageFill();

            SetHealth(currentHp, maxHp);
            SetMana(currentMana, maxMana);
            SetStatuses(null, 0);
            SetVisible(target != null && currentHp > 0);
        }

        public void SetStatuses(UnitStatusCollection statuses, int totalShield)
        {
            SetStatuses(statuses, totalShield, null);
        }

        public void SetStatuses(UnitStatusCollection statuses, int totalShield, StatusPresentationCatalog presentationCatalog)
        {
            if (presentationCatalog != null)
            {
                SetCatalogStatuses(statuses, presentationCatalog);
                SetShield(totalShield);
                return;
            }

            int statusCount = statuses != null ? statuses.Count : 0;
            if (statusCount == 0 && statusIconSlots[0] == null)
            {
                shownStatusVersion = 0;
                SetShield(totalShield);
                return;
            }

            if (!EnsureStatusIconSlots())
            {
                SetShield(totalShield);
                return;
            }
            int visibleCount = 0;
            int hiddenCount = 0;
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

        public void SetPresentationStatuses(
            IReadOnlyList<StatusPresentationState> statuses,
            int totalShield,
            StatusPresentationCatalog presentationCatalog)
        {
            if (presentationCatalog != null)
            {
                SetCatalogPresentationStatuses(statuses, presentationCatalog);
                SetShield(totalShield);
                return;
            }

            int statusCount = statuses != null ? statuses.Count : 0;
            if (statusCount == 0 && statusIconSlots[0] == null)
            {
                shownStatusVersion = 0;
                SetShield(totalShield);
                return;
            }

            if (!EnsureStatusIconSlots())
            {
                SetShield(totalShield);
                return;
            }
            int visibleCount = 0;
            int hiddenCount = 0;
            for (int priority = 0; priority <= 4; priority++)
            {
                for (int i = 0; i < statusCount; i++)
                {
                    StatusPresentationState status = statuses[i];
                    if (status.Kind == StatusKind.Shield || GetPriority(status.Kind) != priority)
                    {
                        continue;
                    }

                    if (visibleCount < maxVisibleStatusIcons)
                    {
                        statusIconSlots[visibleCount].Set(status.Kind, status.Stacks, 0);
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

        private void SetCatalogStatuses(UnitStatusCollection statuses, StatusPresentationCatalog presentationCatalog)
        {
            if (!EnsureStatusIconSlots())
            {
                return;
            }
            int selectedCount = 0;
            int statusCount = statuses != null ? statuses.Count : 0;
            for (int i = 0; i < statusCount; i++)
            {
                StatusKind kind = statuses[i].Kind;
                if (!presentationCatalog.TryGet(kind, out StatusPresentationEntry entry) || !entry.ShowsIcon || entry.Icon == null)
                {
                    continue;
                }

                bool alreadySelected = false;
                for (int selectedIndex = 0; selectedIndex < selectedCount; selectedIndex++)
                {
                    if (selectedStatusKinds[selectedIndex] == kind)
                    {
                        alreadySelected = true;
                        break;
                    }
                }

                if (alreadySelected || selectedCount >= selectedStatusKinds.Length)
                {
                    continue;
                }

                int insertIndex = selectedCount;
                while (insertIndex > 0 && ComparePresentation(entry, kind, selectedStatusEntries[insertIndex - 1], selectedStatusKinds[insertIndex - 1]) < 0)
                {
                    selectedStatusKinds[insertIndex] = selectedStatusKinds[insertIndex - 1];
                    selectedStatusEntries[insertIndex] = selectedStatusEntries[insertIndex - 1];
                    insertIndex--;
                }

                selectedStatusKinds[insertIndex] = kind;
                selectedStatusEntries[insertIndex] = entry;
                selectedCount++;
            }

            int visibleCount = Mathf.Min(selectedCount, maxVisibleStatusIcons);
            for (int i = 0; i < visibleCount; i++)
            {
                statusIconSlots[i].SetIcon(selectedStatusEntries[i].Icon);
            }

            for (int i = visibleCount; i < statusIconSlots.Length; i++)
            {
                statusIconSlots[i].SetVisible(false);
            }

            if (selectedCount > maxVisibleStatusIcons && visibleCount > 0)
            {
                statusIconSlots[visibleCount - 1].SetOverflow(selectedCount - maxVisibleStatusIcons + 1);
            }
        }

        private void SetCatalogPresentationStatuses(IReadOnlyList<StatusPresentationState> statuses, StatusPresentationCatalog presentationCatalog)
        {
            if (!EnsureStatusIconSlots())
            {
                return;
            }
            int selectedCount = 0;
            int statusCount = statuses != null ? statuses.Count : 0;
            for (int i = 0; i < statusCount; i++)
            {
                StatusKind kind = statuses[i].Kind;
                if (!presentationCatalog.TryGet(kind, out StatusPresentationEntry entry) || !entry.ShowsIcon || entry.Icon == null)
                {
                    continue;
                }

                bool alreadySelected = false;
                for (int selectedIndex = 0; selectedIndex < selectedCount; selectedIndex++)
                {
                    if (selectedStatusKinds[selectedIndex] == kind)
                    {
                        alreadySelected = true;
                        break;
                    }
                }

                if (alreadySelected || selectedCount >= selectedStatusKinds.Length)
                {
                    continue;
                }

                int insertIndex = selectedCount;
                while (insertIndex > 0 && ComparePresentation(entry, kind, selectedStatusEntries[insertIndex - 1], selectedStatusKinds[insertIndex - 1]) < 0)
                {
                    selectedStatusKinds[insertIndex] = selectedStatusKinds[insertIndex - 1];
                    selectedStatusEntries[insertIndex] = selectedStatusEntries[insertIndex - 1];
                    insertIndex--;
                }

                selectedStatusKinds[insertIndex] = kind;
                selectedStatusEntries[insertIndex] = entry;
                selectedCount++;
            }

            int visibleCount = Mathf.Min(selectedCount, maxVisibleStatusIcons);
            for (int i = 0; i < visibleCount; i++)
            {
                statusIconSlots[i].SetIcon(selectedStatusEntries[i].Icon);
            }

            for (int i = visibleCount; i < statusIconSlots.Length; i++)
            {
                statusIconSlots[i].SetVisible(false);
            }

            if (selectedCount > maxVisibleStatusIcons && visibleCount > 0)
            {
                statusIconSlots[visibleCount - 1].SetOverflow(selectedCount - maxVisibleStatusIcons + 1);
            }
        }

        private static int ComparePresentation(StatusPresentationEntry left, StatusKind leftKind, StatusPresentationEntry right, StatusKind rightKind)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            return priority != 0 ? priority : leftKind.CompareTo(rightKind);
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

            bool hadShownHealth = shownHp >= 0 && shownMaxHp > 0;
            float normalized = (float)clampedHp / maxHp;
            float previousNormalized = hadShownHealth ? (float)shownHp / shownMaxHp : normalized;

            shownHp = clampedHp;
            shownMaxHp = maxHp;
            SetFill(hpFillImage, hpFillTransform, normalized);
            if (!hadShownHealth || normalized >= previousNormalized)
            {
                SyncDamageFill(normalized);
            }
            else
            {
                BeginDamageFill(previousNormalized, normalized);
            }

            SetText(hpText, clampedHp, maxHp);
        }

        public void SetHpFillColor(Color color)
        {
            if (hpFillImage == null || (hasShownHpFillColor && shownHpFillColor == color))
            {
                return;
            }

            hpFillImage.color = color;
            shownHpFillColor = color;
            hasShownHpFillColor = true;
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
            ResetDamageFill();
            SetStatuses(null, 0);
            SetVisible(false);
        }

        private void BeginDamageFill(float previousNormalized, float targetNormalized)
        {
            damageFillStart = Mathf.Max(shownDamageFill, previousNormalized);
            damageFillTarget = Mathf.Clamp01(targetNormalized);
            damageFillDelayRemaining = Mathf.Max(0f, damageFillDelay);
            damageFillElapsed = 0f;
            damageFillAnimating = true;
            SetDamageFill(damageFillStart);
        }

        private void SyncDamageFill(float normalized)
        {
            damageFillAnimating = false;
            damageFillDelayRemaining = 0f;
            damageFillElapsed = 0f;
            damageFillStart = normalized;
            damageFillTarget = normalized;
            SetDamageFill(normalized);
        }

        private void ResetDamageFill()
        {
            damageFillAnimating = false;
            damageFillDelayRemaining = 0f;
            damageFillElapsed = 0f;
            damageFillStart = 0f;
            damageFillTarget = 0f;
            shownDamageFill = 0f;
        }

        public void TickDamageFill(float deltaTime)
        {
            if (!damageFillAnimating)
            {
                return;
            }

            float remainingDeltaTime = Mathf.Max(0f, deltaTime);
            if (damageFillDelayRemaining > 0f)
            {
                if (remainingDeltaTime <= damageFillDelayRemaining)
                {
                    damageFillDelayRemaining -= remainingDeltaTime;
                    return;
                }

                remainingDeltaTime -= damageFillDelayRemaining;
                damageFillDelayRemaining = 0f;
            }

            float duration = Mathf.Max(0.01f, damageFillDuration);
            damageFillElapsed += remainingDeltaTime;
            float progress = Mathf.Clamp01(damageFillElapsed / duration);
            SetDamageFill(Mathf.SmoothStep(damageFillStart, damageFillTarget, progress));
            if (progress >= 1f)
            {
                damageFillAnimating = false;
                SetDamageFill(damageFillTarget);
            }
        }

        private void SetDamageFill(float normalized)
        {
            shownDamageFill = Mathf.Clamp01(normalized);
            SetFill(hpDamageFillImage, hpDamageFillTransform, shownDamageFill);
        }

        private bool EnsureStatusIconSlots()
        {
            if (statusIconSlots[0] != null)
            {
                return true;
            }

            RectTransform root = statusIconRoot != null ? statusIconRoot : RectTransform;
            if (statusIconPrefab == null)
            {
                Debug.LogError($"{nameof(UnitStatusOverlayView)} requires a StatusIcon prefab.", this);
                return false;
            }

            for (int i = 0; i < statusIconSlots.Length; i++)
            {
                GameObject iconObject = Instantiate(statusIconPrefab, root, false);
                Image image = iconObject.GetComponent<Image>();
                TMP_Text label = iconObject.GetComponentInChildren<TMP_Text>(true);
                if (image == null || label == null)
                {
                    Debug.LogError($"StatusIcon prefab must contain an {nameof(Image)} and a child {nameof(TMP_Text)}.", iconObject);
                    Destroy(iconObject);
                    return false;
                }

                statusIconSlots[i] = new StatusIconSlot(iconObject, image, label);
                statusIconSlots[i].SetVisible(false);
            }

            return true;
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

            public void SetIcon(Sprite sprite)
            {
                image.sprite = sprite;
                image.color = Color.white;
                label.SetText(string.Empty);
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

    }
}
