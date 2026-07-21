using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BattleViewFacingTests
    {
        [Test]
        public void FaceIdleUnitsTowardTargets_RotatesIdleUnitTowardTarget()
        {
            TestContext context = CreateContext();
            try
            {
                context.Player.SetTarget(context.Enemy);

                InvokePrivateMethod(context.BattleView, "FaceIdleUnitsTowardTargets");

                Vector3 targetDirection = context.BoardPresenter.GetWorldPosition(context.Enemy.CurrentHex)
                    - context.PlayerView.transform.position;
                targetDirection.y = 0f;

                Assert.That(
                    Vector3.Dot(context.PlayerModel.transform.forward, targetDirection.normalized),
                    Is.GreaterThan(0.999f));
            }
            finally
            {
                context.Destroy();
            }
        }

        [Test]
        public void FaceIdleUnitsTowardTargets_DoesNotRotateMovingUnit()
        {
            TestContext context = CreateContext();
            try
            {
                context.Player.SetTarget(context.Enemy);
                context.Player.IsMoving = true;
                context.PlayerModel.transform.rotation = Quaternion.identity;

                InvokePrivateMethod(context.BattleView, "FaceIdleUnitsTowardTargets");

                Assert.That(Quaternion.Angle(Quaternion.identity, context.PlayerModel.transform.rotation), Is.LessThan(0.01f));
            }
            finally
            {
                context.Destroy();
            }
        }

        private static TestContext CreateContext()
        {
            UnitDefinition playerDefinition = TestDefinitions.CreateUnit("player", 1);
            UnitDefinition enemyDefinition = TestDefinitions.CreateUnit("enemy", 1);
            var board = new HexBoard(3, 3, 1f);
            BattleSimulation simulation = BattleSimulation.Create(
                board,
                new[]
                {
                    new UnitSpawnData(1, playerDefinition, BattleSide.Player, new HexCoord(0, 1)),
                    new UnitSpawnData(2, enemyDefinition, BattleSide.Enemy, new HexCoord(2, 1))
                });

            GameObject presenterObject = new GameObject("BoardPresenter", typeof(BoardPresenter));
            BoardPresenter boardPresenter = presenterObject.GetComponent<BoardPresenter>();
            SetPrivateField(boardPresenter, "board", board);

            GameObject battleViewObject = new GameObject("BattleView", typeof(BattleView));
            BattleView battleView = battleViewObject.GetComponent<BattleView>();
            SetPrivateField(battleView, "simulation", simulation);
            SetPrivateField(battleView, "boardPresenter", boardPresenter);

            GameObject playerObject = new GameObject("PlayerView", typeof(UnitView));
            GameObject playerModel = new GameObject("PlayerModel");
            playerModel.transform.SetParent(playerObject.transform);
            UnitView playerView = playerObject.GetComponent<UnitView>();
            InvokePrivateMethod(playerView, "Awake");
            playerView.Bind(simulation.Units[0], boardPresenter.GetWorldPosition(simulation.Units[0].CurrentHex));

            Dictionary<int, UnitView> views = GetPrivateField<Dictionary<int, UnitView>>(battleView, "unitViewByUnitId");
            views.Add(simulation.Units[0].UnitId, playerView);

            return new TestContext(
                playerDefinition,
                enemyDefinition,
                presenterObject,
                battleViewObject,
                playerObject,
                playerModel,
                boardPresenter,
                battleView,
                simulation.Units[0],
                simulation.Units[1],
                playerView);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }

        private sealed class TestContext
        {
            private readonly UnitDefinition playerDefinition;
            private readonly UnitDefinition enemyDefinition;
            private readonly GameObject presenterObject;
            private readonly GameObject battleViewObject;
            private readonly GameObject playerObject;

            public readonly GameObject PlayerModel;
            public readonly BoardPresenter BoardPresenter;
            public readonly BattleView BattleView;
            public readonly UnitRuntimeState Player;
            public readonly UnitRuntimeState Enemy;
            public readonly UnitView PlayerView;

            public TestContext(
                UnitDefinition playerDefinition,
                UnitDefinition enemyDefinition,
                GameObject presenterObject,
                GameObject battleViewObject,
                GameObject playerObject,
                GameObject playerModel,
                BoardPresenter boardPresenter,
                BattleView battleView,
                UnitRuntimeState player,
                UnitRuntimeState enemy,
                UnitView playerView)
            {
                this.playerDefinition = playerDefinition;
                this.enemyDefinition = enemyDefinition;
                this.presenterObject = presenterObject;
                this.battleViewObject = battleViewObject;
                this.playerObject = playerObject;
                PlayerModel = playerModel;
                BoardPresenter = boardPresenter;
                BattleView = battleView;
                Player = player;
                Enemy = enemy;
                PlayerView = playerView;
            }

            public void Destroy()
            {
                Object.DestroyImmediate(playerDefinition);
                Object.DestroyImmediate(enemyDefinition);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(battleViewObject);
                Object.DestroyImmediate(presenterObject);
            }
        }
    }
}
