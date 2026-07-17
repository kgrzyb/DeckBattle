#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DeckBattle.Tests
{
    public static class DeckBattleEditorTestRunner
    {
        private const string EditModeTestAssembly = "DeckBattle.EditModeTests";

        [MenuItem("DeckBattle/Tests/Run EditMode Tests")]
        public static void RunEditModeTests()
        {
            TestRunnerApi testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = TestMode.EditMode,
                assemblyNames = new[] { EditModeTestAssembly }
            };

            testRunnerApi.Execute(new ExecutionSettings(filter));
        }
    }
}
#endif
