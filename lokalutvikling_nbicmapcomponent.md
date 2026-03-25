# Lokal utvikling: nbic-map-component med Artskart3

## Forutsetninger

Sørg for at du har følgende installert:
- **Node.js 22.14+** (påkrevd for Angular frontend)
- **npm 10.9.2+** (angitt i package.json)
- **Git** (for å klone repositorier)
- **Angular**: 19+
- **Visual Studio 2026** (for .NET 10 backend)

## Oppsett: Klone begge repositorier

### Steg 1: Sikre at begge repositorier er klonet og verifisert

Hvis du ikke allerede har gjort det, klone begge repositorier til din lokale maskin:

```bash
# Naviger til arbeidsmappe-katalogen din
# Verifiser at begge mapper eksisterer (de burde gjøre det basert på arbeidsstrukturen)
# - Artskart3/
# - nbic-map-component/
```

Begge repositorier burde allerede være klonet til:
- `c:\Users\xxxx\source\repos\ArtsKart3\Artskart3`
- `c:\Users\xxxx\source\repos\ArtsKart3\nbic-map-component`

## Første gangs oppsett

### Steg 1: Installer avhengigheter i nbic-map-component

```bash
cd nbic-map-component
npm install
```

Dette installerer alle utviklingsavhengigheter som trengs for å bygge og teste komponentbiblioteket.

### Steg 2: Bygg nbic-map-component

```bash
npm run build
```

Dette genererer `dist/`-mappen med:
- `dist/index.js` — ES module bygg
- `dist/index.cjs` — CommonJS bygg
- `dist/index.d.ts` — TypeScript typedefinisjon

**Viktig:** Hver gang du gjør endringer i komponentkildekoden, må du kjøre `npm run build` for å generere nytt output.

### Steg 3: Installer avhengigheter i Artskart3.WebApp

```bash
cd ..\Artskart3\Artskart3.WebApp
npm install
```

---

## Metoder for å lenke lokal pakke

### Metode: Bruk `file:` referanse i package.json

#### Steg 1: Rediger package.json

I `Artskart3.WebApp\package.json`, endre avhengighetslinja:

```json
{
  "dependencies": {
    "@artsdatabanken/nbic-map-component": "file:../../nbic-map-component",
    ...
  }
}
```

**Viktig:** Bruk relativstien fra WebApp-mappen til nbic-map-component-mappen (juster stien etter din mappestruktur).

#### Steg 2: Oppdater node_modules

```bash
cd Artskart3.WebApp
npm install
```

Dette oppretter en lenke i `node_modules` som peker til din lokale nbic-map-component.

#### Verifiser lenken

Kontroller at `node_modules/@artsdatabanken/nbic-map-component` peker til din lokale mappe:

```bash
# På Windows
dir node_modules\@artsdatabanken\nbic-map-component

# Du burde se de lokale kildefilene, ikke bare dist/
```
For å bekrefte at det bruker den lokale versjonen, sjekk den faktiske node_modules-oppføringen:
```bash
# Vis package.json som ble løst
cat node_modules/@artsdatabanken/nbic-map-component/package.json
```

#### Gå tilbake til publisert versjon

Endre `package.json` tilbake til:

```json
{
  "dependencies": {
    "@artsdatabanken/nbic-map-component": "^0.2.2",
    ...
  }
}
```

Og installer på nytt:
```bash
npm install
```

---

## Kjøre begge prosjekter i utviklingsmodus

### Metode: Tilnærming med to terminaler

#### Terminal 1: Komponenten i utviklingsmodus

**Alternativ A: Live reload med Vite dev server**
```bash
cd nbic-map-component
npm run dev
```
Dette kjører Vite-utviklingsserveren for componentene playground med instant reload under utvikling.

**Alternativ B: Enkelt bygg etter endringer**
```bash
cd nbic-map-component
npm run build
```
Dette genererer nye filer i `dist/` som kan testes i Artskart3.

**Anbefaling:** Bruk `npm run dev` for løpende utvikling. Bruk `npm run build` når du skal teste endringene i Artskart3-appen.

#### Terminal 2: Kjør Artskart WebApp i utviklingsmodus

```bash
cd Artskart3\Artskart3.WebApp
npm start
```

På Windows kjører dette kommandoen:
```
ng serve --ssl --ssl-cert "%APPDATA%\ASP.NET\https\artskart3.webapp.pem" --ssl-key "%APPDATA%\ASP.NET\https\artskart3.webapp.key" --host=127.0.0.1
```

Angular-utviklingsserveren er tilgjengelig på `https://127.0.0.1:4200` (sjekk output for faktisk port).

## Arbeidsflyt: Gjøre og teste endringer

### Steg 1: Gjør endringer i nbic-map-component

Rediger kildefiler i `nbic-map-component/src/`:

```typescript
// nbic-map-component/src/din-fil.ts
// Gjør dine endringer her
```

### Steg 2: Bygg komponenten

```bash
cd nbic-map-component
npm run build
```

Dette genererer nye filer i `dist/`.

### Steg 3: Test i Artskart

Angular-utviklingsserveren oppdager endringer og hot-reloader

Hvis endringer ikke vises:
1. Sjekk nettleserkonsollen for feil
2. Tøm nettlesercache (Ctrl+Shift+Delete)
3. Hard refresh (Ctrl+Shift+R)
4. Start Angular dev-serveren på nytt

### Steg 4: Test grundig

- Test i Artskart-grensesnittet
- Kjør komponenttester: `npm run test:watch` (i nbic-map-component)
- Kjør Artskart-tester: `npm test` (i Artskart3.WebApp)
- Sjekk nettleserkonsollen for feil
- Verifiser at TypeScript-typer er korrekte
