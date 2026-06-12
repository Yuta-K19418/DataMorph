using AwesomeAssertions;
using DataMorph.App.Views.JsonRangeTreeNodes;
using DataMorph.App.Views.JsonTreeNodes;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App.Views.JsonRangeTreeNodes;

public sealed class RangeTreeNodeBaseTests
{
    [Fact]
    public void EnsureChildrenLoaded_OnSuccess_IsIdempotent()
    {
        // Arrange
        var node = new StubRangeNode { ChildrenToAdd = 4 };

        // Act
        node.EnsureChildrenLoaded();
        node.EnsureChildrenLoaded();

        // Assert — LoadChildren runs once; second call is a no-op
        node.IsChildrenLoaded.Should().BeTrue();
        node.Children.Should().HaveCount(4);
    }

    private sealed class StubRangeNode : RangeTreeNodeBase
    {
        internal StubRangeNode()
        {
            Text = "stub";
        }

        internal int ChildrenToAdd { get; set; }

        protected override void LoadChildren()
        {
            List<ITreeNode> children = [];

            for (var i = 0; i < ChildrenToAdd; i++)
            {
                children.Add(new JsonValueTreeNode($"child {i}"));
            }

            Children = children;
        }
    }
}
