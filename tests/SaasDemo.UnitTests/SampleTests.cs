using FluentAssertions;

namespace SaasDemo.UnitTests;

public class SampleTests
{
    [Fact]
    public void Addition_ReturnsExpectedSum()
    {
        var result = 2 + 2;

        result.Should().Be(4);
    }
}
