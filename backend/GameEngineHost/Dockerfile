# ====== Base runtime image ======
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# ASP.NET will listen on 8080
ENV ASPNETCORE_URLS=http://+:8080

# ====== Build image ======
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore
COPY GameEngine.Host.csproj .
COPY GameEngine/ GameEngine/
RUN dotnet restore GameEngine.Host.csproj

# Copy source code and configuration files
COPY . .

# Build and publish
RUN dotnet publish GameEngine.Host.csproj -c Release -o /app/publish

# ====== Final runtime image ======
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Copy game configuration files to runtime image
COPY --from=build /src/configs /app/configs
COPY --from=build /src/control-program-manifest.json /app/control-program-manifest.json

ENTRYPOINT ["dotnet", "GameEngine.Host.dll"]
