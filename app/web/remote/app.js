// Pilot i ekran w przeglądarce dla aplikacji „Uwielbienia”.
// Protokół: app/src/Uwielbienia.Link/RemoteProtocol.cs — host wysyła {type:"state"|"plan"},
// strona pyta poleceniami {cmd:"hello"|"next"|"prev"|"blank"|"goto"|"showItem"|"showSong"|"search"}.

import { TailcatLink, PairingRefusedError } from "./lib/tailcat/index.js";

const APP_NAME = "uwielbienia";
const $ = (id) => document.getElementById(id);

const ui = {
  pairing: $("pairing"), remote: $("remote"), display: $("display"),
  status: $("status"), planName: $("planName"),
  nowNumber: $("nowNumber"), nowTitle: $("nowTitle"), nowLabel: $("nowLabel"), nowNext: $("nowNext"), nowScreen: $("nowScreen"),
  blankBadge: $("blankBadge"), slides: $("slides"),
  planList: $("planList"), planEmpty: $("planEmpty"),
  searchInput: $("searchInput"), searchList: $("searchList"),
  displayScreen: $("displayScreen"),
};

let link = null;
let state = null;
let plan = { name: null, items: [] };

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
    if (reply.plan) receive({ ...reply.plan, type: "plan" });
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
  } else if (message.type === "plan") {
    plan = message;
    renderPlan();
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
function renderSlide(screen, s, { showImageNote = false } = {}) {
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
    // slajd prezentacji (obraz) — telefon nie dostaje obrazu, tylko jego nazwę
    if (showImageNote && s.lines.length === 0 && s.label) content.push(Object.assign(paragraph(s.label), { className: "image-note" }));
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
  setMarker(ui.nowNumber, s.number, s.itemId ? kindOf(s.itemId) : null);
  ui.nowTitle.textContent = s.title ?? "Ekran jest pusty";
  ui.nowLabel.textContent = s.label ?? "";
  ui.nowNext.textContent = s.next ? `Dalej: ${s.next}` : "";
  renderSlide(ui.nowScreen, s, { showImageNote: true });
  ui.nowScreen.classList.toggle("blank", s.isBlank || !s.title);
  ui.blankBadge.hidden = !s.isBlank;
  $("blank").setAttribute("aria-pressed", String(s.isBlank));

  ui.slides.replaceChildren(...s.slideLabels.map((label, index) => {
    const b = button(label, () => command("goto", { slide: index }));
    b.setAttribute("aria-current", String(index === s.slideIndex));
    return b;
  }));
  ui.slides.querySelector('[aria-current="true"]')?.scrollIntoView({ inline: "center", block: "nearest" });

  markLive();
  renderDisplay();
}

function renderPlan() {
  ui.planName.textContent = plan.name ?? "";
  ui.planEmpty.hidden = plan.items.length > 0;
  ui.planList.replaceChildren(...plan.items.map((item) =>
    listItem(item.number, item.kind, item.title, null, () => command("showItem", { itemId: item.id }), item.id)));
  markLive();
  if (state) setMarker(ui.nowNumber, state.number, state.itemId ? kindOf(state.itemId) : null);
}

const kindOf = (itemId) => plan.items.find((i) => i.id === itemId)?.kind ?? "song";

function markLive() {
  for (const b of ui.planList.querySelectorAll("button")) {
    b.classList.toggle("live", state?.itemId != null && b.dataset.id === state.itemId);
  }
}

function renderDisplay() {
  if (ui.display.hidden) return;
  renderSlide(ui.displayScreen, state);
}

// ---------- wyszukiwanie ----------

let searchTimer = 0;
ui.searchInput.addEventListener("input", () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(async () => {
    const query = ui.searchInput.value.trim();
    if (!query) { ui.searchList.replaceChildren(); return; }
    const reply = await command("search", { query });
    ui.searchList.replaceChildren(...(reply?.results ?? []).map((hit) =>
      listItem(hit.number, hit.isText ? "text" : "song", hit.title, hit.firstLine !== hit.title ? hit.firstLine : null, async () => {
        await command("showSong", { songId: hit.songId });
        ui.searchInput.value = "";
        ui.searchList.replaceChildren();
        selectTab("plan");
      })));
  }, 200);
});

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
  renderDisplay();
}

function selectTab(name) {
  for (const tab of document.querySelectorAll(".tab")) tab.setAttribute("aria-selected", String(tab.dataset.tab === name));
  $("tab-plan").hidden = name !== "plan";
  $("tab-search").hidden = name !== "search";
  if (name === "search") ui.searchInput.focus();
}

$("next").onclick = () => command("next");
$("prev").onclick = () => command("prev");
$("blank").onclick = () => command("blank");
for (const tab of document.querySelectorAll(".tab")) tab.onclick = () => selectTab(tab.dataset.tab);

$("toDisplay").onclick = async () => {
  location.hash = "ekran";
  showMain();
  await document.documentElement.requestFullscreen?.().catch(() => {});
  navigator.wakeLock?.request("screen").catch(() => {});
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
  else if (e.key === "b" || e.key === ".") command("blank");
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

function button(text, onClick) {
  const b = Object.assign(document.createElement("button"), { type: "button", textContent: text });
  b.onclick = onClick;
  return b;
}

// Znacznik pozycji jak w aplikacji: numer pieśni, kropka (tekst), znak slajdu (prezentacja).
function setMarker(el, number, kind) {
  el.className = `marker ${number == null && kind ? kind : ""}`;
  el.textContent = number ?? "";
}

function listItem(number, kind, title, sub, onClick, id) {
  const li = document.createElement("li");
  const b = button("", onClick);
  b.classList.add(kind);
  if (id) b.dataset.id = id;
  const marker = document.createElement("span");
  setMarker(marker, number, kind);
  b.append(marker, Object.assign(document.createElement("span"), { className: "title", textContent: title }));
  if (sub) b.append(Object.assign(document.createElement("span"), { className: "sub", textContent: sub }));
  li.append(b);
  return li;
}

// ---------- start ----------

const codeFromUrl = new URLSearchParams(location.hash.slice(1)).get("code");
try {
  await connect(codeFromUrl);
} catch {
  showPairing(null);
}
