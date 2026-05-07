using SharedKernel;


namespace OpenIdentityStack.Infrastructure.Tests.Common;

public sealed class DateTimeProviderTests
{
    [Fact]
    public void UtcNow_ReturnsUtcTime()
    {
        var provider = new DateTimeProvider();

        DateTimeOffset result = provider.UtcNow;

        result.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Now_ReturnsLocalTime()
    {
        var provider = new DateTimeProvider();

        DateTimeOffset result = provider.Now;

        result.ShouldNotBe(default(DateTimeOffset));
    }
}
