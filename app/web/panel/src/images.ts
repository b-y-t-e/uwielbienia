import { useEffect, useState } from "react";
import { command } from "./connection";

/** Szerokości jak w aplikacji (RemoteImages): miniatura w kolumnie „Pieśń”, slajd w „Na ekranie”. */
export const THUMBNAIL_WIDTH = 320;
export const SLIDE_WIDTH = 640;

// Obraz slajdu przychodzi na prośbę, raz na klucz i wielkość (klucz zmienia się razem z plikiem w aplikacji).
const images = new Map<string, Promise<string | null>>();

function load(key: string, width: number): Promise<string | null> {
  const id = `${width}:${key}`;
  let loading = images.get(id);
  if (!loading) {
    loading = command("image", { key, width }, { quiet: true }).then((reply) => {
      if (!reply?.image) images.delete(id); // nie udało się — następnym razem jeszcze raz
      return reply?.image ?? null;
    });
    images.set(id, loading);
    if (images.size > 200) images.delete(images.keys().next().value!);
  }
  return loading;
}

/** Obraz slajdu (data URL) albo `null`, póki nie przyszedł / gdy go nie ma. */
export function useSlideImage(key: string | null | undefined, width: number): string | null {
  const [image, setImage] = useState<{ key: string; url: string } | null>(null);
  useEffect(() => {
    if (!key) return;
    let current = true;
    void load(key, width).then((url) => { if (current && url) setImage({ key, url }); });
    return () => { current = false; };
  }, [key, width]);
  return key && image?.key === key ? image.url : null;
}

/** Pobiera zawczasu (następny slajd) — „Dalej” pokazuje go od razu. */
export function preloadSlideImage(key: string | null | undefined, width: number) {
  if (key) void load(key, width);
}
