# Notifications (varsler)

`notifications.json` er datakilden for varslene som vises i Artskart-portalen (f.eks. driftsmeldinger, planlagt vedlikehold og annen viktig informasjon til brukerne). Filen redigeres manuelt av en superbruker — det finnes ingen admin-UI for dette per nå.

Endepunktet `GET /api/notifications` leser filen ved hver forespørsel, så en endring blir synlig for klienten umiddelbart etter lagring (ingen restart av API-et er nødvendig).

## Slik legger du til et nytt varsel

Åpne `notifications.json` og legg til et nytt objekt i `notifications`-listen. Husk komma mellom elementene.

```json
{
  "type": "Info",
  "heading": "Kort overskrift for varselet",
  "description": "Lengre forklarende tekst som vises til brukeren.",
  "startDateTime": "2026-09-01T08:00:00",
  "endDateTime": "2026-09-01T16:00:00",
  "startDisplayDate": "2026-08-28",
  "endDisplayDate": "2026-09-01",
  "canClose": true
}
```

### Feltbeskrivelse

| Felt | Type | Påkrevd | Beskrivelse |
|---|---|---|---|
| `type` | tekst | Ja | Type varsel. Gyldige verdier: `Danger`, `Warning`, `Info`, `Success`, `Neutral`. Se [`AlertType`](../../../Artskart3.Core/Domain/Enums/AlertType.cs). Styrer ikon/farge i frontend. |
| `heading` | tekst | Ja | Kort overskrift på varselet. |
| `description` | tekst | Ja | Utfyllende tekst som vises sammen med overskriften. |
| `startDateTime` | dato/klokkeslett eller `null` | Nei | Når selve hendelsen (f.eks. vedlikeholdet) starter. Kun informativ — styrer ikke om varselet vises. |
| `endDateTime` | dato/klokkeslett eller `null` | Nei | Når selve hendelsen avsluttes. Kun informativ — styrer ikke om varselet vises. |
| `startDisplayDate` | dato | Ja | Datoen varselet begynner å vises for brukerne i portalen. |
| `endDisplayDate` | dato | Ja | Datoen varselet slutter å vises for brukerne i portalen. |
| `canClose` | `true`/`false` | Ja | Om brukeren kan lukke varselet selv (`true`) eller om det alltid skal vises i perioden (`false`). |

### Datoformat

* Dato og klokkeslett (`startDateTime`, `endDateTime`): ISO 8601, `ÅÅÅÅ-MM-DDTtt:mm:ss`, f.eks. `"2026-09-01T08:00:00"`.
* Bare dato (`startDisplayDate`, `endDisplayDate`): `ÅÅÅÅ-MM-DD`, f.eks. `"2026-09-01"`.
* Hvis hendelsen ikke har et bestemt tidspunkt, sett `startDateTime`/`endDateTime` til `null` i stedet for å utelate feltet.

## Fjerne eller avslutte et varsel

Sett `endDisplayDate` til en dato som allerede har passert, eller fjern hele objektet fra listen.

## Validering før lagring

* Filen må være gyldig JSON — pass på komma, anførselstegn og krøllparenteser. Bruk gjerne en JSON-validator (f.eks. i VS Code) før du lagrer.
* `type` må være stavet nøyaktig som en av de fem gyldige verdiene ovenfor (case-sensitiv).
* Test gjerne endringen lokalt ved å kalle `GET /api/notifications` mot en lokal API-instans før du oppdaterer produksjonsfilen.
