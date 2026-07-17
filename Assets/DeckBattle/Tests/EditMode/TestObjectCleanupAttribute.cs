using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: DeckBattle.Tests.TestObjectCleanupAttribute]

namespace DeckBattle.Tests
{
    public sealed class TestObjectCleanupAttribute : TestActionAttribute
    {
        public override ActionTargets Targets
        {
            get { return ActionTargets.Test; }
        }

        public override void AfterTest(ITest test)
        {
            TestDefinitions.DestroyCreatedObjects();
        }
    }
}
