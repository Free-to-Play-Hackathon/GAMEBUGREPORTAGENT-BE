FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/GameBug.Domain/GameBug.Domain.csproj src/GameBug.Domain/
COPY src/GameBug.Contracts/GameBug.Contracts.csproj src/GameBug.Contracts/
COPY src/GameBug.Application/GameBug.Application.csproj src/GameBug.Application/
COPY src/GameBug.Infrastructure/GameBug.Infrastructure.csproj src/GameBug.Infrastructure/
COPY src/GameBug.Worker/GameBug.Worker.csproj src/GameBug.Worker/

RUN dotnet restore src/GameBug.Worker/GameBug.Worker.csproj

COPY src/ src/
RUN dotnet publish src/GameBug.Worker/GameBug.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
RUN chown -R $APP_UID:0 /app

USER $APP_UID
ENTRYPOINT ["dotnet", "GameBug.Worker.dll"]
