<?php
// Krótki kod parowania (np. „ajk59”) do podyktowania zamiast długiego kodu tailcat-link.
//   POST  (treść: długi kod)  → krótki kod, ważny 5 minut
//   GET   ?k=ajk59            → długi kod, jednorazowo (potem znika)
// Krótki kod tylko wskazuje zaproszenie — samo zaproszenie (jednorazowe, z kluczem aplikacji) i szyfrowanie
// połączenia są bez zmian. Zgadywanie ogranicza limit nieudanych prób z jednego adresu.

const LIFETIME = 300;           // s
const ALPHABET = 'abcdefghjkmnpqrstuvwxyz23456789'; // bez i, l, o, 0, 1 — nie mylą się przy dyktowaniu
const LENGTH = 5;
const MAX_CODES = 200;          // naraz
const MAX_FAILURES = 20;        // nieudanych odczytów z jednego adresu na LIFETIME

header('Content-Type: text/plain; charset=utf-8');
header('Cache-Control: no-store');
header('Access-Control-Allow-Origin: *');

$dir = sys_get_temp_dir() . DIRECTORY_SEPARATOR . 'uwielbienie-kody';
if (!is_dir($dir) && !@mkdir($dir, 0700, true)) fail(500, 'storage');
$now = time();

// sprzątanie: wygasłe kody i liczniki prób
foreach (glob($dir . DIRECTORY_SEPARATOR . '*') ?: [] as $file)
    if (@filemtime($file) < $now - LIFETIME) @unlink($file);

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') exit;

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $long = trim(file_get_contents('php://input', false, null, 0, 4097));
    if ($long === '' || strlen($long) > 4096 || !preg_match('/^[A-Za-z0-9_.\-~]+$/', $long)) fail(400, 'code');
    if (count(glob($dir . DIRECTORY_SEPARATOR . 'c-*') ?: []) >= MAX_CODES) fail(503, 'busy');
    for ($attempt = 0; $attempt < 20; $attempt++) {
        $short = '';
        for ($i = 0; $i < LENGTH; $i++) $short .= ALPHABET[random_int(0, strlen(ALPHABET) - 1)];
        $handle = @fopen($dir . DIRECTORY_SEPARATOR . 'c-' . $short, 'x'); // 'x' = tylko nowy plik
        if ($handle) {
            fwrite($handle, $long);
            fclose($handle);
            echo $short;
            exit;
        }
    }
    fail(503, 'busy');
}

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    $ip = $dir . DIRECTORY_SEPARATOR . 'ip-' . md5($_SERVER['REMOTE_ADDR'] ?? '');
    $failures = is_file($ip) ? (int)file_get_contents($ip) : 0;
    if ($failures >= MAX_FAILURES) fail(429, 'limit');
    $short = strtolower(preg_replace('/[\s\-]/', '', $_GET['k'] ?? ''));
    $file = $dir . DIRECTORY_SEPARATOR . 'c-' . $short;
    // rename = jednorazowo, także przy dwóch odczytach naraz
    $taken = $file . '.' . bin2hex(random_bytes(4));
    if (preg_match('/^[a-z0-9]{' . LENGTH . '}$/', $short) && @rename($file, $taken)) {
        echo file_get_contents($taken);
        @unlink($taken);
        exit;
    }
    file_put_contents($ip, (string)($failures + 1)); // mtime = ostatnia porażka; limit mija po LIFETIME
    fail(404, 'unknown');
}

fail(405, 'method');

function fail(int $status, string $reason): void
{
    http_response_code($status);
    echo $reason;
    exit;
}
