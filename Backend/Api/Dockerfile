FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
RUN mkdir -p /data && chmod 755 /data
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY ["Api/Api.csproj", "Api/"]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "Api/Api.csproj" --no-cache
COPY . .
RUN dotnet publish "Api/Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-incremental

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
