using Xunit;

namespace Launcher.Common.Tests
{
    public class UtilsTest
    {
        [Theory]
        [InlineData("foo", "\"foo\"")]
        [InlineData(@"""bar""", @"""\""bar\""""")]
        [InlineData(@"\""baz\""", @"""\\\""baz\\\""""")]
        public void EscapeShellArgument(string original, string expected)
        {
            Assert.Equal(expected, Utils.EscapeShellArgument(original));
        }
    }
}
