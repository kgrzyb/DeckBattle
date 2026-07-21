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

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            target.GetType()
                .GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(target, null);
        }
    }
}
