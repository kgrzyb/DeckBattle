using System;
using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using UnityEngine;

namespace DeckBattle
{
    [DefaultExecutionOrder(-100)]
    public sealed class BattleCombatRunner : MonoBehaviour
    {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        [SerializeField] private bool captureDebugSnapshots;
#endif

        private readonly BattleEventQueue eventQueue = new BattleEventQueue(32);
        private readonly BattleDebugSnapshot debugSnapshot = new BattleDebugSnapshot(16);
        private BattlePresentationSnapshot presentationSnapshot;

        private BattleSimulation simulation;
        private BattleTickLoop tickLoop;
        private float tickAccumulator;
        private float combatAccelerationDelay;
        private float acceleratedCombatSpeed;
        private bool accelerationActivated;
        private int maxTicks;
        private int maxTicksPerFrame;
        private int ticksElapsed;
        private BattleTickResult lastTickResult;
        private BattleRunResult result;

        public event Action<BattleTickResult, IReadOnlyList<BattleEvent>> TickProcessed;
        public event Action<BattleRunResult> Completed;
        public event Action<float> CombatSpeedChanged;

        public bool IsRunning { get; private set; }
        public float CurrentCombatSpeed { get; private set; }
        public float CombatElapsedTime { get; private set; }
        public float CombatAccelerationDelay { get { return combatAccelerationDelay; } }
        public bool IsCombatAccelerationEnabled { get { return acceleratedCombatSpeed > 1f && combatAccelerationDelay > 0f; } }
        public BattleSimulation Simulation { get { return simulation; } }
        public BattleRunResult Result { get { return result; } }
        public BattleDebugSnapshot DebugSnapshot { get { return debugSnapshot; } }
        public BattlePresentationSnapshot PresentationSnapshot { get { return presentationSnapshot; } }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        public void StartCombat(BattleSimulation nextSimulation, float tickDuration, int nextMaxTicks, int nextMaxTicksPerFrame)
        {
            StartCombat(
                nextSimulation,
                tickDuration,
                nextMaxTicks,
                nextMaxTicksPerFrame,
                0f,
                1f);
        }

        public void StartCombat(
            BattleSimulation nextSimulation,
            float tickDuration,
            int nextMaxTicks,
            int nextMaxTicksPerFrame,
            float nextCombatAccelerationDelay,
            float nextAcceleratedCombatSpeed)
        {
            if (nextSimulation == null)
            {
                throw new ArgumentNullException(nameof(nextSimulation));
            }

            if (tickDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickDuration));
            }

            simulation = nextSimulation;
            tickLoop = new BattleTickLoop(simulation, tickDuration);
            maxTicks = Math.Max(1, nextMaxTicks);
            maxTicksPerFrame = Math.Max(1, nextMaxTicksPerFrame);
            combatAccelerationDelay = BattleTiming.ResolveCombatAccelerationDelay(nextCombatAccelerationDelay);
            acceleratedCombatSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(nextAcceleratedCombatSpeed);
            CombatElapsedTime = 0f;
            CurrentCombatSpeed = combatAccelerationDelay <= 0f
                ? acceleratedCombatSpeed
                : 1f;
            accelerationActivated = combatAccelerationDelay <= 0f || acceleratedCombatSpeed <= 1f;
            tickAccumulator = 0f;
            ticksElapsed = 0;
            lastTickResult = new BattleTickResult(0, 0, false, false, BattleSide.Player);
            result = null;
            eventQueue.Clear();
            if (presentationSnapshot == null)
            {
                presentationSnapshot = new BattlePresentationSnapshot(simulation.Units.Count);
            }

            presentationSnapshot.Capture(simulation);
            IsRunning = true;
            CaptureDebugSnapshot(null);
        }

        public void StopCombat()
        {
            IsRunning = false;
            simulation = null;
            tickLoop = null;
            tickAccumulator = 0f;
            combatAccelerationDelay = BattleTiming.DefaultCombatAccelerationDelay;
            acceleratedCombatSpeed = BattleTiming.DefaultAcceleratedCombatSpeed;
            accelerationActivated = false;
            CombatElapsedTime = 0f;
            CurrentCombatSpeed = 1f;
            ticksElapsed = 0;
            result = null;
            eventQueue.Clear();
            CaptureDebugSnapshot(null);
        }

        public void Advance(float deltaTime)
        {
            if (!IsRunning || simulation == null || tickLoop == null)
            {
                return;
            }

            UpdateCombatSpeedForFrame();

            float baseDeltaTime = Mathf.Max(0f, deltaTime);
            CombatElapsedTime += baseDeltaTime;
            tickAccumulator += baseDeltaTime * CurrentCombatSpeed;
            int ticksThisFrame = 0;
            while (tickAccumulator >= tickLoop.TickDuration && ticksThisFrame < maxTicksPerFrame)
            {
                if (ticksElapsed >= maxTicks)
                {
                    Complete(CombatEndReason.MaxTicksReached);
                    return;
                }

                BattleTickResult tickResult = tickLoop.Tick(eventQueue);
                ticksElapsed++;
                lastTickResult = tickResult;
                CaptureDebugSnapshot(eventQueue.Events);
                TickProcessed?.Invoke(tickResult, eventQueue.Events);

                tickAccumulator -= tickLoop.TickDuration;
                ticksThisFrame++;

                if (tickResult.BattleEnded)
                {
                    Complete(tickResult.HasWinner ? CombatEndReason.OneSideDefeated : CombatEndReason.BothSidesDefeated);
                    return;
                }

                if (ticksElapsed >= maxTicks)
                {
                    Complete(CombatEndReason.MaxTicksReached);
                    return;
                }
            }

            if (ticksThisFrame >= maxTicksPerFrame)
            {
                tickAccumulator = 0f;
            }
        }

        private void Complete(CombatEndReason endReason)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            tickAccumulator = 0f;
            result = new BattleRunResult(ticksElapsed, lastTickResult, endReason == CombatEndReason.MaxTicksReached, endReason);
            Completed?.Invoke(result);
        }

        private void UpdateCombatSpeedForFrame()
        {
            if (accelerationActivated || CombatElapsedTime < combatAccelerationDelay)
            {
                return;
            }

            accelerationActivated = true;
            CurrentCombatSpeed = acceleratedCombatSpeed;
            if (CurrentCombatSpeed > 1f)
            {
                CombatSpeedChanged?.Invoke(CurrentCombatSpeed);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private void CaptureDebugSnapshot(IReadOnlyList<BattleEvent> events)
        {
#if UNITY_EDITOR
            debugSnapshot.Capture(simulation, events);
#elif DEVELOPMENT_BUILD
            if (captureDebugSnapshots)
            {
                debugSnapshot.Capture(simulation, events);
            }
#endif
        }
    }

    public sealed class BattleRunResult
    {
        public readonly int Ticks;
        public readonly BattleTickResult LastTickResult;
        public readonly bool MaxTicksReached;
        public readonly CombatEndReason EndReason;

        public BattleRunResult(int ticks, BattleTickResult lastTickResult, bool maxTicksReached, CombatEndReason endReason)
        {
            Ticks = ticks;
            LastTickResult = lastTickResult;
            MaxTicksReached = maxTicksReached;
            EndReason = endReason;
        }
    }
}
