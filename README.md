# Artskart3
Ny versjon av Artskart, påbegynt Februar 2026 

## Requirements
* Node versjon 22.14 for Angular frontend
* Visual Studio 2026 er nødvendig for .NET 10

## Lage commits
Alle commits til prosjektet skal være verifisert. Dette betyr at du må generere en SSH nøkkel med å skrive denne kommandoen i git bash: 

`ssh-keygen -t ed25519 -C "your_email@example.com"` 

og legger til .pub nøkkelen under signing keys og authentication keys på GitHub. 

[Connecting to GitHub with SSH](https://docs.github.com/en/authentication/connecting-to-github-with-ssh) og spesifikt seksjonen Generate new SSH Key.

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