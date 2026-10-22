// Klient tailcat-link — ten sam co na stronie pilota (app/web/remote/lib/tailcat, bez typów TypeScript).
// tweetnacl dochodzi z paczki npm (lib/tailcat/nacl.js importuje go, gdy nie ma globalnego).
// @ts-expect-error — moduł JS bez deklaracji typów
import * as tailcat from "../../remote/lib/tailcat/index.js";

export interface LinkStore {
  load(appName: string): Promise<unknown>;
  save(appName: string, state: unknown): Promise<void>;
  remove(appName: string): Promise<void>;
}

export interface Link {
  request(text: string): Promise<string>;
  onNotify(handler: (text: string) => void): void;
  events: EventTarget;
  close(): Promise<void>;
}

interface JoinOptions {
  appName: string;
  invitationCode?: string | null;
  displayName?: string;
  derpMap: string;
  store: LinkStore;
}

export const TailcatLink = tailcat.TailcatLink as { join(options: JoinOptions): Promise<Link> };
export const PairingRefusedError = tailcat.PairingRefusedError as new (...args: unknown[]) => Error;
const IndexedDbStore = tailcat.IndexedDbStore as LinkStore;

/**
 * Panel ma własną tożsamość urządzenia, osobną od pilota na tej samej stronie (ta sama domena = ta sama baza
 * IndexedDB): aplikacja nadaje uprawnienia według klucza urządzenia, więc telefon-pilot w tej przeglądarce nie staje
 * się panelem i odwrotnie.
 */
export const panelStore: LinkStore = {
  load: (appName) => IndexedDbStore.load(`${appName}/panel`),
  save: (appName, state) => IndexedDbStore.save(`${appName}/panel`, state),
  remove: (appName) => IndexedDbStore.remove(`${appName}/panel`),
};
