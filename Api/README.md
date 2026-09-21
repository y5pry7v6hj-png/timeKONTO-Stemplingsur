# PT's Stemplingsur API

PHP/PDO API for Stemplingsur 8.0.

## Oppsett

1. Kopier `config.example.php` til `config.php` på serveren.
2. Fyll inn databaseverdier og en lang tilfeldig API-nøkkel.
3. Sørg for at `declare(strict_types=1);` er første statement etter `<?php` i `index.php`.
4. Test API-et fra Stemplingsur-appen via **Innstillinger → TEST TILKOBLING**.

Standard klientadresse er `https://admin.pts.bar/api/index.php`, men den kan endres i appen.

API-nøkkelen skal ikke hardkodes i klientkoden; den legges inn lokalt under Innstillinger.
