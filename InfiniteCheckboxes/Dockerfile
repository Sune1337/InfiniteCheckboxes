FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Install Node.js
RUN set -uex; \
    apt-get update; \
    apt-get install -y ca-certificates curl gnupg; \
    mkdir -p /etc/apt/keyrings; \
    curl -fsSL https://deb.nodesource.com/gpgkey/nodesource-repo.gpg.key | gpg --dearmor -o /etc/apt/keyrings/nodesource.gpg; \
    NODE_MAJOR=22; \
    echo "deb [signed-by=/etc/apt/keyrings/nodesource.gpg] https://deb.nodesource.com/node_$NODE_MAJOR.x nodistro main" > /etc/apt/sources.list.d/nodesource.list; \
    apt-get update; \
    apt-get install nodejs -y; \
    rm -rf /var/lib/apt/lists/*

ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["InfiniteCheckboxes/", "InfiniteCheckboxes/"]
COPY ["CheckboxHubv1/", "CheckboxHubv1/"]
COPY ["WarHubv1/", "WarHubv1/"]
COPY ["GrainInterfaces/", "GrainInterfaces/"]
COPY ["RedisMessages/", "RedisMessages/"]
COPY ["Two56BitId/", "Two56BitId/"]
COPY ["ValueDebouncer/", "ValueDebouncer/"]
RUN dotnet restore "InfiniteCheckboxes/InfiniteCheckboxes.csproj"
WORKDIR "/src/InfiniteCheckboxes"
RUN dotnet build "./InfiniteCheckboxes.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./InfiniteCheckboxes.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "InfiniteCheckboxes.dll"]
