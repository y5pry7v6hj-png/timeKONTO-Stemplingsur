# timeKONTO Stemplingsur 8.5

.NET MAUI-klient for inn- og utstempling mot timeKONTO-stemplings-API.

## Funksjoner

- Henter aktive ansatte fra API.
- Inn- og utstempling med bekreftelsesdialog.
- Dato og klokkeslett kan korrigeres før inn- og utstempling.
- Kommentar kan legges inn ved innstempling og hentes/redigeres ved utstempling.
- API-adresse, API-nøkkel og administrator-PIN kan endres i Innstillinger.
- API-nøkkel bruker SecureStorage når tilgjengelig, med lokal fallback for utviklingsmiljøer.
- Kremfarget UI og pubgrønn title bar på Mac Catalyst/Windows.

## Standard administrator-PIN

`2020`

Bytt denne under Innstillinger ved første oppsett.

## Standard API-adresse

`https://admin.pts.bar/api/index.php`

API-nøkkel må legges inn under Innstillinger på den enkelte terminalen.

## Kjør på Mac i utviklingsmodus

```bash
dotnet restore
dotnet run -f net10.0-maccatalyst
```

## Lag Mac Release + PKG

```bash
chmod +x build-mac-release.sh
./build-mac-release.sh
```

Resultatet legges i `dist/`.

Se `MAC-RELEASE.md` for signering og distribusjon.
