FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/GameBug.Domain/GameBug.Domain.csproj src/GameBug.Domain/
COPY src/GameBug.Contracts/GameBug.Contracts.csproj src/GameBug.Contracts/
COPY src/GameBug.Application/GameBug.Application.csproj src/GameBug.Application/
COPY src/GameBug.Infrastructure/GameBug.Infrastructure.csproj src/GameBug.Infrastructure/
COPY src/GameBug.Api/GameBug.Api.csproj src/GameBug.Api/

RUN dotnet restore src/GameBug.Api/GameBug.Api.csproj

COPY src/ src/
COPY screenshots/ screenshots/
RUN dotnet publish src/GameBug.Api/GameBug.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/evaluation/artifacts

COPY --from=build /app/publish ./
COPY evaluation ./evaluation
RUN chown -R $APP_UID:0 /app

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "GameBug.Api.dll"]
