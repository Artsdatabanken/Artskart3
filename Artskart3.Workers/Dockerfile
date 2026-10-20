# Byggkontekst er reporoten, ikke denne mappa:
#   podman build -f Artskart3.Workers/Dockerfile -t artskart3-workers .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY Artskart3.Core/Artskart3.Core.csproj Artskart3.Core/
COPY Artskart3.Infrastructure/Artskart3.Infrastructure.csproj Artskart3.Infrastructure/
COPY Artskart3.Workers/Artskart3.Workers.csproj Artskart3.Workers/
RUN dotnet restore Artskart3.Workers/Artskart3.Workers.csproj

COPY Scripts/ Scripts/
COPY Artskart3.Core/ Artskart3.Core/
COPY Artskart3.Infrastructure/ Artskart3.Infrastructure/
COPY Artskart3.Workers/ Artskart3.Workers/
RUN dotnet publish Artskart3.Workers/Artskart3.Workers.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=backend /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Artskart3.Workers.dll"]
