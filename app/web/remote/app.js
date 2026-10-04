// Pilot i ekran w przeglądarce dla aplikacji „Uwielbienia”.
// Protokół: app/src/Uwielbienia.Link/RemoteProtocol.cs — host wysyła {type:"state"|"plan"},
// strona pyta poleceniami {cmd:"hello"|"next"|"prev"}. Telefon tylko zmienia slajdy: bez planu,
// wyszukiwania i gaszenia ekranu (to zostaje w aplikacji) — dziecko nic nie zepsuje, a muzyk widzi
// bieżący i następny slajd z akordami bez dotykania telefonu.

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
  }
}

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
  if (s && !s.isBlank) {
    if (s.showTitle) content.push(Object.assign(document.createElement("div"), { className: "slide-title", textContent: s.title }));
    for (const l of s.lines) {
      const p = paragraph(l.text);
      p.classList.toggle("chorus", !!l.chorus);
      p.classList.toggle("part-start", !!l.partStart);
      p.classList.toggle("is-title", !!l.title);
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
  // slajd prezentacji (obraz) — telefon dostaje tylko jego nazwę
  ui.currentLines.replaceChildren(...(s.lines.length ? s.lines.map(chordLine) : []));
  ui.currentLines.classList.toggle("image", s.lines.length === 0 && !!s.label);
  if (s.lines.length === 0 && s.label) ui.currentLines.append(paragraph(s.label));

  // początek następnego slajdu (2 wersy z akordami) — muzyk wie, co nadchodzi, a bieżący zostaje duży
  const nextLines = (s.nextLines ?? []).slice(0, 2);
  ui.nextLabel.textContent = s.next ? `Dalej: ${s.next}` : "";
  ui.nextLines.replaceChildren(...nextLines.map(chordLine));
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

// Klawiatura (np. laptop z przeglądarką jako pilot lub ekran)
document.addEventListener("keydown", (e) => {
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
