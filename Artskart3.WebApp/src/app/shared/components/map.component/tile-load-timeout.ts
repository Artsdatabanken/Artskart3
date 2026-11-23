import type ImageTile from 'ol/ImageTile';
import type { LoadFunction } from 'ol/Tile';

/**
 * Downloads a tile but gives up once no data has arrived for `stallMs`. OpenLayers has no timeout
 * of its own, so a stalled download would hold one of its few tile loading slots until the page is
 * reloaded. Slow downloads that keep receiving data are left to finish.
 */
export function loadTileWithStallTimeout(stallMs: number): LoadFunction {
  return (tile, src) => {
    const image = (tile as ImageTile).getImage() as HTMLImageElement;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    const restartTimer = () => {
      clearTimeout(timer);
      timer = setTimeout(() => controller.abort(), stallMs);
    };
    restartTimer();

    download(src, controller.signal, restartTimer)
      .then((blob) => {
        const url = URL.createObjectURL(blob);
        const revoke = () => URL.revokeObjectURL(url);
        image.addEventListener('load', revoke, { once: true });
        image.addEventListener('error', revoke, { once: true });
        image.src = url;
      })
      .catch(() => {
        // An empty source makes the image fire `error`, which OpenLayers handles as a failed tile.
        image.src = '';
      })
      .finally(() => clearTimeout(timer));
  };
}

async function download(src: string, signal: AbortSignal, onData: () => void): Promise<Blob> {
  const response = await fetch(src, { signal });
  if (!response.ok || !response.body) throw new Error(`Tile request failed with status ${response.status}`);
  // Response bodies always yield chunks backed by a plain ArrayBuffer.
  const reader = response.body.getReader() as ReadableStreamDefaultReader<Uint8Array<ArrayBuffer>>;
  const chunks: Uint8Array<ArrayBuffer>[] = [];
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    chunks.push(value);
    onData();
  }
  return new Blob(chunks, { type: response.headers.get('content-type') ?? 'image/png' });
}
