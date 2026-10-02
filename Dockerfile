# syntax=docker/dockerfile:1
# One image for the whole app: the backend serves the API, the SignalR hub and the built React UI on one origin.
#   docker build -t canliskor .
#   docker run -p 8080:8080 canliskor        -> http://localhost:8080

# --- 1. Frontend: static build ---------------------------------------------------------------
FROM node:22-alpine AS frontend
WORKDIR /src/frontend

# Manifests first, so the npm layer is cached until dependencies change.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# --- 2. Backend: publish, with the UI in wwwroot ---------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src/backend

# global.json is left out on purpose: it pins a local SDK feature band the image may not have;
# any .NET 10 SDK builds this.
COPY backend/src/CanliSkor.Core/CanliSkor.Core.csproj src/CanliSkor.Core/
COPY backend/src/CanliSkor.Infrastructure/CanliSkor.Infrastructure.csproj src/CanliSkor.Infrastructure/
COPY backend/src/CanliSkor.Api/CanliSkor.Api.csproj src/CanliSkor.Api/
RUN dotnet restore src/CanliSkor.Api/CanliSkor.Api.csproj

COPY backend/src/ src/
COPY --from=frontend /src/frontend/dist/ src/CanliSkor.Api/wwwroot/
RUN dotnet publish src/CanliSkor.Api/CanliSkor.Api.csproj -c Release -o /app --no-restore

# --- 3. Runtime: ASP.NET only, non-root ------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./

# The base image listens on 8080 and provides an unprivileged "app" user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CanliSkor.Api.dll"]
