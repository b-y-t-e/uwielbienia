import { useSyncExternalStore } from "react";
import { PairingRefusedError, TailcatLink, panelStore, type Link } from "./tailcat";
import type {
  Command, CommandReply, DeviceMessage, KioskMessage, Notification, PlanMessage, PlansMessage, StateMessage, VideoMessage,
} from "./protocol";

const APP_NAME = "uwielbienia";

export type Status = "pairing" | "connecting" | "connected" | "failed";

/** Wszystko, co panel wie o aplikacji — tylko z wiadomości hosta (jedno źródło prawdy: aplikacja). */
export interface Snapshot {
  status: Status;
  pairingError: string | null;
  error: string | null;
  state: StateMessage | null;
  plan: PlanMessage | null;
  plans: PlansMessage | null;
  video: (VideoMessage & { receivedAt: number }) | null;
  kiosk: KioskMessage | null;
  device: DeviceMessage | null;
  /** Rośnie przy każdej zmianie planu — kolumna „Pieśń” wczytuje wtedy pozycję od nowa. */
  planVersion: number;
}

let snapshot: Snapshot = {
  status: "connecting", pairingError: null, error: null, state: null, plan: null, plans: null, video: null, kiosk: null,
  device: null, planVersion: 0,
};
const listeners = new Set<() => void>();
let link: Link | null = null;

function update(change: Partial<Snapshot>) {
  snapshot = { ...snapshot, ...change };
  listeners.forEach((l) => l());
}

export function useConnection(): Snapshot {
  return useSyncExternalStore(
    (listener) => { listeners.add(listener); return () => listeners.delete(listener); },
    () => snapshot,
  );
}

function receive(message: Notification) {
  switch (message.type) {
    case "state":
      if (snapshot.state && message.version < snapshot.state.version) return; // starsza wiadomość po nowszej
      update({ state: message });
      break;
    case "plan":
      update({ plan: message, planVersion: snapshot.planVersion + 1 });
      break;
    case "plans":
      update({ plans: message });
      break;
    case "video":
      update({ video: { ...message, receivedAt: performance.now() } });
      break;
    case "kiosk":
      update({ kiosk: message });
      break;
    case "device":
      update({ device: message });
      break;
  }
}

/** Polecenie do aplikacji; stan z odpowiedzi trafia od razu do widoków. Błąd — komunikat na chwilę. */
export async function command(cmd: string, extra: Omit<Command, "cmd"> = {}): Promise<CommandReply | null> {
  if (!link) return null;
  try {
    const reply = JSON.parse(await link.request(JSON.stringify({ cmd, ...extra }))) as CommandReply;
    if (reply.state) receive({ ...reply.state, type: "state" });
    if (reply.plan) receive({ ...reply.plan, type: "plan" });
    if (reply.plans) receive({ ...reply.plans, type: "plans" });
    if (reply.video) receive({ ...reply.video, type: "video" });
    if (reply.device) receive({ ...reply.device, type: "device" });
    if (reply.kiosk) receive({ ...reply.kiosk, type: "kiosk" });
    if (!reply.ok && reply.error) showError(reply.error);
    return reply;
  } catch {
    showError("Polecenie nie dotarło do aplikacji.");
    return null;
  }
}

let errorTimer = 0;
export function showError(error: string) {
  update({ error });
  clearTimeout(errorTimer);
  errorTimer = window.setTimeout(() => update({ error: null }), 5000);
}

/** Łączy z aplikacją: pierwszy raz z kodem parowania (z adresu po „#code=” albo wklejonym), potem sam. */
export async function connect(invitationCode: string | null) {
  update({ status: "connecting", pairingError: null });
  try {
    invitationCode = await expandShortCode(invitationCode);
  } catch {
    update({ status: "pairing", pairingError: "Kod wygasł albo został już użyty. Poproś o nowy w aplikacji (okno „Urządzenia” → „Panel”)." });
    return;
  }
  try {
    link = await TailcatLink.join({
      appName: APP_NAME,
      invitationCode,
      displayName: "panel",
      derpMap: new URL("derpmap.json", location.href).href,
      store: panelStore,
    });
  } catch {
    update({
      status: "pairing",
      pairingError: invitationCode ? "Nie udało się użyć kodu. Poproś o nowy w aplikacji (okno „Urządzenia” → „Panel”)." : null,
    });
    return;
  }

  history.replaceState(null, "", location.pathname + location.search);
  link.onNotify((text) => receive(JSON.parse(text) as Notification));
  link.events.addEventListener("connected", () => { update({ status: "connected" }); void command("hello"); });
  link.events.addEventListener("disconnected", () => update({ status: "connecting" }));
  link.events.addEventListener("failed", (e) => {
    if ((e as CustomEvent).detail instanceof PairingRefusedError)
      update({ status: "pairing", pairingError: "Aplikacja nie zna już tego urządzenia. Zeskanuj nowy kod panelu." });
    else update({ status: "failed" });
  });
  const reply = await command("hello");
  if (reply) update({ status: "connected" });
}

/** Krótki kod do podyktowania (np. „ajk59”) zamienia na długi kod parowania — raz, przez kod.php obok stron. */
async function expandShortCode(code: string | null): Promise<string | null> {
  const short = code?.replace(/[\s-]/g, "").toLowerCase();
  if (!short || !/^[a-z0-9]{4,8}$/.test(short)) return code;
  const response = await fetch(new URL(`../kod.php?k=${short}`, location.href), { cache: "no-store" });
  if (!response.ok) throw new Error(`kod ${short}: ${response.status}`);
  return (await response.text()).trim();
}

export function start() {
  const code = new URLSearchParams(location.hash.slice(1)).get("code");
  void connect(code);
}
