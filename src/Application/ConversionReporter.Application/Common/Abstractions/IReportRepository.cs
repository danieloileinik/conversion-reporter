using ConversionReporter.Domain.Reports;

namespace ConversionReporter.Application.Common.Abstractions;

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(Report report);
}