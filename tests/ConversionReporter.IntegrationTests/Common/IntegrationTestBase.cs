namespace ConversionReporter.IntegrationTests.Common;

[Collection("IntegrationTests")]
public abstract class IntegrationTestBase(IntegrationTestFixture fixture) : IAsyncLifetime
{
    protected IServiceProvider Services => fixture.Services;

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}