namespace AkotaStartupManager.Core.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void CoreAssembly_HasExpectedName()
    {
        Assert.Equal("AkotaStartupManager.Core", typeof(Core.Models.ManagedStartupEntry).Assembly.GetName().Name);
    }
}
