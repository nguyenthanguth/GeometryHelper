#nullable enable
using Xunit;

namespace Clipper2Lib.UnitTests
{
    /// <summary>
    /// Clipper2's own test of offsets (CSharp/Tests/Tests1/Tests/TestOffset.cs of 2.0.0), from MSTest to xUnit:
    /// offsetting nothing does not throw.
    /// </summary>
    public class TestOffsets
    {
        [Fact]
        public void TestOffsetEmpty()
        {
            Paths64 solution = new();

            ClipperOffset offset = new ClipperOffset();
            offset.Execute(10, solution);
        }
    }
}
