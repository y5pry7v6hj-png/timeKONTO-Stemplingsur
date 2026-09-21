<?php
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');

require __DIR__ . '/config.php';

function jsonResponse(mixed $data, int $status = 200): never {
    http_response_code($status);
    echo json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR);
    exit;
}

function fail(string $message, int $status = 400): never {
    jsonResponse(['error' => $message], $status);
}

function body(): array {
    $raw = file_get_contents('php://input');
    if ($raw === false || trim($raw) === '') return [];
    $data = json_decode($raw, true);
    if (!is_array($data)) fail('Ugyldig JSON.');
    return $data;
}

function requireApiKey(): void {
    $key = $_SERVER['HTTP_X_API_KEY'] ?? '';
    if (!hash_equals(API_KEY, $key)) fail('Ugyldig API-nøkkel.', 401);
}

function db(): PDO {
    static $pdo = null;
    if ($pdo instanceof PDO) return $pdo;

    $dsn = sprintf('mysql:host=%s;port=%d;dbname=%s;charset=utf8mb4', DB_HOST, DB_PORT, DB_NAME);
    $pdo = new PDO($dsn, DB_USER, DB_PASS, [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_EMULATE_PREPARES => false,
    ]);
    return $pdo;
}

function parseWhen(mixed $value): DateTimeImmutable {
    if (!is_string($value) || trim($value) === '') fail('Tidspunkt mangler.');
    try {
        return new DateTimeImmutable($value);
    } catch (Throwable) {
        fail('Ugyldig tidspunkt.');
    }
}

function requireDdmm(mixed $value): string {
    $value = trim((string)$value);
    if (!preg_match('/^\d{4}$/', $value)) fail('Fødselsdag/måned må være DDMM.');
    return $value;
}

function employeePin(PDO $pdo, int $employeeId): string {
    $stmt = $pdo->prepare("SELECT IFNULL(DATE_FORMAT(dtFodselsdato, '%d%m'), '0101') FROM ansatt WHERE idansatt = ? AND iSkalstemple = 1");
    $stmt->execute([$employeeId]);
    $pin = $stmt->fetchColumn();
    if ($pin === false) fail('Fant ikke aktiv medarbeider.', 404);
    return (string)$pin;
}

function verifyEmployeePin(PDO $pdo, int $employeeId, string $pin): void {
    if (!hash_equals(employeePin($pdo, $employeeId), $pin)) fail('Feil fødselsdag eller måned.', 403);
}

requireApiKey();
$pdo = db();
$action = $_GET['action'] ?? '';

try {
    switch ($action) {
        case 'health':
            jsonResponse(['ok' => true, 'version' => '5.1']);

        case 'employees':
            $rows = $pdo->query("SELECT idansatt, sFornavn, sEtternavn FROM ansatt WHERE iSkalstemple = 1 ORDER BY sEtternavn, sFornavn")->fetchAll();
            jsonResponse(array_map(static fn(array $r) => [
                'id' => (int)$r['idansatt'],
                'firstName' => trim((string)$r['sFornavn']),
                'lastName' => trim((string)$r['sEtternavn']),
                'birthDayMonth' => ''
            ], $rows));

        case 'active':
            $sql = "SELECT s.idstempling, s.idansatt, s.dtinn, s.scomment, a.sFornavn, a.sEtternavn
                    FROM stempling s
                    JOIN ansatt a ON a.idansatt = s.idansatt
                    WHERE s.istempletut = 0
                    ORDER BY s.tstimestamp, s.dtinn";
            $rows = $pdo->query($sql)->fetchAll();
            jsonResponse(array_map(static fn(array $r) => [
                'id' => (int)$r['idstempling'],
                'employeeId' => (int)$r['idansatt'],
                'employeeName' => trim((string)$r['sFornavn'] . ' ' . (string)$r['sEtternavn']),
                'clockIn' => (new DateTimeImmutable((string)$r['dtinn']))->format(DATE_ATOM),
                'clockOut' => null,
                'comment' => (string)($r['scomment'] ?? '')
            ], $rows));

        case 'clock-in':
            if ($_SERVER['REQUEST_METHOD'] !== 'POST') fail('Kun POST er tillatt.', 405);
            $data = body();
            $employeeId = (int)($data['employeeId'] ?? 0);
            $pin = requireDdmm($data['birthDayMonth'] ?? '');
            $when = parseWhen($data['when'] ?? null);
            $comment = trim((string)($data['comment'] ?? ''));
            if (mb_strlen($comment) > 2000) fail('Kommentaren er for lang.');
            verifyEmployeePin($pdo, $employeeId, $pin);

            $pdo->beginTransaction();
            $check = $pdo->prepare('SELECT idstempling FROM stempling WHERE idansatt = ? AND istempletut = 0 LIMIT 1 FOR UPDATE');
            $check->execute([$employeeId]);
            if ($check->fetchColumn() !== false) {
                $pdo->rollBack();
                fail('Medarbeideren er allerede stemplet inn.', 409);
            }

            $mysqlWhen = $when->format('Y-m-d H:i:s');
            $insert = $pdo->prepare("INSERT INTO stempling (idansatt, dtinn, dtut, istempletut, scomment) VALUES (?, ?, ?, 0, ?)");
            $insert->execute([$employeeId, $mysqlWhen, $mysqlWhen, $comment]);
            $id = (int)$pdo->lastInsertId();

            $name = $pdo->prepare('SELECT sFornavn, sEtternavn FROM ansatt WHERE idansatt = ?');
            $name->execute([$employeeId]);
            $employee = $name->fetch();
            $pdo->commit();

            jsonResponse([
                'id' => $id,
                'employeeId' => $employeeId,
                'employeeName' => trim((string)$employee['sFornavn'] . ' ' . (string)$employee['sEtternavn']),
                'clockIn' => $when->format(DATE_ATOM),
                'clockOut' => null,
                'comment' => $comment
            ], 201);

        case 'clock-out':
            if ($_SERVER['REQUEST_METHOD'] !== 'POST') fail('Kun POST er tillatt.', 405);
            $data = body();
            $entryId = (int)($data['timeEntryId'] ?? 0);
            $pin = requireDdmm($data['birthDayMonth'] ?? '');
            $when = parseWhen($data['when'] ?? null);
            $comment = trim((string)($data['comment'] ?? ''));
            if (mb_strlen($comment) > 2000) fail('Kommentaren er for lang.');

            $pdo->beginTransaction();
            $stmt = $pdo->prepare('SELECT idansatt, dtinn FROM stempling WHERE idstempling = ? AND istempletut = 0 LIMIT 1 FOR UPDATE');
            $stmt->execute([$entryId]);
            $entry = $stmt->fetch();
            if (!$entry) {
                $pdo->rollBack();
                fail('Fant ikke aktiv stempling.', 404);
            }

            $employeeId = (int)$entry['idansatt'];
            verifyEmployeePin($pdo, $employeeId, $pin);
            $clockIn = new DateTimeImmutable((string)$entry['dtinn']);
            if ($when < $clockIn) {
                $pdo->rollBack();
                fail('Du kan ikke stemple ut før du startet.');
            }
            if (($when->getTimestamp() - $clockIn->getTimestamp()) > (20 * 3600)) {
                $pdo->rollBack();
                fail('Du har ikke jobbet over 20 timer. Kontroller utstemplingsdato og klokkeslett.');
            }

            // Overtid brukes ikke lenger. Gamle databasefelt beholdes urørt for historikk.
            $update = $pdo->prepare("UPDATE stempling SET dtut = ?, istempletut = 1, scomment = ? WHERE idstempling = ? AND istempletut = 0");
            $update->execute([$when->format('Y-m-d H:i:s'), $comment, $entryId]);
            $pdo->commit();
            jsonResponse(['ok' => true]);

        default:
            fail('Ukjent action.', 404);
    }
} catch (PDOException $e) {
    if ($pdo->inTransaction()) $pdo->rollBack();
    error_log('Stemplingsur API database error: ' . $e->getMessage());
    fail('Databasefeil.', 500);
} catch (Throwable $e) {
    if ($pdo->inTransaction()) $pdo->rollBack();
    error_log('Stemplingsur API error: ' . $e->getMessage());
    fail('Serverfeil.', 500);
}
