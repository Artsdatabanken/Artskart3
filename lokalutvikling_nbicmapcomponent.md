# Lokal utvikling: nbic-map-component med Artskart3

## Forutsetninger

Sørg for at du har følgende installert:
- **Node.js 24 LTS, minst 24.15.0** (Angular 22 krever `^22.22.3 || ^24.15.0 || >=26`)
- **npm** (følger med Node)
- **Git** (for å klone repositorier)
- **Angular**: 22
- **Visual Studio 2026** (for .NET 10 backend)

## Oppsett: Klone begge repositorier

Begge repositoriene må ligge i samme mappe, siden stiene under er relative:
- `c:\Users\xxxx\source\repos\ArtsKart3\Artskart3`
- `c:\Users\xxxx\source\repos\ArtsKart3\nbic-map-component`

## Første gangs oppsett

### Steg 1: Installer avhengigheter i nbic-map-component

```bash
cd nbic-map-component
npm install
```

`npm install` bygger også `dist/` (via `prepare`-scriptet):
- `dist/index.js` — ES module bygg
- `dist/index.cjs` — CommonJS bygg
- `dist/index.d.ts` — TypeScript typedefinisjon

### Steg 2: Installer avhengigheter i Artskart3.WebApp

```bash
cd ..\Artskart3\Artskart3.WebApp
npm install
```

---

## Bruke lokal versjon av komponenten i Artskart3

Artskart3 installerer komponenten som en pakket `.tgz`-fil, akkurat slik den blir publisert.

### Steg 1: Pakk komponenten

```bash
cd nbic-map-component
npm pack
```

Dette bygger `dist/` og lager `artsdatabanken-nbic-map-component-<versjon>.tgz` (f.eks. `artsdatabanken-nbic-map-component-0.3.0.tgz`). Filen er ignorert av git.

### Steg 2: Installer pakken i Artskart3

```bash
cd ..\Artskart3\Artskart3.WebApp
npm install ../../nbic-map-component/artsdatabanken-nbic-map-component-0.3.0.tgz
```

`package.json` peker nå på `file:../../nbic-map-component/artsdatabanken-nbic-map-component-0.3.0.tgz`.

- **Ikke commit** `package.json` og `package-lock.json` mens de peker på `.tgz`-filen.
- Advarselen `allow-scripts … (prepare: npm run build)` kan ignoreres, fordi `dist/` allerede ligger i `.tgz`-filen.

### Hvorfor ikke `file:../../nbic-map-component`?

Med en mappe-lenke installerer npm komponentens avhengigheter (`ol`, `proj4` m.fl.) i komponentens egen `node_modules`. Artskart3 får da enten ingen `ol` eller to kopier av den. Kartkomponenten registrerer EPSG:25833 i sin egen kopi, så `transform(…, 'EPSG:25833')` i Artskart3 og lag eller kontroller som Artskart3 sender inn, kan feile. Med `.tgz` deler Artskart3 og komponenten én `ol`.

### Gå tilbake til publisert versjon

```bash
cd Artskart3\Artskart3.WebApp
git checkout -- package.json package-lock.json
npm install
```

---

## Kjøre Artskart3

```bash
cd Artskart3\Artskart3.WebApp
npm start
```

`npm start` kjører `ng serve --ssl` med dev-sertifikatet fra ASP.NET:
- Windows: `%APPDATA%\ASP.NET\https\artskart3.webapp.pem` / `.key`
- Mac/Linux: `$HOME/.aspnet/https/artskart3.webapp.pem` / `.key`

Åpne **https://localhost:49219** (porten er satt i `angular.json`). Sertifikatet gjelder bare `localhost`, så `https://127.0.0.1:49219` gir sertifikatfeil.

## Arbeidsflyt: Gjøre og teste endringer

1. Gjør endringer i `nbic-map-component/src/`.
2. Pakk på nytt: `npm pack` (i nbic-map-component).
3. Installer på nytt i Artskart3.WebApp, med samme kommando som i steg 2 over. npm plukker opp det nye innholdet selv om versjonsnummeret er det samme.
4. Start `npm start` på nytt. Ved oppstart ser Angular/Vite at lockfilen er endret og bygger avhengighetene på nytt.

For å utvikle og teste komponenten alene, uten Artskart3, bruk playgrounden: `npm run dev` i nbic-map-component (Vite med live reload). Artskart3 ser ikke endringer derfra før du har pakket og installert på nytt.

## Test

- Komponenten: `npm test` eller `npm run test:watch` (i nbic-map-component)
- Artskart3: `npm run lint`, `npm run test -- --watch=false` og `npm run build` (i Artskart3.WebApp)
- Test i Artskart-grensesnittet og sjekk nettleserkonsollen for feil
