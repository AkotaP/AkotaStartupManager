namespace AkotaStartupManager.Infrastructure.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void InfrastructureAssembly_HasExpectedName()
    {
        Assert.Equal("AkotaStartupManager.Infrastructure", typeof(Infrastructure.Persistence.PortablePathService).Assembly.GetName().Name);
    }
}
