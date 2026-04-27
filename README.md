# Artskart3
Ny versjon av Artskart, som per idag (april 2026) ligger på artskart.artsdatabanken.no (videre omtalt som Artskart 2).
Første innsats i Artskart 3 har som mål å erstatte Artskart 2, så snart det er mulig. Videre er det ambisjoner om å utvide Artskart med mer funksjonalitet enn det som finnes i Artskart 2.

Noen viktige mål
- Få bukt med ytelsesproblemer
- Ny og foredret backend som er mer modulær og oversiktlig
- Få en er moderne frontend
- Få en portal som er i henhold til Artsdatabankens designsystem og visuelle identitet

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
* Åpne løsningsfilen `Artskart3.sln` i Visual Studio og kjør prosjektet
* API-et starter på `http://localhost:5088`
* **NB:** Swagger UI er tilgjengelig på `http://localhost:5088/swagger` — bruk denne URL-en direkte og **ikke** SPA-proxy-porten (f.eks. `49219`), da SPA-proxyen vil omdirigere alle ukjente ruter til `index.html`
* Helsesjekk for databasetilkobling og andre avhengigheter: `http://localhost:5088/hc`

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
