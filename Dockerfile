# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY Directory.Build.props global.json ConstructionOps.sln ./
COPY src/ConstructionOps.Application/*.csproj src/ConstructionOps.Application/
COPY src/ConstructionOps.Infrastructure/*.csproj src/ConstructionOps.Infrastructure/
COPY src/ConstructionOps.Api/*.csproj src/ConstructionOps.Api/
# Optional public CA for development environments with a trusted HTTPS proxy.
RUN --mount=type=secret,id=proxy_ca \
    set -eu; if [ -f /run/secrets/proxy_ca ]; then cp /run/secrets/proxy_ca /usr/local/share/ca-certificates/session-proxy.crt && update-ca-certificates; fi; \
    dotnet restore src/ConstructionOps.Api/ConstructionOps.Api.csproj
COPY src/ src/
RUN dotnet publish src/ConstructionOps.Api -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ConstructionOps.Api.dll"]
