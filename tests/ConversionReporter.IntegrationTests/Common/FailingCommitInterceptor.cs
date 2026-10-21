using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConversionReporter.IntegrationTests.Common;

public sealed class FailingCommitInterceptor : DbTransactionInterceptor
{
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction,
        TransactionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Simulated commit failure");
    }
}