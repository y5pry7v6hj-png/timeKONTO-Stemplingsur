<?php
declare(strict_types=1);

// Kopier denne filen til config.php og fyll inn verdiene.
// config.php skal IKKE legges i Git eller deles offentlig.

const DB_HOST = '127.0.0.1';
const DB_PORT = 3306;
const DB_NAME = 'database';
const DB_USER = 'bruker';
const DB_PASS = 'passord';

// Lag en lang tilfeldig nøkkel, og bruk samme verdi i MAUI Services/ApiSettings.cs.
const API_KEY = 'BYTT-MEG-TIL-EN-LANG-TILFELDIG-NOKKEL';

// Norge-tid brukes ved servergenererte tidspunkt (f.eks. sistlest).
date_default_timezone_set('Europe/Oslo');
