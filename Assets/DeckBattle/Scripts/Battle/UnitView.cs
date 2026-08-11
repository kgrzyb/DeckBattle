using System;
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

        private const int AnimatorLayerIndex = 0;
        private const float LocomotionTransitionDuration = 0.1f;
        private const float ActionTransitionDuration = 0.08f;
        private const float DeathTransitionDuration = 0.05f;

        private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
        private static readonly int RunState = Animator.StringToHash("Base Layer.Run");
        private static readonly int AttackState = Animator.StringToHash("Base Layer.Attack");
        private static readonly int AttackSpeedParameter = Animator.StringToHash("attackSpeed");
        private static readonly int RunSpeedParameter = Animator.StringToHash("runSpeed");
        private static readonly int SpecialState = Animator.StringToHash("Base Layer.Special");
        private static readonly int DeadState = Animator.StringToHash("Base Layer.Dead");

        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private UnitVfxAnchors vfxAnchors;
        [SerializeField] private float groundOffset = 0.65f;
        [SerializeField] private float deathDuration = 0.25f;
        [SerializeField] private float attackWindupAnimationDuration = 0.25f;
        [SerializeField, Min(1f)] private float rotationSpeedDegreesPerSecond = 540f;

        public int RuntimeId { get; private set; }
        public RuntimeUnit Unit { get; private set; }
        public UnitRuntimeState RealtimeUnit { get; private set; }
        internal float RunAnimationSpeedMultiplier { get { return runAnimationSpeedMultiplier; } }
        public event Action<UnitView, UnitAnimationVfxSignal> AnimationVfxSignal;

        private Vector3 baseModelScale;
        private Quaternion baseModelRotation;
        private Quaternion facingTargetRotation;
        private Vector3 moveFrom;
        private Vector3 moveTo;
        private Vector3 lastKnownTargetWorldPosition;
        private readonly Vector3[] queuedMoveTargets = new Vector3[MaxQueuedMoves];
        private readonly float[] queuedMoveDurations = new float[MaxQueuedMoves];
        private float moveElapsed;
        private float moveDuration;
        private float deathTimer;
        private float combatSpeed = 1f;
        private float runAnimationSpeedMultiplier = 1f;
        private int queuedMoveHead;
        private int queuedMoveCount;
        private bool isMoving;
        private bool isDying;
        private bool isTurning;
        private bool hasKnownTargetWorldPosition;
        private bool hasResolvedVfxAnchors;
        private int activeAttackSequenceId;
        private int activeSpecialSequenceId;
        private UnitVisualState visualState;

        private void Awake()
        {
            if (modelRoot == null)
            {
                modelRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = combatSpeed;
            }

            CacheVfxAnchors();

            baseModelScale = modelRoot.localScale;
            baseModelRotation = modelRoot.localRotation;
        }

        private void Update()
        {
            if (!HasActiveFrameWork)
            {
                return;
            }

            float deltaTime = Time.deltaTime * combatSpeed;
            UpdateMovement(deltaTime);
            UpdateFacing(deltaTime);
            UpdateDeathTimer(deltaTime);
        }

        private bool HasActiveFrameWork
        {
            get { return isMoving || isTurning || isDying; }
        }

        public void Bind(RuntimeUnit unit, Vector3 worldPosition)
        {
            Unit = unit;
            RealtimeUnit = null;
            RuntimeId = unit.RuntimeId;
            ResetTransientState(worldPosition);
            name = FormatUnitName(unit.Side, unit.RuntimeId, unit.Definition);
        }

        public void Bind(UnitRuntimeState unit, Vector3 worldPosition)
        {
            Unit = null;
            RealtimeUnit = unit;
            RuntimeId = unit.UnitId;
            ResetTransientState(worldPosition);
            name = unit.Side + "_Unit_" + unit.UnitId;
        }

        public void Bind(UnitPresentationState state, Vector3 worldPosition)
        {
            // The registry reuses the formation view when combat presentation begins.
            bool preserveRuntimeUnitName = RuntimeId == state.UnitId && Unit != null;
            Unit = null;
            RealtimeUnit = null;
            RuntimeId = state.UnitId;
            ResetTransientState(worldPosition);
            if (!preserveRuntimeUnitName)
            {
                name = state.Side + "_Unit_" + state.UnitId;
            }

        }

        public Transform ResolveVfxAnchor(UnitVfxAnchor anchor)
        {
            CacheVfxAnchors();
            if (vfxAnchors == null)
            {
                return transform;
            }

            Transform resolved = vfxAnchors.Resolve(anchor);
            return resolved != null ? resolved : transform;
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

        public void SetCombatSpeed(float speed)
        {
            combatSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(speed);
            if (animator != null)
            {
                animator.speed = combatSpeed;
            }
        }

        public void SetRunAnimationSpeedMultiplier(float multiplier)
        {
            runAnimationSpeedMultiplier = ResolveRunAnimationSpeedMultiplier(multiplier);
            if (animator != null)
            {
                animator.SetFloat(RunSpeedParameter, runAnimationSpeedMultiplier);
            }
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

        public void BeginSpecialWindup(int sequenceId, UnitSpecialKind specialKind, float duration)
        {
            activeSpecialSequenceId = sequenceId;
            if (specialKind != UnitSpecialKind.FurySwipes)
            {
                TriggerAnimation(UnitVisualState.Special, true);
            }
        }

        public void CompleteSpecialWindup(int sequenceId)
        {
            if (sequenceId != activeSpecialSequenceId) return;
            TriggerAnimation(UnitVisualState.Idle);
        }

        public void CancelSpecialWindup(int sequenceId)
        {
            if (sequenceId != activeSpecialSequenceId) return;
            TriggerAnimation(UnitVisualState.Idle);
        }

        public void BeginSpecialCast(int sequenceId, UnitSpecialKind specialKind)
        {
            if (sequenceId != activeSpecialSequenceId) return;
            FaceLastKnownTarget();
            if (specialKind != UnitSpecialKind.MegaArrow)
            {
                TriggerAnimation(UnitVisualState.Special, true);
            }
        }

        public void PlaySpecialStrike(int sequenceId)
        {
            if (sequenceId != activeSpecialSequenceId) return;
            FaceLastKnownTarget();
        }

        public void PlaySpecialAttackAnimationEvent()
        {
            if (isDying || visualState != UnitVisualState.Special)
            {
                return;
            }

            FaceLastKnownTarget();
            AnimationVfxSignal?.Invoke(this, UnitAnimationVfxSignal.SpecialContact);
        }

        public void PlayAttackContactAnimationEvent()
        {
            if (isDying || visualState != UnitVisualState.Attack)
            {
                return;
            }

            FaceLastKnownTarget();
            AnimationVfxSignal?.Invoke(this, UnitAnimationVfxSignal.AttackContact);
        }

        public void PlayProjectileReleaseAnimationEvent()
        {
            if (isDying || visualState != UnitVisualState.Attack)
            {
                return;
            }

            FaceLastKnownTarget();
            AnimationVfxSignal?.Invoke(this, UnitAnimationVfxSignal.ProjectileRelease);
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

        private void CacheVfxAnchors()
        {
            if (hasResolvedVfxAnchors)
            {
                return;
            }

            vfxAnchors = vfxAnchors != null
                ? vfxAnchors
                : GetComponentInChildren<UnitVfxAnchors>(true);
            hasResolvedVfxAnchors = true;
        }

        private void ResetTransientState(Vector3 worldPosition)
        {
            gameObject.SetActive(true);
            isDying = false;
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

            float remainingDeltaTime = Mathf.Max(0f, deltaTime);
            while (isMoving)
            {
                float segmentTimeRemaining = Mathf.Max(0f, moveDuration - moveElapsed);
                if (segmentTimeRemaining > remainingDeltaTime)
                {
                    moveElapsed += remainingDeltaTime;
                    float normalized = moveElapsed / moveDuration;
                    transform.position = Vector3.LerpUnclamped(moveFrom, moveTo, normalized);
                    return;
                }

                transform.position = moveTo;
                remainingDeltaTime -= segmentTimeRemaining;
                if (!TryStartNextQueuedMove())
                {
                    isMoving = false;
                    FaceLastKnownTarget();
                    if (visualState == UnitVisualState.Run)
                    {
                        TriggerAnimation(UnitVisualState.Idle);
                    }
                    return;
                }

                // Consume leftover frame time on the following waypoint so the view
                // retains its velocity while crossing adjacent hexes.
                if (remainingDeltaTime <= 0f)
                {
                    return;
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

        private void UpdateDeathTimer(float deltaTime)
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

            animator.Rebind();
            animator.Update(0f);
            animator.speed = combatSpeed;
            animator.SetFloat(AttackSpeedParameter, 1f);
            animator.SetFloat(RunSpeedParameter, runAnimationSpeedMultiplier);
            animator.Play(IdleState, AnimatorLayerIndex, 0f);
        }

        internal static float ResolveRunAnimationSpeedMultiplier(float multiplier)
        {
            return multiplier > 0f && !float.IsNaN(multiplier) && !float.IsInfinity(multiplier)
                ? multiplier
                : 1f;
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

            animator.CrossFadeInFixedTime(
                GetAnimatorState(nextState),
                GetTransitionDuration(nextState),
                AnimatorLayerIndex,
                0f);
        }

        private static int GetAnimatorState(UnitVisualState state)
        {
            switch (state)
            {
                case UnitVisualState.Idle: return IdleState;
                case UnitVisualState.Run: return RunState;
                case UnitVisualState.Attack: return AttackState;
                case UnitVisualState.Special: return SpecialState;
                case UnitVisualState.Dead: return DeadState;
                default: return IdleState;
            }
        }

        private static float GetTransitionDuration(UnitVisualState state)
        {
            switch (state)
            {
                case UnitVisualState.Idle:
                case UnitVisualState.Run:
                    return LocomotionTransitionDuration;
                case UnitVisualState.Dead:
                    return DeathTransitionDuration;
                default:
                    return ActionTransitionDuration;
            }
        }
    }
}
