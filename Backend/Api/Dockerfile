FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
# Create persistent data directory for SQLite (mount Railway volume here)
RUN mkdir -p /data && chmod 755 /data

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Api/Api.csproj", "Api/"]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "Api/Api.csproj"

# Change this value to force a full cache bust on COPY + build
ARG CACHEBUST=1779138818
RUN echo "Cache bust: ${CACHEBUST}"
COPY . .
WORKDIR "/src/Api"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
ARG CACHEBUST=1779138818
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
