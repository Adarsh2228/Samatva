# ─── Stage 1: Build ───────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["SplitWisePro.API/SplitWisePro.API.csproj", "SplitWisePro.API/"]
COPY ["SplitWisePro.Core/SplitWisePro.Core.csproj", "SplitWisePro.Core/"]
COPY ["SplitWisePro.Infrastructure/SplitWisePro.Infrastructure.csproj", "SplitWisePro.Infrastructure/"]
RUN dotnet restore "SplitWisePro.API/SplitWisePro.API.csproj"

# Copy all source and publish
COPY . .
WORKDIR /src/SplitWisePro.API
RUN dotnet publish "SplitWisePro.API.csproj" -c Release -o /app/publish --no-restore

# ─── Stage 2: Runtime ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Render uses PORT env variable
ENV ASPNETCORE_URLS=http://+:${PORT:-10000}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

# Run EF migrations at startup then start API
ENTRYPOINT ["dotnet", "SplitWisePro.API.dll"]
