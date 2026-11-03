# Byggkontekst er reporoten, ikke denne mappa:
#   podman build -f Artskart3.Api/Dockerfile -t artskart3-api .
#
# Rent API. Frontend bygges og serveres av web-containeren, se
# Artskart3.WebApp/Dockerfile.
#
# MERK: Program.cs serverer fortsatt statiske filer fra wwwroot hvis de finnes.
# Det er med vilje - dagens App Service-utrulling legger Angular-bygget der via
# pipelinen, og den må fortsette å virke til flyttingen er fullført. Imaget her
# har ingen wwwroot, så kodestien er uvirksom i container.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
# Prosjektfilene først, slik at restore kan caches uavhengig av kodeendringer.
# .editorconfig må med: Directory.Build.props slår på EnforceCodeStyleInBuild
# sammen med TreatWarningsAsErrors, så reglene må være de samme som lokalt.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY Artskart3.Core/Artskart3.Core.csproj Artskart3.Core/
COPY Artskart3.Infrastructure/Artskart3.Infrastructure.csproj Artskart3.Infrastructure/
COPY Artskart3.Api/Artskart3.Api.csproj Artskart3.Api/
RUN dotnet restore Artskart3.Api/Artskart3.Api.csproj

COPY Scripts/ Scripts/
COPY Artskart3.Core/ Artskart3.Core/
COPY Artskart3.Infrastructure/ Artskart3.Infrastructure/
COPY Artskart3.Api/ Artskart3.Api/
RUN dotnet publish Artskart3.Api/Artskart3.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=backend /app ./
# Ingen HTTPS i containeren; gateway/ingress terminerer TLS.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Artskart3.Api.dll"]
