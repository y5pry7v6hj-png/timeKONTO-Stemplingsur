# Mac Release / installer

`build-mac-release.sh` bygger timeKONTO Stemplingsur 8.5 for Apple Silicon (M1/M2/M3/M4/M5) og lager en `.pkg` som installerer appen i `/Applications`.

## Bygg

```bash
chmod +x build-mac-release.sh
./build-mac-release.sh
```

Skriptet lager:

- `dist/timeKONTO-Stemplingsur-8.5.app`
- `dist/timeKONTO-Stemplingsur-8.5.pkg`

## Uten Apple Developer-signering

En usignert/adhoc-pakke er egnet for intern testing, men macOS kan vise Gatekeeper-advarsel på andre Mac-er.

## Med Developer ID

Hvis du senere har Apple Developer-sertifikater, kan du angi:

```bash
export APP_SIGN_IDENTITY="Developer ID Application: FIRMANAVN (TEAMID)"
export PKG_SIGN_IDENTITY="Developer ID Installer: FIRMANAVN (TEAMID)"
./build-mac-release.sh
```

Skriptet signerer da appen og installerpakken. Notarisering kan legges til som neste steg.
