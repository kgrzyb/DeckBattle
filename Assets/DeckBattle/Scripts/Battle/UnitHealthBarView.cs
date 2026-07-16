using UnityEngine;
using UnityEngine.UI;

namespace DeckBattle
{
    public sealed class UnitHealthBarView : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Transform fillTransform;
        [SerializeField] private bool faceToCamera = true;

        private static Camera cachedCamera;

        private Vector3 baseScale;
        private int shownHp = -1;
        private int shownMaxHp = -1;
        private bool initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            FaceToCamera();
        }

        public void SetHealth(int currentHp, int maximumHp)
        {
            EnsureInitialized();

            int maxHp = Mathf.Max(1, maximumHp);
            int clampedHp = Mathf.Clamp(currentHp, 0, maxHp);
            if (shownHp == clampedHp && shownMaxHp == maxHp)
            {
                return;
            }

            shownHp = clampedHp;
            shownMaxHp = maxHp;
            float normalized = (float)clampedHp / maxHp;
            if (fillImage != null)
            {
                fillImage.fillAmount = normalized;
            }

            if (fillTransform != null)
            {
                Vector3 scale = fillTransform.localScale;
                scale.x = normalized;
                fillTransform.localScale = scale;
            }

            gameObject.SetActive(clampedHp > 0 && clampedHp < maxHp);
        }

        public void ResetScale()
        {
            EnsureInitialized();
            transform.localScale = baseScale;
        }

        public void CompensateParentScale(float parentScale)
        {
            EnsureInitialized();

            if (parentScale <= 0.001f)
            {
                return;
            }

            transform.localScale = baseScale / parentScale;
        }

        private void FaceToCamera()
        {
            if (!faceToCamera || !gameObject.activeSelf)
            {
                return;
            }

            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
            {
                cachedCamera = Camera.main;
                if (cachedCamera == null)
                {
                    return;
                }
            }

            transform.rotation = cachedCamera.transform.rotation;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            baseScale = transform.localScale;
            initialized = true;
        }
    }
}
