---
name: Feature template
about: With definition of done checklist
title: ''
labels: ''
assignees: ''

---

### Definition of done

Stryk ut de punktene som ikke er relevante.
Kryss av de som er håndtert.

👩‍💻 Funksjonalitet og brukergrensesnitt
- [ ] Løsningen dekker akseptansekriteriene/brukerbehovene i epicen
- [ ] Eventuelle edge cases er vurdert
- [ ] Data hentes med riktig språk
- [ ] Er universelt utformet
- [ ] Følger retningslinjer fra design system
- [ ] Klar for store og små skjermer (evergreen browsers, iOS og Android)
- [ ] Fungerer i lys og mørk modus
- [ ] Alle tekststrenger er knyttet mot språkstyring
- [ ] Ingen konsollfeil eller advarsler

🏅 Kode- og datakvalitet
- [ ] Koden følger teamets kode‑standard
- [ ] Ingen kjente TODO/DEBUG‑rester
- [ ] Data er i henhold til standarder (FAIR, GBIF)

🤖 Automatiserte tester
- [ ] Tester er implementert (unit / integrasjon)
- [ ] Alle tester er kjørt og grønne
- [ ] Testene gir faktisk verdi (ikke bare for coverage)

🏗️ Bygg & pipeline
- [ ] Applikasjonen passerer CI‑pipeline uten feil

📝 Dokumentasjon
Endringer som påvirker følgende er dokumentert:
- [ ] API
- [ ] konfigurering
- [ ] data/skjema
- [ ] Kompleks eller ikke-intuitiv logikk i kode er dokumentert

🕵️ Ikke‑funksjonelle krav
- [ ] Ytelse er testet (kode og endepunkter)
- [ ] Feilhåndtering er på plass (eks tomme datasett, null-verdier)
- [ ] Sikkerhet er vurdert opp mot risiko
- [ ] Logging/metrics er lagt til

🪡 Kompatibilitet & migrering
- [ ] Bakoverkompatibilitet bekreftet (eller migreringsstrategi dokumentert)
- [ ] Databasemigreringsarbeid testet
- [ ] Endringer i miljøvariabler/konfigurasjon dokumentert
- [ ] Ingen breaking changes i offentlige API-er (eller tydelig merket som breaking)

✅ Klar for release
- [ ] Løsningen holder den kvaliteten vi forventer av hverandre i teamet
- [ ] Oppleves ikke av brukeren eller andre som uferdig
