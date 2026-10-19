// Pilot i ekran w przeglądarce dla aplikacji „Uwielbienia”.
// Protokół: app/src/Uwielbienia.Link/RemoteProtocol.cs — host wysyła {type:"state"|"plan"|"video"|"device"|"kiosk"},
// strona pyta poleceniami {cmd:"hello"|"next"|"prev"|…}. Telefon zmienia slajdy: bez planu i gaszenia
// ekranu (to zostaje w aplikacji) — dziecko nic nie zepsuje, a muzyk widzi bieżący i następny slajd z
// akordami bez dotykania telefonu. Dodawanie pieśni („+”) tylko z uprawnieniem nadanym w aplikacji.

import { TailcatLink, PairingRefusedError } from "./lib/tailcat/index.js";

const APP_NAME = "uwielbienia";
const $ = (id) => document.getElementById(id);

const ui = {
  pairing: $("pairing"), remote: $("remote"), display: $("display"),
  status: $("status"),
  nowNumber: $("nowNumber"), nowTitle: $("nowTitle"), nowLabel: $("nowLabel"), blankBadge: $("blankBadge"),
  currentLines: $("currentLines"), nextLabel: $("nextLabel"), nextLines: $("nextLines"),
  displayScreen: $("displayScreen"),
};

let link = null;
let state = null;

// ---------- połączenie ----------

async function connect(invitationCode) {
  setStatus("connecting", "Łączenie…");
  try {
    link = await TailcatLink.join({
      appName: APP_NAME,
      invitationCode,
      displayName: navigator.userAgentData?.mobile ? "telefon" : "przeglądarka",
      derpMap: new URL("derpmap.json", location.href).href,
    });
  } catch (error) {
    showPairing(invitationCode ? "Nie udało się użyć kodu. Poproś o nowy w aplikacji." : null);
    return;
  }

  history.replaceState(null, "", location.pathname + location.search + (isDisplayMode() ? "#ekran" : ""));
  link.onNotify((text) => receive(JSON.parse(text)));
  link.events.addEventListener("connected", () => { setStatus("connected", "Połączono"); hello(); });
  link.events.addEventListener("disconnected", () => setStatus("connecting", "Łączenie ponownie…"));
  link.events.addEventListener("failed", (e) => {
    if (e.detail instanceof PairingRefusedError) showPairing("Aplikacja nie zna już tego urządzenia. Zeskanuj nowy kod.");
    else setStatus("failed", "Brak połączenia");
  });

  showMain();
  hello();
}

async function command(cmd, extra = {}) {
  if (!link) return null;
  try {
    const reply = JSON.parse(await link.request(JSON.stringify({ cmd, ...extra })));
    if (reply.state) receive({ ...reply.state, type: "state" });
    if (reply.video) receive({ ...reply.video, type: "video" });
    if (reply.device) receive({ ...reply.device, type: "device" });
    if (reply.kiosk) receive({ ...reply.kiosk, type: "kiosk" });
    return reply;
  } catch {
    setStatus("failed", "Polecenie nie dotarło");
    return null;
  }
}

const hello = () => command("hello");

function receive(message) {
  if (message.type === "state") {
    if (state && message.version < state.version) return; // starsza wiadomość po nowszej
    state = message;
    renderState();
  } else if (message.type === "device") {
    setCanAddSongs(!!message.canAddSongs);
  } else if (message.type === "video") {
    video = { ...message, receivedAt: performance.now() };
    renderVideo();
  } else if (message.type === "kiosk") {
    kiosk = message;
    renderKiosk();
    renderDisplay();
  }
}

// ---------- kiosk ----------

let kiosk = null;

// Kiosk na ekranie: elementy zmieniają się same (dla telefonu to jeden slajd — „Dalej” = następna pozycja planu);
// zamiast wersów podgląd bieżącego obrazu i napis.
// Element tylko tego kiosku, który jest teraz na ekranie — nie poprzedniego (wiadomości przychodzą w dowolnej kolejności).
function isKioskShown() {
  return !!state?.isKiosk && !!kiosk && kiosk.itemId === state.itemId;
}

function renderKiosk() {
  const show = isKioskShown();
  $("kioskPanel").hidden = !show;
  if (state) ui.currentLines.hidden = !!state.isVideo || show;
  if (!show) return;
  ui.nowLabel.textContent = `Kiosk · ${kiosk.index + 1} z ${kiosk.count}${kiosk.isRunning || kiosk.count < 2 ? "" : " · zatrzymany"}`;
  $("kioskImage").hidden = !kiosk.image;
  if (kiosk.image) $("kioskImage").src = kiosk.image;
  $("kioskTitle").textContent = kiosk.title ?? "";
  $("kioskText").textContent = kiosk.text ?? "";
  $("kioskPanel").classList.toggle("text-only", !kiosk.image);
}

// ---------- film ----------

let video = null;
const formatTime = (seconds) => {
  const s = Math.max(0, Math.floor(seconds));
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), r = String(s % 60).padStart(2, "0");
  return h > 0 ? `${h}:${String(m).padStart(2, "0")}:${r}` : `${m}:${r}`;
};

// Film na ekranie: zamiast tekstu czas, pasek postępu, pauza i ±15 s (stan z aplikacji co sekundę, między
// wiadomościami czas płynie lokalnie).
function renderVideo() {
  const show = !!state?.isVideo;
  $("videoPanel").hidden = !show;
  ui.currentLines.hidden = show || !!state?.isKiosk;
  if (!show) return;
  const v = video ?? { isPlaying: false, position: 0, duration: 0 };
  const elapsed = v.isPlaying ? (performance.now() - (v.receivedAt ?? 0)) / 1000 : 0;
  const position = Math.min(v.duration || Infinity, v.position + elapsed);
  $("videoTime").textContent = formatTime(position);
  $("videoDuration").textContent = v.duration ? ` / ${formatTime(v.duration)}` : "";
  $("videoProgress").style.width = v.duration ? `${(100 * position) / v.duration}%` : "0%";
  $("iconPause").toggleAttribute("hidden", !v.isPlaying); // SVG nie ma właściwości hidden — tylko atrybut
  $("iconPlay").toggleAttribute("hidden", v.isPlaying);
  $("videoToggle").setAttribute("aria-label", v.isPlaying ? "Pauza" : "Odtwórz");
  const ms = v.pictureOffsetMs ?? 0;
  const seconds = (Math.abs(ms) / 1000).toFixed(1).replace(".", ",");
  $("pictureOffset").textContent = ms > 0 ? `+${seconds} s` : ms < 0 ? `−${seconds} s` : `${seconds} s`;
  $("videoOffset").classList.toggle("set", ms !== 0);
  $("videoError").hidden = !v.error;
  $("videoError").textContent = v.error ?? "";
}
setInterval(() => { if (video?.isPlaying && state?.isVideo) renderVideo(); }, 250);
$("videoToggle").onclick = () => command("videoToggle");
$("videoBack").onclick = () => command("videoSkip", { seconds: -15 });
$("videoForward").onclick = () => command("videoSkip", { seconds: 15 });
$("pictureEarlier").onclick = () => command("videoOffset", { milliseconds: -100 });
$("pictureLater").onclick = () => command("videoOffset", { milliseconds: 100 });

// ---------- widok ----------

function setStatus(kind, text) {
  ui.status.dataset.state = kind;
  ui.status.textContent = text;
  ui.status.title = text;
}

// Slajd tak jak na rzutniku: tytuł z linią (tylko gdy coś wnosi), wersy tytułowe w kolorze tytułu,
// refren dołączony do zwrotki kursywą z odstępem, „×N” przy powtarzanych wersach.
function renderSlide(screen, s) {
  const text = screen.querySelector(".slide-text");
  const content = [];
  if (s && !s.isBlank && s.isKiosk) {
    if (!isKioskShown()) { text.replaceChildren(); return; }
    // kiosk: podgląd obrazu (mały — z telefonu) i napis pod nim
    if (kiosk.image) content.push(Object.assign(document.createElement("img"), { className: "kiosk-image", src: kiosk.image, alt: "" }));
    if (kiosk.title) content.push(Object.assign(document.createElement("div"), { className: "slide-title", textContent: kiosk.title }));
    if (kiosk.text) content.push(Object.assign(document.createElement("p"), { className: "kiosk-text", textContent: kiosk.text }));
  } else if (s && !s.isBlank) {
    if (s.showTitle) content.push(Object.assign(document.createElement("div"), { className: "slide-title", textContent: s.title }));
    for (const l of s.lines) {
      // słowa powtarzające tytuł w kolorze tytułu, reszta wersu zwykła
      const p = document.createElement("p");
      const titleLength = l.titleLength ?? 0;
      if (titleLength > 0) p.append(Object.assign(document.createElement("span"), { className: "is-title", textContent: l.text.slice(0, titleLength) }));
      p.append(l.text.slice(titleLength));
      p.classList.toggle("chorus", !!l.chorus);
      p.classList.toggle("part-start", !!l.partStart);
      if (l.repeat > 1) p.append(Object.assign(document.createElement("span"), { className: "rep", textContent: `  ×${l.repeat}` }));
      content.push(p);
    }
  }
  text.replaceChildren(...content);
  fit(screen);
}

// Jak Viewbox w aplikacji: tekst, który się nie mieści, zmniejsza się w całości.
function fit(screen) {
  const text = screen.querySelector(".slide-text");
  const box = screen.querySelector(".slide-fit");
  text.style.transform = "";
  const style = getComputedStyle(box);
  const room = box.clientHeight - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom);
  if (room > 0 && text.scrollHeight > room) text.style.transform = `scale(${room / text.scrollHeight})`;
}
// Ponownie, gdy zmieni się ekran albo sam tekst (np. po wczytaniu Literaty); transform nie zmienia wymiarów — bez pętli.
const resized = new ResizeObserver((entries) => entries.forEach((e) => fit(e.target.closest(".slide-screen"))));
for (const screen of document.querySelectorAll(".slide-screen")) {
  resized.observe(screen);
  resized.observe(screen.querySelector(".slide-text"));
}

function renderState() {
  const s = state;
  ui.nowNumber.textContent = s.number ?? "";
  ui.nowTitle.textContent = s.title ?? "Ekran jest pusty";
  ui.nowLabel.textContent = s.label ?? "";
  ui.blankBadge.hidden = !s.isBlank;
  ui.currentLines.parentElement.classList.toggle("empty", !s.title);
  ui.currentLines.replaceChildren(...s.lines.map(chordLine));
  // sam tytuł (tekst bez treści) — jak na rzutniku; slajd prezentacji (obraz) — tylko jego nazwa
  const titleOnly = s.lines.length === 0 && s.showTitle;
  ui.currentLines.classList.toggle("image", s.lines.length === 0 && !!s.label && !titleOnly);
  ui.currentLines.classList.toggle("title-only", titleOnly);
  if (titleOnly) ui.currentLines.append(paragraph(s.title));
  else if (s.lines.length === 0 && s.label) ui.currentLines.append(paragraph(s.label));

  // początek następnego slajdu (2 wersy z akordami) — muzyk wie, co nadchodzi, a bieżący zostaje duży
  const nextLines = (s.nextLines ?? []).slice(0, 2);
  ui.nextLabel.textContent = s.next ? `Dalej: ${s.next}` : "";
  ui.nextLines.replaceChildren(...nextLines.map(chordLine));
  renderVideo();
  renderKiosk();
  fitAll();
  renderDisplay();
}

// Wers z akordami nad nim (jak kolumna „Na ekranie” w aplikacji); refren dołączony do zwrotki kursywą.
function chordLine(l) {
  const div = document.createElement("div");
  div.className = "line";
  div.classList.toggle("chorus", !!l.chorus);
  div.classList.toggle("part-start", !!l.partStart);
  if (l.chords) div.append(Object.assign(document.createElement("div"), { className: "chords", textContent: l.chords }));
  const text = paragraph(l.text);
  if (l.repeat > 1) text.append(Object.assign(document.createElement("span"), { className: "rep", textContent: ` ×${l.repeat}` }));
  div.append(text);
  return div;
}

// Bieżący slajd w stałym rozmiarze (24 px, największy ze skali); mniejszy tylko wtedy, gdy się nie mieści —
// cały ma być widoczny bez przewijania (muzyk nie dotyka telefonu). Krótki slajd nie robi się olbrzymi.
function fitText(box, max) {
  if (!box.offsetParent) return;
  let low = 13, high = max;
  box.style.fontSize = `${high}px`;
  if (box.scrollHeight <= box.clientHeight && box.scrollWidth <= box.clientWidth) return;
  while (high - low > 0.5) {
    const size = (low + high) / 2;
    box.style.fontSize = `${size}px`;
    if (box.scrollHeight <= box.clientHeight && box.scrollWidth <= box.clientWidth) low = size; else high = size;
  }
  box.style.fontSize = `${low}px`;
}

// Wers w jednej linii czyta się lepiej niż zawinięty: zmniejsz tekst najwyżej o 25%, tak żeby zawijało się
// jak najmniej wersów (szerokość wersu rośnie liniowo z rozmiarem czcionki; za długie zostają zawinięte).
function avoidWrapping(box) {
  const size = parseFloat(box.style.fontSize);
  if (!size) return;
  box.classList.add("measure");
  const widths = [...box.querySelectorAll("p")].map((p) => p.scrollWidth);
  box.classList.remove("measure");
  const fitting = widths.map((w) => size * (box.clientWidth / w) * 0.98).filter((s) => s >= size * 0.75);
  const target = Math.min(size, ...fitting);
  if (target < size) box.style.fontSize = `${target}px`;
}

function fitAll() {
  fitText(ui.currentLines, 24);
  avoidWrapping(ui.currentLines);
}
new ResizeObserver(() => fitAll()).observe(ui.remote);
document.fonts.addEventListener("loadingdone", () => fitAll());

function renderDisplay() {
  if (ui.display.hidden) return;
  renderSlide(ui.displayScreen, state);
}

// ---------- tryby i zdarzenia ----------

const isDisplayMode = () => location.hash === "#ekran";

function showPairing(message) {
  ui.pairing.hidden = false;
  ui.remote.hidden = true;
  ui.display.hidden = true;
  $("pairError").textContent = message ?? "";
}

function showMain() {
  ui.pairing.hidden = true;
  ui.remote.hidden = isDisplayMode();
  ui.display.hidden = !isDisplayMode();
  keepAwake();
  fitAll();
  renderDisplay();
}

// Telefon na pulpicie ani ekran w przeglądarce nie mogą się wygaszać (blokada wraca po powrocie do karty).
function keepAwake() {
  navigator.wakeLock?.request("screen").catch(() => {});
}
document.addEventListener("visibilitychange", () => { if (document.visibilityState === "visible" && link) keepAwake(); });

// Akordy nad wersami: dla muzyka; dziecko może mieć sam tekst. Zapamiętane w tej przeglądarce.
const chordsToggle = $("chordsToggle");
function showChords(on) {
  chordsToggle.setAttribute("aria-pressed", String(on));
  ui.remote.classList.toggle("no-chords", !on);
  localStorage.setItem("akordy", on ? "1" : "0");
  fitAll();
}
chordsToggle.onclick = () => showChords(ui.remote.classList.contains("no-chords"));
showChords(localStorage.getItem("akordy") !== "0");

// Jasny / ciemny wygląd samego telefonu (rzutnik bez zmian). Zapamiętany w tej przeglądarce.
const themeToggle = $("themeToggle");
function setTheme(theme) {
  document.documentElement.dataset.theme = theme;
  themeToggle.setAttribute("aria-label", theme === "light" ? "Ciemny wygląd" : "Jasny wygląd");
  document.querySelector('meta[name="theme-color"]').content = theme === "light" ? "#F6F7FB" : "#161B2E";
}
themeToggle.onclick = () => {
  const theme = document.documentElement.dataset.theme === "light" ? "dark" : "light";
  localStorage.setItem("wyglad", theme);
  setTheme(theme);
};
setTheme(document.documentElement.dataset.theme);

$("next").onclick = () => command("next");
$("prev").onclick = () => command("prev");

$("toDisplay").onclick = async () => {
  location.hash = "ekran";
  showMain();
  await document.documentElement.requestFullscreen?.().catch(() => {});
};
$("leaveDisplay").onclick = () => {
  history.replaceState(null, "", location.pathname);
  document.exitFullscreen?.().catch(() => {});
  showMain();
};
ui.display.addEventListener("dblclick", () => document.documentElement.requestFullscreen?.().catch(() => {}));

// ---------- dodawanie pieśni (tylko z uprawnieniem z okna „Telefon”) ----------
// Telefon bez uprawnienia nic nie dodaje (można dać dziecku). Z uprawnieniem: „+” → wyszukiwarka → dotknięcie wyniku
// = pieśń zaraz za tą na ekranie; ekran się nie zmienia, a „Cofnij” przez chwilę wycofuje pomyłkę.

const addSheet = $("addSheet"), addQuery = $("addQuery");
let searchTimer = 0, searchSeq = 0, toastTimer = 0;

function setCanAddSongs(allowed) {
  $("addSong").hidden = !allowed;
  if (!allowed) closeAddSheet();
}

function openAddSheet() {
  addSheet.hidden = false;
  addQuery.value = "";
  addQuery.focus();
  search("");
}

function closeAddSheet() {
  addSheet.hidden = true;
  addQuery.blur();
}

async function search(query) {
  const seq = ++searchSeq;
  const reply = await command("search", { query });
  if (seq !== searchSeq || !reply?.ok) return; // starsza odpowiedź po nowszym wpisaniu
  const hits = reply.results ?? [];
  $("addResults").replaceChildren(...hits.map(hitItem));
  $("addEmpty").hidden = hits.length > 0 || query.trim() === "";
}

function hitItem(hit) {
  const span = (className, textContent) => Object.assign(document.createElement("span"), { className, textContent });
  const button = document.createElement("button");
  button.append(
    span("hit-number" + (hit.isText ? " text" : ""), hit.isText ? "" : String(hit.number ?? "")),
    span("hit-title", hit.title),
    span("hit-line", hit.firstLine));
  button.onclick = () => addNext(hit);
  const li = document.createElement("li");
  li.append(button);
  return li;
}

// Odpowiedź idzie przez serwer pośredniczący — kolejne dotknięcia do tego czasu nie dodają pieśni drugi raz.
let adding = false;

async function addNext(hit) {
  if (adding) return;
  adding = true;
  let reply;
  try {
    reply = await command("addNext", { songId: hit.songId });
  } finally {
    adding = false;
  }
  if (!reply) return;
  if (!reply.ok) { showToast(reply.error ?? "Nie udało się dodać."); return; }
  closeAddSheet();
  const added = reply.added;
  showToast(`Następna: ${added.number ? added.number + " " : ""}${added.title}`, () => undoAdd(added.id));
}

async function undoAdd(itemId) {
  const reply = await command("undoAdd", { itemId });
  if (reply) showToast(reply.ok ? "Cofnięto." : reply.error ?? "Nie udało się cofnąć.");
}

function showToast(text, undo = null) {
  clearTimeout(toastTimer);
  $("toastText").textContent = text;
  $("toastUndo").hidden = !undo;
  $("toastUndo").onclick = () => { $("toast").hidden = true; undo?.(); };
  $("toast").hidden = false;
  toastTimer = setTimeout(() => { $("toast").hidden = true; }, undo ? 5000 : 2500);
}

$("addSong").onclick = openAddSheet;
$("addCancel").onclick = closeAddSheet;
addQuery.addEventListener("input", () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => search(addQuery.value), 150);
});
$("addForm").addEventListener("submit", (e) => {
  e.preventDefault();
  addQuery.blur(); // Enter chowa klawiaturę — wyniki widać w całości
});

// Klawiatura (np. laptop z przeglądarką jako pilot lub ekran)
document.addEventListener("keydown", (e) => {
  if (!addSheet.hidden) { if (e.key === "Escape") closeAddSheet(); return; }
  if (e.target instanceof HTMLInputElement || !link) return;
  if ([" ", "ArrowRight", "ArrowDown", "PageDown"].includes(e.key)) { command("next"); e.preventDefault(); }
  else if (["Backspace", "ArrowLeft", "ArrowUp", "PageUp"].includes(e.key)) { command("prev"); e.preventDefault(); }
});

$("pairForm").addEventListener("submit", (e) => {
  e.preventDefault();
  const code = $("codeInput").value.trim();
  if (code) connect(code);
});

// ---------- pomocnicze ----------

function paragraph(text) {
  return Object.assign(document.createElement("p"), { textContent: text });
}

// ---------- start ----------

const codeFromUrl = new URLSearchParams(location.hash.slice(1)).get("code");
try {
  await connect(codeFromUrl);
} catch {
  showPairing(null);
}
