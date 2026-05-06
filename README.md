# Artskart3
Ny versjon av Artskart, påbegynt Februar 2026 

## Requirements
* Node versjon 22.14 for Angular frontend
* Angular CLI 21.1
* Visual Studio 2026 er nødvendig for .NET 10

* installasjon av lokale nbic-map-komponent
Pakken er tilgjengelig på npm: https://www.npmjs.com/package/@artsdatabanken/nbic-map-component
Install the package from `artskart3.webapp`:

    ```bash
    npm install @artsdatabanken/nbic-map-component
    ```

## Lage commits
Alle commits til prosjektet skal være verifisert. Dette betyr at du må generere en SSH nøkkel med å skrive denne kommandoen i git bash: 

`ssh-keygen -t ed25519 -C "your_email@example.com"` 

og legger til .pub nøkkelen under signing keys og authentication keys på GitHub. 

[Connecting to GitHub with SSH](https://docs.github.com/en/authentication/connecting-to-github-with-ssh) og spesifikt seksjonen Generate new SSH Key.
## Kjøre Artskart 3 lokalt
* Kjør `gh repo clone Artsdatabanken/Artskart3` eller bruk Github Desktop for å laste ned repositoriet
### Frontend
* Åpne Artskart 3 i terminalen og kjør `cd .\Artskart3.WebApp\; npm i` for å installere pakkene i frontend
* Kjør kommandoen `ng serve` fra Artskart3/Artskart3.Webapp/ i terminalen for å starte frontend 

### Backend (API)
* Åpne løsningsfilen `Artskart3.slnx` i Visual Studio/Rider og kjør prosjektet med profilen `Artskart3.Api`
* API-et starter på `https://localhost:5088`
* Swagger UI er tilgjengelig på `https://localhost:5088/swagger`
* Helsesjekk for databasetilkobling og andre avhengigheter: `https://localhost:5088/hc`

> **NB:** Første gang du kjører API-et på HTTPS må du sette opp trust mot det lokale utviklersertifikatet:
> ```powershell
> dotnet dev-certs https --trust
> ```

#### Database
Du vil trenge en sql server database for å kjøre apiet. En .bacpac fil ligger på Teams (Artskart->General->Shared->NyeArtskart->Artskart3Index.bacpak) hvis du ønsker å starte med en database med data. Ønsker du å starte med en tom database, se Databasemigrasjoner lenger ned.

#### Sett opp User Secrets
##### Visual studio/rider
VS: Høyreklikk prosjektet og velg "Manage User Secrets"
Rider: Høyreklikk prosjektet og velg Tools->.Net user Secrets

Filen skal se noe slik ut, endre connectionstring ved behov.
```
{
  "ConnectionStrings:ArtskartDb": "data source=localhost;initial catalog=Artskart3Index;Integrated Security=true;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True",
  "ClientSafeList": "127.0.0.1;::1"
}
```

##### Command line
```powershell
cd Artskart3.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:ArtskartDb" "data source=localhost;initial catalog=Artskart3Index;Integrated Security=true;MultipleActiveResultSets=True;App=EntityFramework;TrustServerCertificate=True"
dotnet user-secrets set "ClientSafeList" "127.0.0.1;::1"
```

Valgfritt — legg til Application Insights ConnectionString hvis du skal teste det lokalt

### Databasemigrasjoner (EF Core Code-First)
Prosjektet bruker EF Core code-first migrasjoner. Alle migrasjoner kjøres fra `Artskart3.Infrastructure`-mappen.

**Forutsetninger** — installer EF Core-verktøyet globalt én gang:
```powershell
dotnet tool install --global dotnet-ef
```

**Legge til en ny migrasjon** etter å ha endret en domeneklasse:
```powershell
cd Artskart3.Infrastructure
dotnet ef migrations add <NavnPåMigrasjon> --startup-project ..\Artskart3.Api
```

**Kjøre migrasjoner mot lokal database:**
```powershell
cd Artskart3.Infrastructure
dotnet ef database update --startup-project ..\Artskart3.Api
```

**Angre siste migrasjon** (kun hvis den ikke er kjørt mot databasen ennå):
```powershell
cd Artskart3.Infrastructure
dotnet ef migrations remove --startup-project ..\Artskart3.Api
```

> Migrasjonsfilene ligger i `Artskart3.Infrastructure/Migrations/`. Ikke rediger disse manuelt etter at de er kjørt mot en delt database.

## Navngiving av branches
Standariserer navngiving av branches er `prosjektnavn-sak#-navn_på_oppgave` som for eksempel: `artskart3-sak42-project-setup-and-commits`

## Merging av endringer
For å gjøre endringer i Artskart krever det at det lages en pull request som må godkjennes av en annen utvikler.

## Oppsett av lokale DNS-oppslag
Før vi setter opp DNS-oppslag og publiserer disse offentlig, så er det greit å sette opp lokale oppslag mot de URL-ene og IP-adressene vi benytter oss av.

For de av oss som benytter Windows, er det følgende fil som gjelder: C:\Windows\System32\drivers\etc\hosts.
Innslagene du bør legge til er følgende:
* 20.251.135.164 artskart3.test.artsdatabanken.no
* 20.251.135.164 artskart3-staging.test.artsdatabanken.no
* 51.120.48.232 artskart3.artsdatabanken.no