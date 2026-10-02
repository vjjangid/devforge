# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY backend/DevForge.Domain/DevForge.Domain.csproj backend/DevForge.Domain/
COPY backend/DevForge.Application/DevForge.Application.csproj backend/DevForge.Application/
COPY backend/DevForge.Infrastructure/DevForge.Infrastructure.csproj backend/DevForge.Infrastructure/
COPY worker/DevForge.Worker/DevForge.Worker.csproj worker/DevForge.Worker/
RUN dotnet restore worker/DevForge.Worker/DevForge.Worker.csproj

COPY backend/ backend/
COPY worker/ worker/
RUN dotnet publish worker/DevForge.Worker/DevForge.Worker.csproj \
    --configuration Release --no-restore --output /app/publish

# The worker serves no HTTP, so the plain .NET runtime image is enough.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# The PostgreSQL driver probes for the Kerberos library at startup and prints an error when it is
# missing. It is not needed for password login, but installing it keeps the log clean.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "DevForge.Worker.dll"]
