using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class UnitAnimatorControllerTests
    {
        private const string ControllerPath = "Assets/DeckBattle/Art/Animations/Units/UnitAnimatorController.controller";

        [Test]
        public void RunState_UsesDedicatedRunSpeedMultiplierParameter()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            Assert.IsNotNull(controller);
            Assert.IsTrue(HasFloatParameter(controller, "runSpeed", 1f));

            AnimatorState[] states = GetStates(controller.layers[0].stateMachine.states);
            AnimatorState runState = FindState(states, "Run");
            Assert.IsNotNull(runState);
            Assert.IsTrue(runState.speedParameterActive);
            Assert.AreEqual("runSpeed", runState.speedParameter);

            Assert.IsFalse(FindState(states, "Idle").speedParameterActive);
            Assert.IsFalse(FindState(states, "Attack").speedParameterActive);
            Assert.IsFalse(FindState(states, "Special").speedParameterActive);
            Assert.IsFalse(FindState(states, "Dead").speedParameterActive);
        }

        private static bool HasFloatParameter(AnimatorController controller, string name, float defaultValue)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.name == name)
                {
                    return parameter.type == AnimatorControllerParameterType.Float
                        && Mathf.Approximately(parameter.defaultFloat, defaultValue);
                }
            }

            return false;
        }

        private static AnimatorState FindState(AnimatorState[] states, string stateName)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && states[i].name == stateName)
                {
                    return states[i];
                }
            }

            return null;
        }

        private static AnimatorState[] GetStates(ChildAnimatorState[] childStates)
        {
            var states = new AnimatorState[childStates.Length];
            for (int i = 0; i < childStates.Length; i++)
            {
                states[i] = childStates[i].state;
            }

            return states;
        }
    }
}
