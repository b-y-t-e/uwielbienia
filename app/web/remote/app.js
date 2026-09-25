// Pilot i ekran w przeglądarce dla aplikacji „Uwielbienia”.
// Protokół: app/src/Uwielbienia.Link/RemoteProtocol.cs — host wysyła {type:"state"|"plan"},
// strona pyta poleceniami {cmd:"hello"|"next"|"prev"|"blank"|"goto"|"showItem"|"showSong"|"search"}.

import { TailcatLink, PairingRefusedError } from "./lib/tailcat/index.js";

const APP_NAME = "uwielbienia";
const $ = (id) => document.getElementById(id);

const ui = {
  pairing: $("pairing"), remote: $("remote"), display: $("display"),
  status: $("status"), planName: $("planName"),
  nowNumber: $("nowNumber"), nowTitle: $("nowTitle"), nowLabel: $("nowLabel"), nowLines: $("nowLines"),
  blankBadge: $("blankBadge"), slides: $("slides"),
  planList: $("planList"), planEmpty: $("planEmpty"),
  searchInput: $("searchInput"), searchList: $("searchList"),
  displayLines: $("displayLines"),
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
}

function renderState() {
  const s = state;
  ui.nowNumber.textContent = s.number ?? "";
  ui.nowTitle.textContent = s.title ?? "Ekran jest pusty";
  ui.nowLabel.textContent = [s.label, s.next ? `dalej: ${s.next}` : null].filter(Boolean).join(", ");
  ui.nowLines.replaceChildren(...s.lines.map((l) => paragraph(l.text)));
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
    listItem(item.number, item.title, null, () => command("showItem", { itemId: item.id }), item.id)));
  markLive();
}

function markLive() {
  for (const b of ui.planList.querySelectorAll("button")) {
    b.classList.toggle("live", state?.itemId != null && b.dataset.id === state.itemId);
  }
}

function renderDisplay() {
  if (ui.display.hidden) return;
  const visible = state && !state.isBlank ? state.lines : [];
  ui.displayLines.replaceChildren(...visible.map((l) => {
    const p = paragraph(l.text);
    if (l.repeat > 1) p.append(Object.assign(document.createElement("span"), { className: "rep", textContent: `  ×${l.repeat}` }));
    return p;
  }));
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
      listItem(hit.number, hit.title, hit.firstLine !== hit.title ? hit.firstLine : null, async () => {
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

function listItem(number, title, sub, onClick, id) {
  const li = document.createElement("li");
  const b = button("", onClick);
  if (id) b.dataset.id = id;
  b.append(
    Object.assign(document.createElement("span"), { className: "number", textContent: number }),
    Object.assign(document.createElement("span"), { className: "title", textContent: title }),
  );
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
