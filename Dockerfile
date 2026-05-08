FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

COPY ConversionReporter.slnx .
COPY src/ConversionReporter.Contracts.Events/ConversionReporter.Contracts.Events.csproj src/ConversionReporter.Contracts.Events/
COPY src/ConversionReporter.Contracts.Grpc/ConversionReporter.Contracts.Grpc.csproj src/ConversionReporter.Contracts.Grpc/
COPY src/ConversionReporter/ConversionReporter.csproj src/ConversionReporter/

RUN dotnet restore src/ConversionReporter/ConversionReporter.csproj

COPY src/ ./src/

RUN dotnet ef migrations bundle \
    --project src/ConversionReporter \
    --startup-project src/ConversionReporter \
    -o /app/publish/efbundle

WORKDIR /app/src/ConversionReporter
RUN dotnet publish ConversionReporter.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

RUN apt-get update && apt-get install -y libicu-dev && rm -rf /var/lib/apt/lists/*

EXPOSE 8080

ENTRYPOINT ["dotnet", "ConversionReporter.dll"]
