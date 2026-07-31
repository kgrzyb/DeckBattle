using UnityEngine;

namespace DeckBattle
{
    public sealed class UnitView : MonoBehaviour
    {
        private const int MaxQueuedMoves = 4;

        private enum UnitVisualState
        {
            Idle = 0,
            Run = 1,
            Attack = 2,
            Special = 3,
            Dead = 4
        }

        private static readonly int IdleTrigger = Animator.StringToHash("idle");
        private static readonly int RunTrigger = Animator.StringToHash("run");
        private static readonly int AttackTrigger = Animator.StringToHash("attack");
        private static readonly int AttackSpeedParameter = Animator.StringToHash("attackSpeed");
        private static readonly int SpecialTrigger = Animator.StringToHash("special");
        private static readonly int DeadTrigger = Animator.StringToHash("dead");

        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private float groundOffset = 0.65f;
        [SerializeField] private float damageFlashDuration = 0.12f;
        [SerializeField] private float deathDuration = 0.25f;
        [SerializeField] private float attackWindupAnimationDuration = 0.25f;
        [SerializeField, Min(1f)] private float rotationSpeedDegreesPerSecond = 540f;
        [SerializeField] private Color playerColor = new Color(0.18f, 0.62f, 0.95f, 1f);
        [SerializeField] private Color enemyColor = new Color(0.95f, 0.35f, 0.25f, 1f);
        [SerializeField] private Color damageFlashColor = Color.white;

        public int RuntimeId { get; private set; }
        public RuntimeUnit Unit { get; private set; }
        public UnitRuntimeState RealtimeUnit { get; private set; }
        public Transform StatusVfxPivot { get { return transform; } }

        private MaterialPropertyBlock propertyBlock;
        private Vector3 baseModelScale;
        private Quaternion baseModelRotation;
        private Quaternion facingTargetRotation;
        private Vector3 moveFrom;
        private Vector3 moveTo;
        private Vector3 lastKnownTargetWorldPosition;
        private readonly Vector3[] queuedMoveTargets = new Vector3[MaxQueuedMoves];
        private readonly float[] queuedMoveDurations = new float[MaxQueuedMoves];
        private Color sideColor;
        private float moveElapsed;
        private float moveDuration;
        private float damageTimer;
        private float deathTimer;
        private int queuedMoveHead;
        private int queuedMoveCount;
        private bool isMoving;
        private bool isDying;
        private bool isTurning;
        private bool hasKnownTargetWorldPosition;
        private int activeAttackSequenceId;
        private int activeSpecialSequenceId;
        private UnitVisualState visualState;

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

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            baseModelScale = modelRoot.localScale;
            baseModelRotation = modelRoot.localRotation;
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!HasActiveFrameWork)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateMovement(deltaTime);
            UpdateFacing(deltaTime);
            UpdateVisualTimers(deltaTime);
        }

        private bool HasActiveFrameWork
        {
            get { return isMoving || isTurning || damageTimer > 0f || isDying; }
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
            name = unit.Side + "_Unit_" + unit.UnitId;
            ApplySideColor(unit.Side);
        }

        public void Bind(UnitPresentationState state, Vector3 worldPosition)
        {
            Unit = null;
            RealtimeUnit = null;
            RuntimeId = state.UnitId;
            ResetTransientState(worldPosition);
            name = state.Side + "_Unit_" + state.UnitId;
            ApplySideColor(state.Side);
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

        public void BeginAttackWindup(int sequenceId, float duration)
        {
            activeAttackSequenceId = sequenceId;
            if (animator != null)
            {
                animator.SetFloat(AttackSpeedParameter, CalculateAttackAnimationSpeed(duration));
            }

            TriggerAnimation(UnitVisualState.Attack, true);
        }

        public void PlayAttackFire(int sequenceId)
        {
            if (sequenceId != activeAttackSequenceId) return;
            visualState = UnitVisualState.Idle;
        }

        public void CancelAttackWindup(int sequenceId)
        {
            if (sequenceId != activeAttackSequenceId) return;
            TriggerAnimation(UnitVisualState.Idle);
        }

        public void BeginSpecialWindup(int sequenceId, float duration)
        {
            activeSpecialSequenceId = sequenceId;
            TriggerAnimation(UnitVisualState.Special, true);
        }

        public void CompleteSpecialWindup(int sequenceId)
        {
            if (sequenceId != activeSpecialSequenceId) return;
            visualState = UnitVisualState.Idle;
        }

        public void CancelSpecialWindup(int sequenceId)
        {
            if (sequenceId != activeSpecialSequenceId) return;
            TriggerAnimation(UnitVisualState.Idle);
        }

        public void FaceWorldPosition(Vector3 worldPosition, bool immediately = false)
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

            facingTargetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * baseModelRotation;
            if (immediately)
            {
                modelRoot.rotation = facingTargetRotation;
                isTurning = false;
                return;
            }

            isTurning = Quaternion.Angle(modelRoot.rotation, facingTargetRotation) > 0.01f;
        }

        public void SetTargetWorldPosition(Vector3 worldPosition)
        {
            lastKnownTargetWorldPosition = worldPosition;
            hasKnownTargetWorldPosition = true;
            if (!isMoving && queuedMoveCount == 0)
            {
                FaceWorldPosition(worldPosition);
            }
        }

        public void ClearTargetWorldPosition()
        {
            hasKnownTargetWorldPosition = false;
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

            isDying = true;
            deathTimer = Mathf.Max(deathDuration, 0.01f);
            isMoving = false;
            queuedMoveHead = 0;
            queuedMoveCount = 0;
            TriggerAnimation(UnitVisualState.Dead, true);
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
            damageTimer = 0f;
            deathTimer = 0f;
            activeAttackSequenceId = 0;
            activeSpecialSequenceId = 0;
            isTurning = false;
            hasKnownTargetWorldPosition = false;
            if (modelRoot != null)
            {
                modelRoot.localScale = baseModelScale;
                modelRoot.localRotation = baseModelRotation;
                facingTargetRotation = modelRoot.rotation;
            }

            SetWorldPosition(worldPosition);
            ResetAnimator();
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
                    FaceLastKnownTarget();
                    TriggerAnimation(UnitVisualState.Idle);
                }
            }
        }

        private void StartMove(Vector3 target, float duration)
        {
            moveFrom = transform.position;
            moveTo = target;
            moveElapsed = 0f;
            moveDuration = duration;
            FaceWorldPosition(target);
            isMoving = true;
            TriggerAnimation(UnitVisualState.Run);
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
            if (isDying)
            {
                deathTimer = Mathf.Max(0f, deathTimer - deltaTime);

                if (deathTimer <= 0f)
                {
                    gameObject.SetActive(false);
                    return;
                }
            }

            if (damageTimer > 0f)
            {
                damageTimer = Mathf.Max(0f, damageTimer - deltaTime);
                if (damageTimer <= 0f)
                {
                    ApplyColor(sideColor);
                }
            }
        }

        private void UpdateFacing(float deltaTime)
        {
            if (!isTurning || modelRoot == null)
            {
                return;
            }

            float maxDegreesDelta = Mathf.Max(1f, rotationSpeedDegreesPerSecond) * Mathf.Max(0f, deltaTime);
            modelRoot.rotation = Quaternion.RotateTowards(modelRoot.rotation, facingTargetRotation, maxDegreesDelta);
            if (Quaternion.Angle(modelRoot.rotation, facingTargetRotation) <= 0.01f)
            {
                modelRoot.rotation = facingTargetRotation;
                isTurning = false;
            }
        }

        private void FaceLastKnownTarget()
        {
            if (hasKnownTargetWorldPosition)
            {
                FaceWorldPosition(lastKnownTargetWorldPosition);
            }
        }

        private static string FormatUnitName(BattleSide side, int runtimeId, UnitDefinition definition)
        {
            string displayName = definition != null ? definition.DisplayName : "Unknown";
            return side + "_Unit_" + runtimeId + "_" + displayName;
        }

        private void ResetAnimator()
        {
            visualState = UnitVisualState.Idle;
            if (animator == null)
            {
                return;
            }

            animator.ResetTrigger(IdleTrigger);
            animator.ResetTrigger(RunTrigger);
            animator.ResetTrigger(AttackTrigger);
            animator.ResetTrigger(SpecialTrigger);
            animator.ResetTrigger(DeadTrigger);
            animator.Rebind();
            animator.Update(0f);
            animator.SetFloat(AttackSpeedParameter, 1f);
            animator.SetTrigger(IdleTrigger);
        }

        private float CalculateAttackAnimationSpeed(float actualDuration)
        {
            float baseDuration = Mathf.Max(0.01f, attackWindupAnimationDuration);
            return actualDuration > 0f && !float.IsNaN(actualDuration) && !float.IsInfinity(actualDuration)
                ? baseDuration / actualDuration
                : 1f;
        }

        private void TriggerAnimation(UnitVisualState nextState, bool force = false)
        {
            if (isDying && nextState != UnitVisualState.Dead)
            {
                return;
            }

            if (!force && visualState == nextState)
            {
                return;
            }

            visualState = nextState;
            if (animator == null)
            {
                return;
            }

            switch (nextState)
            {
                case UnitVisualState.Idle: animator.SetTrigger(IdleTrigger); break;
                case UnitVisualState.Run: animator.SetTrigger(RunTrigger); break;
                case UnitVisualState.Attack: animator.SetTrigger(AttackTrigger); break;
                case UnitVisualState.Special: animator.SetTrigger(SpecialTrigger); break;
                case UnitVisualState.Dead: animator.SetTrigger(DeadTrigger); break;
            }
        }
    }
}
