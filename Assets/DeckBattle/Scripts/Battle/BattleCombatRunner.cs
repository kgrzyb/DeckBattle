using System;
using System.Collections.Generic;
using Conditional = System.Diagnostics.ConditionalAttribute;
using UnityEngine;

namespace DeckBattle
{
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
        private int maxTicks;
        private int maxTicksPerFrame;
        private int ticksElapsed;
        private BattleTickResult lastTickResult;
        private BattleRunResult result;

        public event Action<BattleTickResult, IReadOnlyList<BattleEvent>> TickProcessed;
        public event Action<BattleRunResult> Completed;

        public bool IsRunning { get; private set; }
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

            tickAccumulator += Mathf.Max(0f, deltaTime);
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
