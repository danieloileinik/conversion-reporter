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

    protected static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!await condition())
            await Task.Delay(50, timeout.Token);
    }
}