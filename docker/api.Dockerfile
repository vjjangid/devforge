# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so this layer is reused until dependencies change.
COPY Directory.Build.props Directory.Packages.props ./
COPY backend/DevForge.Domain/DevForge.Domain.csproj backend/DevForge.Domain/
COPY backend/DevForge.Application/DevForge.Application.csproj backend/DevForge.Application/
COPY backend/DevForge.Infrastructure/DevForge.Infrastructure.csproj backend/DevForge.Infrastructure/
COPY backend/DevForge.Api/DevForge.Api.csproj backend/DevForge.Api/
RUN dotnet restore backend/DevForge.Api/DevForge.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/DevForge.Api/DevForge.Api.csproj \
    --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is only here for the container health check.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# The base image ships an unprivileged user; do not run as root.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DevForge.Api.dll"]
