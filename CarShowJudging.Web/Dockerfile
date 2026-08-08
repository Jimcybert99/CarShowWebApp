# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CarShowJudging.sln .
COPY CarShowJudging.Core/CarShowJudging.Core.csproj CarShowJudging.Core/
COPY CarShowJudging.Infrastructure/CarShowJudging.Infrastructure.csproj CarShowJudging.Infrastructure/
COPY CarShowJudging.Web/CarShowJudging.Web.csproj CarShowJudging.Web/
RUN dotnet restore CarShowJudging.Web/CarShowJudging.Web.csproj

COPY CarShowJudging.Core/ CarShowJudging.Core/
COPY CarShowJudging.Infrastructure/ CarShowJudging.Infrastructure/
COPY CarShowJudging.Web/ CarShowJudging.Web/
RUN dotnet publish CarShowJudging.Web/CarShowJudging.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN groupadd -r appuser && useradd -r -g appuser appuser \
    && mkdir -p /data /app/wwwroot/uploads/vehicles \
    && chown -R appuser:appuser /data /app
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__DefaultConnection="Data Source=/data/carshow.db"
EXPOSE 8080
VOLUME ["/data", "/app/wwwroot/uploads/vehicles"]

ENTRYPOINT ["dotnet", "CarShowJudging.Web.dll"]
