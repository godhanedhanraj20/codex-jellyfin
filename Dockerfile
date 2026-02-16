# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish Jellyfin.Server/Jellyfin.Server.csproj \
    -c Release \
    -o /app/publish \
    --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8096 \
    PORT=8096 \
    HEROKU=true \
    JELLYFIN_DISABLE_OPTIONAL_BACKGROUND_SERVICES=true \
    JELLYFIN_DATA_DIR=/app/data \
    JELLYFIN_CONFIG_DIR=/app/config \
    JELLYFIN_CACHE_DIR=/app/cache \
    JELLYFIN_LOG_DIR=/app/log \
    TRANSCODING_TEMP_PATH=/tmp/jellyfin-transcodes

RUN addgroup --gid 10001 jellyfin && \
    adduser --disabled-password --uid 10001 --gid 10001 jellyfin && \
    mkdir -p /app/data /app/config /app/cache /app/log /tmp/jellyfin-transcodes && \
    chown -R jellyfin:jellyfin /app /tmp/jellyfin-transcodes

COPY --from=build /app/publish/ ./

USER jellyfin
EXPOSE 8096

ENTRYPOINT ["./jellyfin"]
