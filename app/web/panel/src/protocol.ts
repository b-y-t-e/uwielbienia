// Protokół panelu — odpowiednik app/src/Uwielbienia.Link/RemoteProtocol.cs i PanelProtocol.cs (JSON, camelCase).
// Zmiana po jednej stronie = zmiana po drugiej.

export interface LineMessage {
  text: string;
  repeat: number;
  chorus?: boolean;
  partStart?: boolean;
  titleLength?: number;
  chords?: string | null;
}

export interface StateMessage {
  type: "state";
  /** Uruchomienie aplikacji — wersje porównuje się tylko w jednym. */
  session: string;
  version: number;
  songId?: string | null;
  itemId?: string | null;
  number?: number | null;
  title?: string | null;
  slideIndex: number;
  slideCount: number;
  label?: string | null;
  lines: LineMessage[];
  slideLabels: string[];
  isBlank: boolean;
  next?: string | null;
  showTitle?: boolean;
  nextLines?: LineMessage[] | null;
  isVideo?: boolean;
  isKiosk?: boolean;
}

export type ItemKind = "song" | "text" | "presentation" | "video" | "kiosk";

export interface PlanItemMessage {
  id: string;
  songId: string;
  number?: number | null;
  title: string;
  kind: ItemKind;
  note?: string | null;
  slideCount: number;
}

export interface PlanMessage {
  type: "plan";
  id?: string | null;
  name?: string | null;
  date?: string | null;
  planKind?: "event" | "template" | null;
  items: PlanItemMessage[];
}

export interface PlanSummaryMessage {
  id: string;
  name: string;
  date?: string | null;
  kind: "event" | "template";
  count: number;
  isOpen: boolean;
}

export interface PlansMessage {
  type: "plans";
  events: PlanSummaryMessage[];
  templates: PlanSummaryMessage[];
  openId?: string | null;
}

export interface PartMessage {
  code: string;
  name: string;
  sung: boolean;
  lines: LineMessage[];
}

export interface ItemMessage {
  itemId: string;
  kind: ItemKind;
  title: string;
  number?: number | null;
  note?: string | null;
  slideCount: number;
  category?: string | null;
  origin?: "local" | "modified" | null;
  canReorder?: boolean;
  parts?: PartMessage[] | null;
  removed?: { code: string; name: string }[] | null;
}

export interface VideoMessage {
  type: "video";
  isLoaded: boolean;
  isPlaying: boolean;
  position: number;
  duration: number;
  error?: string | null;
  pictureOffsetMs?: number;
}

export interface KioskMessage {
  type: "kiosk";
  itemId?: string | null;
  index: number;
  count: number;
  isRunning: boolean;
  title?: string | null;
  text?: string | null;
  image?: string | null;
}

export interface DeviceMessage {
  type: "device";
  canAddSongs: boolean;
  isPanel: boolean;
}

export interface SongHit {
  songId: string;
  number?: number | null;
  title: string;
  firstLine: string;
  isText?: boolean;
}

export interface LayoutEntry {
  code: string;
  sung: boolean;
}

export interface CommandReply {
  ok: boolean;
  error?: string | null;
  state?: Omit<StateMessage, "type">;
  plan?: Omit<PlanMessage, "type">;
  results?: SongHit[];
  video?: Omit<VideoMessage, "type">;
  device?: Omit<DeviceMessage, "type">;
  added?: PlanItemMessage;
  kiosk?: Omit<KioskMessage, "type">;
  plans?: Omit<PlansMessage, "type">;
  item?: ItemMessage;
}

export type Notification = StateMessage | PlanMessage | PlansMessage | VideoMessage | KioskMessage | DeviceMessage;

/** Polecenia panelu (pole `cmd` i parametry). */
export interface Command {
  cmd: string;
  slide?: number;
  itemId?: string;
  songId?: string;
  query?: string;
  seconds?: number;
  milliseconds?: number;
  planId?: string;
  name?: string;
  date?: string | null;
  kind?: "event" | "template";
  index?: number;
  layout?: LayoutEntry[];
}
