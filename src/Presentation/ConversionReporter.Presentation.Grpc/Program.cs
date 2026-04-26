using ConversionReporter.Application;
using ConversionReporter.Infrastructure.Caching;
using ConversionReporter.Infrastructure.Messaging;
using ConversionReporter.Infrastructure.Persistence;
using ConversionReporter.Presentation.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services
    .AddApplication()
    .AddPersistence(builder.Configuration)
    .AddCaching(builder.Configuration)
    .AddMessaging(builder.Configuration);

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<ReportService>();

app.Run();