using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class FloatingDamageTextController : MonoBehaviour
    {
        private static readonly Vector2[] HorizontalOffsets =
        {
            Vector2.zero,
            new Vector2(-18f, 0f),
            new Vector2(18f, 0f),
            new Vector2(-32f, 0f),
            new Vector2(32f, 0f)
        };

        [SerializeField] private FloatingDamageTextView textPrefab;
        [SerializeField] private RectTransform textRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField, Min(0)] private int prewarmCount = 16;
        [SerializeField, Min(1)] private int maxActive = 32;

        private readonly List<FloatingDamageTextView> activeTexts = new List<FloatingDamageTextView>(32);
        private readonly Stack<FloatingDamageTextView> pooledTexts = new Stack<FloatingDamageTextView>(32);

        private RectTransform cachedRoot;
        private Canvas rootCanvas;
        private float combatSpeed = 1f;
        private int nextHorizontalOffsetIndex;
        private uint visualRandomState;
        private bool missingPrefabLogged;

        public int ActiveCount { get { return activeTexts.Count; } }
        public int PooledCount { get { return pooledTexts.Count; } }

        private void Awake()
        {
            visualRandomState = unchecked((uint)GetInstanceID());
            if (visualRandomState == 0u)
            {
                visualRandomState = 0x6D2B79F5u;
            }

            Prewarm();
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime * combatSpeed;
            for (int i = activeTexts.Count - 1; i >= 0; i--)
            {
                FloatingDamageTextView text = activeTexts[i];
                if (text != null && !text.Tick(deltaTime))
                {
                    continue;
                }

                activeTexts.RemoveAt(i);
                Pool(text);
            }
        }

        private void OnDisable()
        {
            ReleaseAll();
        }

        public void SetCombatSpeed(float speed)
        {
            combatSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
        }

        public void Show(Vector3 worldPosition, int amount, FloatingDamageTextType type)
        {
            if (amount <= 0 || !TryGetAnchoredPosition(worldPosition, out Vector2 anchoredPosition))
            {
                return;
            }

            FloatingDamageTextView text = GetText();
            if (text == null)
            {
                return;
            }

            RectTransform root = ResolveRoot();
            text.transform.SetParent(root, false);
            text.Play(
                amount,
                anchoredPosition + GetNextHorizontalOffset(),
                type,
                GetNextHorizontalDirection());
            activeTexts.Add(text);
        }

        public void ReleaseAll()
        {
            for (int i = activeTexts.Count - 1; i >= 0; i--)
            {
                Pool(activeTexts[i]);
            }

            activeTexts.Clear();
            nextHorizontalOffsetIndex = 0;
        }

        private void Prewarm()
        {
            int capacity = Mathf.Max(1, maxActive);
            int targetCount = Mathf.Min(Mathf.Max(0, prewarmCount), capacity);
            while (pooledTexts.Count < targetCount)
            {
                FloatingDamageTextView text = CreateText();
                if (text == null)
                {
                    return;
                }

                Pool(text);
            }
        }

        private FloatingDamageTextView GetText()
        {
            int capacity = Mathf.Max(1, maxActive);
            if (activeTexts.Count >= capacity)
            {
                FloatingDamageTextView oldest = activeTexts[0];
                activeTexts.RemoveAt(0);
                return oldest;
            }

            if (pooledTexts.Count > 0)
            {
                return pooledTexts.Pop();
            }

            return activeTexts.Count + pooledTexts.Count < capacity ? CreateText() : null;
        }

        private FloatingDamageTextView CreateText()
        {
            RectTransform root = ResolveRoot();
            if (root == null)
            {
                return null;
            }

            if (textPrefab == null)
            {
                if (!missingPrefabLogged)
                {
                    Debug.LogError("FloatingDamageTextController is missing its FloatingDamageTextView prefab.", this);
                    missingPrefabLogged = true;
                }

                return null;
            }

            FloatingDamageTextView text = Instantiate(textPrefab, root);
            text.Release();
            return text;
        }

        private void Pool(FloatingDamageTextView text)
        {
            if (text == null)
            {
                return;
            }

            RectTransform root = ResolveRoot();
            if (root != null)
            {
                text.transform.SetParent(root, false);
            }

            text.Release();
            pooledTexts.Push(text);
        }

        private bool TryGetAnchoredPosition(Vector3 worldPosition, out Vector2 anchoredPosition)
        {
            anchoredPosition = default;
            RectTransform root = ResolveRoot();
            Camera camera = ResolveWorldCamera();
            if (root == null || camera == null)
            {
                return false;
            }

            Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition + worldOffset);
            if (screenPosition.z <= 0f)
            {
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root,
                screenPosition,
                ResolveUiCamera(root),
                out anchoredPosition);
        }

        private Vector2 GetNextHorizontalOffset()
        {
            Vector2 offset = HorizontalOffsets[nextHorizontalOffsetIndex];
            nextHorizontalOffsetIndex = (nextHorizontalOffsetIndex + 1) % HorizontalOffsets.Length;
            return offset;
        }

        private float GetNextHorizontalDirection()
        {
            visualRandomState ^= visualRandomState << 13;
            visualRandomState ^= visualRandomState >> 17;
            visualRandomState ^= visualRandomState << 5;
            return (visualRandomState & 1u) == 0u ? -1f : 1f;
        }

        private RectTransform ResolveRoot()
        {
            if (textRoot != null)
            {
                return textRoot;
            }

            if (cachedRoot == null)
            {
                cachedRoot = transform as RectTransform;
            }

            return cachedRoot;
        }

        private Camera ResolveWorldCamera()
        {
            if (worldCamera != null && worldCamera.isActiveAndEnabled)
            {
                return worldCamera;
            }

            worldCamera = Camera.main;
            return worldCamera;
        }

        private Camera ResolveUiCamera(RectTransform root)
        {
            if (rootCanvas == null)
            {
                rootCanvas = root.GetComponentInParent<Canvas>();
            }

            return rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
        }
    }
}
