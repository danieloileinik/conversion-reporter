FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /app

COPY ConversionReporter.sln .

COPY src/Domain/ConversionReporter.Domain/ConversionReporter.Domain.csproj src/Domain/ConversionReporter.Domain/
COPY src/Application/ConversionReporter.Application.Contracts/ConversionReporter.Application.Contracts.csproj src/Application/ConversionReporter.Application.Contracts/
COPY src/Application/ConversionReporter.Application/ConversionReporter.Application.csproj src/Application/ConversionReporter.Application/
COPY src/Infrastructure/ConversionReporter.Infrastructure.Persistence/ConversionReporter.Infrastructure.Persistence.csproj src/Infrastructure/ConversionReporter.Infrastructure.Persistence/
COPY src/Infrastructure/ConversionReporter.Infrastructure.Caching/ConversionReporter.Infrastructure.Caching.csproj src/Infrastructure/ConversionReporter.Infrastructure.Caching/
COPY src/Infrastructure/ConversionReporter.Infrastructure.Messaging/ConversionReporter.Infrastructure.Messaging.csproj src/Infrastructure/ConversionReporter.Infrastructure.Messaging/
COPY src/Presentation/ConversionReporter.Presentation.Grpc/ConversionReporter.Presentation.Grpc.csproj src/Presentation/ConversionReporter.Presentation.Grpc/

RUN dotnet restore src/Presentation/ConversionReporter.Presentation.Grpc/ConversionReporter.Presentation.Grpc.csproj

COPY src/ ./src/

WORKDIR /app/src/Presentation/ConversionReporter.Presentation.Grpc
RUN dotnet publish ConversionReporter.Presentation.Grpc.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "ConversionReporter.Presentation.Grpc.dll"]