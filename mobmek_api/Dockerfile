# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore first (better layer caching)
COPY src/MobmekApi/MobmekApi.csproj src/MobmekApi/
RUN dotnet restore src/MobmekApi/MobmekApi.csproj

# Copy the rest and publish
COPY src/ src/
RUN dotnet publish src/MobmekApi/MobmekApi.csproj -c Release -o /app --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MobmekApi.dll"]
