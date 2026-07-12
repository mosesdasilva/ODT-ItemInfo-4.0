using ItemInfo.Recoloring;
using Xunit;

namespace ItemInfo.Tests;

public class StaticRecolorPassRunnerTests
{
    [Fact]
    public void Delayed_lifecycle_invokes_the_static_recolor_pass_exactly_once()
    {
        var runner = new StaticRecolorPassRunner();
        var invocations = 0;

        Assert.True(runner.RunOnce(() => invocations++));
        Assert.False(runner.RunOnce(() => invocations++));
        Assert.Equal(1, invocations);
    }
}
