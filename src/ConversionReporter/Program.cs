using ConversionReporter;
using ConversionReporter.Features.Reports.Queries.GetReport;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
    options.ConfigureEndpointDefaults(endpoint => endpoint.Protocols = HttpProtocols.Http2));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddGrpc();

var app = builder.Build();
app.MapGrpcService<ReportService>();
app.Run();