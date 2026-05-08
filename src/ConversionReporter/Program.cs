using ConversionReporter;
using ConversionReporter.Features.Reports.Queries.GetReport;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddGrpc();

var app = builder.Build();
app.MapGrpcService<ReportService>();
app.Run();