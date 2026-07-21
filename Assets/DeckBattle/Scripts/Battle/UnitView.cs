using DG.Tweening;
using UnityEngine;

namespace DeckBattle
{
    public sealed class UnitView : MonoBehaviour
    {
        private const int MaxQueuedMoves = 4;

        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private float groundOffset = 0.65f;
        [SerializeField] private float attackPulseDuration = 0.14f;
        [SerializeField] private float meleeAttackDuration = 0.3f;
        [SerializeField] private float meleeAttackLeanBackAngle = -7f;
        [SerializeField] private float meleeAttackStrikeAngle = 14f;
        [SerializeField] private float damageFlashDuration = 0.12f;
        [SerializeField] private float deathDuration = 0.25f;
        [SerializeField] private float deathSinkDistance = 0.25f;
        [SerializeField] private Color playerColor = new Color(0.18f, 0.62f, 0.95f, 1f);
        [SerializeField] private Color enemyColor = new Color(0.95f, 0.35f, 0.25f, 1f);
        [SerializeField] private Color damageFlashColor = Color.white;

        public int RuntimeId { get; private set; }
        public RuntimeUnit Unit { get; private set; }
        public UnitRuntimeState RealtimeUnit { get; private set; }

        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseModelScale;
        private Quaternion baseModelRotation;
        private Vector3 moveFrom;
        private Vector3 moveTo;
        private readonly Vector3[] queuedMoveTargets = new Vector3[MaxQueuedMoves];
        private readonly float[] queuedMoveDurations = new float[MaxQueuedMoves];
        private Vector3 deathStartPosition;
        private Color sideColor;
        private float moveElapsed;
        private float moveDuration;
        private float attackTimer;
        private float damageTimer;
        private float deathTimer;
        private int queuedMoveHead;
        private int queuedMoveCount;
        private bool isMoving;
        private bool isDying;
        private Sequence meleeAttackSequence;

        private void Awake()
        {
            if (modelRoot == null)
            {
                modelRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            if (meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                meshRenderer = modelRoot.GetComponentInChildren<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = GetComponentInChildren<MeshRenderer>();
                }
            }

            baseModelScale = modelRoot.localScale;
            baseModelRotation = modelRoot.localRotation;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            UpdateMovement(deltaTime);
            UpdateVisualTimers(deltaTime);
        }

        private void OnDisable()
        {
            KillMeleeAttackSequence();
        }

        private void OnDestroy()
        {
            KillMeleeAttackSequence();
        }

        public void Bind(RuntimeUnit unit, Vector3 worldPosition)
        {
            Unit = unit;
            RealtimeUnit = null;
            RuntimeId = unit.RuntimeId;
            ResetTransientState(worldPosition);
            name = FormatUnitName(unit.Side, unit.RuntimeId, unit.Definition);
            ApplySideColor(unit.Side);
        }

        public void Bind(UnitRuntimeState unit, Vector3 worldPosition)
        {
            Unit = null;
            RealtimeUnit = unit;
            RuntimeId = unit.UnitId;
            ResetTransientState(worldPosition);
            name = FormatUnitName(unit.Side, unit.UnitId, unit.Definition);
            ApplySideColor(unit.Side);
        }

        public void SetWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition + Vector3.up * groundOffset;
            moveFrom = transform.position;
            moveTo = transform.position;
            moveElapsed = 0f;
            moveDuration = 0f;
            queuedMoveHead = 0;
            queuedMoveCount = 0;
            isMoving = false;
        }

        public void MoveToWorldPosition(Vector3 worldPosition, float duration)
        {
            Vector3 target = worldPosition + Vector3.up * groundOffset;
            float safeDuration = Mathf.Max(0.01f, duration);
            if (isMoving)
            {
                EnqueueMove(target, safeDuration);
                return;
            }

            StartMove(target, safeDuration);
        }

        public void PlayAttack()
        {
            attackTimer = Mathf.Max(attackPulseDuration, 0.01f);
        }

        public void PlayMeleeAttack()
        {
            if (modelRoot == null)
            {
                return;
            }

            KillMeleeAttackSequence();

            Quaternion startRotation = modelRoot.localRotation;
            Quaternion leanBackRotation = startRotation * Quaternion.Euler(meleeAttackLeanBackAngle, 0f, 0f);
            Quaternion strikeRotation = startRotation * Quaternion.Euler(meleeAttackStrikeAngle, 0f, 0f);
            float duration = Mathf.Max(0.01f, meleeAttackDuration);

            meleeAttackSequence = DOTween.Sequence()
                .SetTarget(modelRoot)
                .Append(modelRoot.DOLocalRotateQuaternion(leanBackRotation, duration * 0.25f).SetEase(Ease.OutQuad))
                .Append(modelRoot.DOLocalRotateQuaternion(strikeRotation, duration * 0.35f).SetEase(Ease.InQuad))
                .Append(modelRoot.DOLocalRotateQuaternion(startRotation, duration * 0.4f).SetEase(Ease.OutQuad))
                .OnKill(() => meleeAttackSequence = null);
        }

        public void FaceWorldPosition(Vector3 worldPosition)
        {
            if (modelRoot == null)
            {
                return;
            }

            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            modelRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * baseModelRotation;
        }

        public void PlayDamage(int remainingHp)
        {
            damageTimer = Mathf.Max(damageFlashDuration, 0.01f);
            ApplyColor(damageFlashColor);
        }

        public void PlayDeath()
        {
            if (isDying)
            {
                return;
            }

            KillMeleeAttackSequence();
            isDying = true;
            deathTimer = Mathf.Max(deathDuration, 0.01f);
            deathStartPosition = transform.position;
            isMoving = false;
            queuedMoveHead = 0;
            queuedMoveCount = 0;
        }

        private void ApplySideColor(BattleSide side)
        {
            sideColor = side == BattleSide.Player ? playerColor : enemyColor;
            ApplyColor(sideColor);
        }

        private void ApplyColor(Color color)
        {
            if (meshRenderer == null)
            {
                return;
            }

            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ResetTransientState(Vector3 worldPosition)
        {
            gameObject.SetActive(true);
            isDying = false;
            attackTimer = 0f;
            damageTimer = 0f;
            deathTimer = 0f;
            KillMeleeAttackSequence();
            if (modelRoot != null)
            {
                modelRoot.localScale = baseModelScale;
                modelRoot.localRotation = baseModelRotation;
            }

            SetWorldPosition(worldPosition);
        }

        private void UpdateMovement(float deltaTime)
        {
            if (!isMoving)
            {
                return;
            }

            moveElapsed += deltaTime;
            float normalized = Mathf.Clamp01(moveElapsed / moveDuration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            transform.position = Vector3.LerpUnclamped(moveFrom, moveTo, eased);

            if (normalized >= 1f)
            {
                transform.position = moveTo;
                if (!TryStartNextQueuedMove())
                {
                    isMoving = false;
                }
            }
        }

        private void StartMove(Vector3 target, float duration)
        {
            moveFrom = transform.position;
            moveTo = target;
            moveElapsed = 0f;
            moveDuration = duration;
            KillMeleeAttackSequence();
            FaceWorldPosition(target);
            isMoving = true;
        }

        private void EnqueueMove(Vector3 target, float duration)
        {
            if (queuedMoveCount >= MaxQueuedMoves)
            {
                int lastIndex = (queuedMoveHead + queuedMoveCount - 1) % MaxQueuedMoves;
                queuedMoveTargets[lastIndex] = target;
                queuedMoveDurations[lastIndex] = duration;
                return;
            }

            int index = (queuedMoveHead + queuedMoveCount) % MaxQueuedMoves;
            queuedMoveTargets[index] = target;
            queuedMoveDurations[index] = duration;
            queuedMoveCount++;
        }

        private bool TryStartNextQueuedMove()
        {
            if (queuedMoveCount <= 0)
            {
                return false;
            }

            Vector3 target = queuedMoveTargets[queuedMoveHead];
            float duration = queuedMoveDurations[queuedMoveHead];
            queuedMoveHead = (queuedMoveHead + 1) % MaxQueuedMoves;
            queuedMoveCount--;
            StartMove(target, duration);
            return true;
        }

        private void UpdateVisualTimers(float deltaTime)
        {
            if (modelRoot == null)
            {
                return;
            }

            float pulseScale = 1f;
            if (attackTimer > 0f)
            {
                attackTimer = Mathf.Max(0f, attackTimer - deltaTime);
                float normalized = attackPulseDuration > 0f ? attackTimer / attackPulseDuration : 0f;
                pulseScale += Mathf.Sin((1f - normalized) * Mathf.PI) * 0.12f;
            }

            if (isDying)
            {
                deathTimer = Mathf.Max(0f, deathTimer - deltaTime);
                float normalized = deathDuration > 0f ? deathTimer / deathDuration : 0f;
                pulseScale *= Mathf.Clamp01(normalized);
                transform.position = deathStartPosition + Vector3.down * ((1f - normalized) * deathSinkDistance);

                if (deathTimer <= 0f)
                {
                    KillMeleeAttackSequence();
                    gameObject.SetActive(false);
                    return;
                }
            }

            modelRoot.localScale = baseModelScale * pulseScale;

            if (damageTimer > 0f)
            {
                damageTimer = Mathf.Max(0f, damageTimer - deltaTime);
                if (damageTimer <= 0f)
                {
                    ApplyColor(sideColor);
                }
            }
        }

        private static string FormatUnitName(BattleSide side, int runtimeId, UnitDefinition definition)
        {
            string displayName = definition != null ? definition.DisplayName : "Unknown";
            return side + "_Unit_" + runtimeId + "_" + displayName;
        }

        private void KillMeleeAttackSequence()
        {
            if (meleeAttackSequence == null)
            {
                return;
            }

            meleeAttackSequence.Kill();
            meleeAttackSequence = null;
        }
    }
}
