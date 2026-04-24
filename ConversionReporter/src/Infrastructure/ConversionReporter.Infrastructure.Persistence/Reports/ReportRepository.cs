using ConversionReporter.Application.Common.Abstractions;
using ConversionReporter.Domain.Reports;
using ConversionReporter.Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;

namespace ConversionReporter.Infrastructure.Persistence.Reports;

public class ReportRepository(AppDbContext dbContext) : IReportRepository
{
    public async Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Reports
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public void Add(Report report)
    {
        dbContext.Reports.Add(report);
    }
}