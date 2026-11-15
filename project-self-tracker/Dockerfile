# One image for the whole app: the React SPA is built and served by the API from wwwroot (local "prod").
# Used by the `app` service in docker-compose.yml. Pinned tags, verified 15 Nov 2026.

# ---- Web (Vite build) ----
FROM node:24.18.1-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npx vite build --outDir /out/wwwroot --emptyOutDir

# ---- API (dotnet publish) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS api
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props SelfTracker.slnx .editorconfig ./
COPY src/SelfTracker.Domain/SelfTracker.Domain.csproj src/SelfTracker.Domain/
COPY src/SelfTracker.Application/SelfTracker.Application.csproj src/SelfTracker.Application/
COPY src/SelfTracker.Infrastructure/SelfTracker.Infrastructure.csproj src/SelfTracker.Infrastructure/
COPY src/SelfTracker.Api/SelfTracker.Api.csproj src/SelfTracker.Api/
RUN dotnet restore src/SelfTracker.Api/SelfTracker.Api.csproj
COPY src/ src/
COPY seed/ seed/
RUN dotnet publish src/SelfTracker.Api/SelfTracker.Api.csproj -c Release -o /app --no-restore

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /out/wwwroot ./wwwroot
# Files, backups and logs live in mounted folders (see docker-compose.yml); the app runs as the image's non-root user.
ENV ASPNETCORE_ENVIRONMENT=Production \
    SELFTRACKER_DATA=/data \
    SELFTRACKER_LISTEN_ANY=true \
    API_PORT=5080
EXPOSE 5080
ENTRYPOINT ["dotnet", "SelfTracker.Api.dll", "--migrate"]
