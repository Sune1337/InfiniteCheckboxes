FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SiloHost/", "SiloHost/"]
COPY ["CheckboxGrain/", "CheckboxGrain/"]
COPY ["WarGrain/", "WarGrain/"]
COPY ["StatisticsGrain/", "StatisticsGrain/"]
COPY ["GoldDiggerGrain/", "GoldDiggerGrain/"]
COPY ["UserGrain/", "UserGrain/"]
COPY ["HighscoreGrain/", "HighscoreGrain/"]
COPY ["MinesweeperGrain/", "MinesweeperGrain/"]
COPY ["GrainInterfaces/", "GrainInterfaces/"]
COPY ["RedisMessages/", "RedisMessages/"]
COPY ["Two56BitId/", "Two56BitId/"]
COPY ["BitCoding/", "BitCoding/"]
COPY ["ValueDebouncer/", "ValueDebouncer/"]
COPY ["RngWithSecret/", "RngWithSecret/"]
RUN dotnet restore "SiloHost/SiloHost.csproj"
WORKDIR "/src/SiloHost"
RUN dotnet build "./SiloHost.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SiloHost.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SiloHost.dll"]
