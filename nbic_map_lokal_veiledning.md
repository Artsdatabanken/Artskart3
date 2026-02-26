# Lokal utvikling med NBIC Map Component

## Oversikt

Denne dokumentasjonen beskriver hvordan du setter opp og arbeider lokalt med `nbic-map.component` i Artskart3-prosjektet. Dette er nyttig når du skal utvikle eller endre kartkomponenten.

## Trinn 1: Klone og bygge NBIC Map Component

### 1.1 Klone repositoriet

```bash
git clone https://github.com/Artsdatabanken/nbic-map-component.git
cd nbic-map-component
```

### 1.2 Installer avhengigheter

```bash
npm install
```

### 1.3 Bygg komponenten

```bash
npm run build
```

### 1.4 Pakk komponenten

```bash
npm pack
```

Dette genererer en `.tgz`-fil (f.eks. `nbic-map-component-1.0.0.tgz`) i root-mappen.

## Trinn 2: Konfigurer Artskart3 for lokal utvikling

### 2.1 Installer den lokale pakken

I `artskart3` prosjektet, naviger til client-mappen:

```bash
cd artskart3.client
```

Installer pakken med full sti:

```bash
npm install file:../../../nbic-map-component/nbic-map-component-1.0.0.tgz
```

### 2.2 Verifiser installasjon

Sjekk `package.json` i `artskart3.client` - du skal se en linje som ligner:

```json
"dependencies": {
  "nbic-map-component": "file:../../../nbic-map-component/nbic-map-component-1.0.0.tgz",
}
```

## Trinn 3: Lokal utvikling og testing

### 3.1 Start watch-modus i Artskart3

I `artskart3.client` mappen:

```bash
npm run watch
```

Dette gjenoppbygger Client-koden når du gjør endringer.