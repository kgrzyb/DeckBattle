using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class UnitViewFacingTests
    {
        [Test]
        public void FaceWorldPosition_RotatesModelTowardPlanarTarget()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject modelObject = new GameObject("Model");
            try
            {
                modelObject.transform.SetParent(unitObject.transform);
                UnitView view = unitObject.GetComponent<UnitView>();
                SetPrivateField(view, "modelRoot", modelObject.transform);
                InvokePrivateMethod(view, "Awake");

                unitObject.transform.position = Vector3.zero;
                view.FaceWorldPosition(new Vector3(1f, 2f, 0f));
                InvokePrivateMethod(view, "UpdateFacing", 1f);

                Assert.That(Vector3.Dot(modelObject.transform.forward, Vector3.right), Is.GreaterThan(0.999f));
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void FaceWorldPosition_WhenTargetOverlaps_DoesNotChangeRotation()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject modelObject = new GameObject("Model");
            try
            {
                modelObject.transform.SetParent(unitObject.transform);
                UnitView view = unitObject.GetComponent<UnitView>();
                SetPrivateField(view, "modelRoot", modelObject.transform);
                InvokePrivateMethod(view, "Awake");

                Quaternion rotation = Quaternion.Euler(0f, 45f, 0f);
                modelObject.transform.rotation = rotation;
                unitObject.transform.position = new Vector3(2f, 0f, 3f);

                view.FaceWorldPosition(unitObject.transform.position + Vector3.up * 4f);

                Assert.That(Quaternion.Angle(rotation, modelObject.transform.rotation), Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void FaceWorldPosition_WhenModelRootNotAssigned_RotatesChildModel()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject modelObject = new GameObject("Model");
            try
            {
                modelObject.transform.SetParent(unitObject.transform);
                UnitView view = unitObject.GetComponent<UnitView>();
                InvokePrivateMethod(view, "Awake");

                unitObject.transform.position = Vector3.zero;
                view.FaceWorldPosition(new Vector3(1f, 0f, 0f));
                InvokePrivateMethod(view, "UpdateFacing", 1f);

                Assert.That(Vector3.Dot(modelObject.transform.forward, Vector3.right), Is.GreaterThan(0.999f));
                Assert.That(Quaternion.Angle(Quaternion.identity, unitObject.transform.rotation), Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void MoveToWorldPosition_RotatesModelTowardMovementTarget()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject modelObject = new GameObject("Model");
            try
            {
                modelObject.transform.SetParent(unitObject.transform);
                UnitView view = unitObject.GetComponent<UnitView>();
                SetPrivateField(view, "modelRoot", modelObject.transform);
                InvokePrivateMethod(view, "Awake");

                view.SetWorldPosition(Vector3.zero);
                view.MoveToWorldPosition(new Vector3(0f, 0f, 1f), 0.25f);
                InvokePrivateMethod(view, "UpdateFacing", 1f);

                Assert.That(Vector3.Dot(modelObject.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void FaceWorldPosition_UsesConfiguredConstantRotationSpeed()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject modelObject = new GameObject("Model");
            try
            {
                modelObject.transform.SetParent(unitObject.transform);
                UnitView view = unitObject.GetComponent<UnitView>();
                SetPrivateField(view, "modelRoot", modelObject.transform);
                SetPrivateField(view, "rotationSpeedDegreesPerSecond", 90f);
                InvokePrivateMethod(view, "Awake");

                view.FaceWorldPosition(Vector3.right);
                InvokePrivateMethod(view, "UpdateFacing", 0.5f);

                Assert.That(Vector3.Angle(modelObject.transform.forward, Vector3.forward), Is.EqualTo(45f).Within(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void SetTargetWorldPosition_DuringQueuedMovement_FacesTargetOnlyAfterFinalHex()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject modelObject = new GameObject("Model");
            try
            {
                modelObject.transform.SetParent(unitObject.transform);
                UnitView view = unitObject.GetComponent<UnitView>();
                SetPrivateField(view, "modelRoot", modelObject.transform);
                SetPrivateField(view, "rotationSpeedDegreesPerSecond", 1000f);
                InvokePrivateMethod(view, "Awake");

                view.SetWorldPosition(Vector3.zero);
                view.MoveToWorldPosition(Vector3.forward, 0.25f);
                view.MoveToWorldPosition(Vector3.forward * 2f, 0.25f);
                view.SetTargetWorldPosition(Vector3.right * 3f);

                InvokePrivateMethod(view, "UpdateFacing", 1f);
                Assert.That(Vector3.Dot(modelObject.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));

                InvokePrivateMethod(view, "UpdateMovement", 0.25f);
                InvokePrivateMethod(view, "UpdateFacing", 1f);
                Assert.That(Vector3.Dot(modelObject.transform.forward, Vector3.forward), Is.GreaterThan(0.999f));

                InvokePrivateMethod(view, "UpdateMovement", 0.25f);
                InvokePrivateMethod(view, "UpdateFacing", 1f);
                Assert.That(Vector3.Dot(modelObject.transform.forward, Vector3.right), Is.GreaterThan(0.999f));
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void QueuedMovement_ConsumesRemainingFrameTimeOnFollowingWaypoint()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            GameObject modelObject = new GameObject("Model");
            try
            {
                modelObject.transform.SetParent(unitObject.transform);
                UnitView view = unitObject.GetComponent<UnitView>();
                SetPrivateField(view, "modelRoot", modelObject.transform);
                InvokePrivateMethod(view, "Awake");

                view.SetWorldPosition(Vector3.zero);
                view.MoveToWorldPosition(Vector3.forward, 1f);
                view.MoveToWorldPosition(Vector3.forward * 2f, 1f);

                InvokePrivateMethod(view, "UpdateMovement", 1.25f);

                Assert.That(unitObject.transform.position.z, Is.EqualTo(1.25f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void MovementCompletion_DoesNotInterruptAttackWindupStartedDuringMovement()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            try
            {
                UnitView view = unitObject.GetComponent<UnitView>();
                InvokePrivateMethod(view, "Awake");

                view.SetWorldPosition(Vector3.zero);
                view.MoveToWorldPosition(Vector3.forward, 0.25f);
                view.BeginAttackWindup(1, 0.25f);

                InvokePrivateMethod(view, "UpdateMovement", 0.25f);

                object visualState = view.GetType()
                    .GetField("visualState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(view);
                Assert.AreEqual("Attack", visualState.ToString());
            }
            finally
            {
                Object.DestroyImmediate(unitObject);
            }
        }

        [Test]
        public void PresentationMovementDuration_MatchesTheNextPossibleSimulationStep()
        {
            float duration = BattleUnitPresenter.CalculatePresentationMovementDuration(0.4f, 0.35f);

            Assert.That(duration, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void BindPresentationState_PreservesTheExistingRuntimeUnitName()
        {
            GameObject unitObject = new GameObject("Unit", typeof(UnitView));
            UnitDefinition definition = TestDefinitions.CreateUnit("swordsman", 1);
            try
            {
                UnitView view = unitObject.GetComponent<UnitView>();
                InvokePrivateMethod(view, "Awake");
                var runtimeUnit = new RuntimeUnit(1, definition, BattleSide.Player, new HexCoord(0, 0));

                view.Bind(runtimeUnit, Vector3.zero);
                string preparationName = view.name;

                view.Bind(
                    new UnitPresentationState(1, BattlePresentationId.ForUnit(definition), BattleSide.Player, new HexCoord(0, 0), 10, 10, 0, 100),
                    Vector3.zero);

                Assert.AreEqual(preparationName, view.name);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(unitObject);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName, params object[] arguments)
        {
            target.GetType()
                .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(target, arguments);
        }
    }
}
