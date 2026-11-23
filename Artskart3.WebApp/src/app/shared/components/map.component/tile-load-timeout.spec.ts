import type Tile from 'ol/Tile';
import { loadTileWithStallTimeout } from './tile-load-timeout';

describe('loadTileWithStallTimeout', () => {
  const src = 'https://tiles.test/14/1/2.png';
  const stallMs = 1000;
  const originalCreateObjectURL = URL.createObjectURL;
  const originalRevokeObjectURL = URL.revokeObjectURL;
  let image: HTMLImageElement;
  let tile: Tile;
  let signal: AbortSignal | undefined;

  /** Answers the download with one chunk per entry in `chunkDelays`, each after that many ms; Infinity stalls. */
  const respondWith = (chunkDelays: number[], status = 200) => {
    vi.stubGlobal('fetch', vi.fn((_url: string, init?: RequestInit) => {
      signal = init?.signal ?? undefined;
      let next = 0;
      const read = () => new Promise<ReadableStreamReadResult<Uint8Array>>((resolve, reject) => {
        signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
        if (next === chunkDelays.length) return resolve({ done: true, value: undefined });
        const delay = chunkDelays[next++];
        if (delay !== Infinity) setTimeout(() => resolve({ done: false, value: new Uint8Array([1]) }), delay);
      });
      return Promise.resolve({
        ok: status === 200,
        status,
        headers: { get: () => 'image/png' },
        body: { getReader: () => ({ read }) },
      } as unknown as Response);
    }));
  };

  beforeEach(() => {
    vi.useFakeTimers();
    URL.createObjectURL = vi.fn(() => 'blob:tile');
    URL.revokeObjectURL = vi.fn();
    image = new Image();
    tile = { getImage: () => image } as unknown as Tile;
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    URL.createObjectURL = originalCreateObjectURL;
    URL.revokeObjectURL = originalRevokeObjectURL;
  });

  it('shows the tile once the download completes', async () => {
    respondWith([0, 0]);

    loadTileWithStallTimeout(stallMs)(tile, src);
    await vi.advanceTimersByTimeAsync(10);

    expect(fetch).toHaveBeenCalledWith(src, expect.objectContaining({ signal: expect.any(AbortSignal) }));
    expect(image.getAttribute('src')).toBe('blob:tile');
  });

  it('lets a slow download finish while data keeps arriving', async () => {
    respondWith([800, 800, 800]);

    loadTileWithStallTimeout(stallMs)(tile, src);
    await vi.advanceTimersByTimeAsync(2500);

    expect(signal?.aborted).toBe(false);
    expect(image.getAttribute('src')).toBe('blob:tile');
  });

  it('gives up on a download that stops receiving data', async () => {
    respondWith([100, Infinity]);

    loadTileWithStallTimeout(stallMs)(tile, src);
    await vi.advanceTimersByTimeAsync(100 + stallMs);

    expect(signal?.aborted).toBe(true);
    expect(image.getAttribute('src')).toBe('');
  });

  it('gives up when the server answers with an error', async () => {
    respondWith([], 500);

    loadTileWithStallTimeout(stallMs)(tile, src);
    await vi.advanceTimersByTimeAsync(0);

    expect(image.getAttribute('src')).toBe('');
  });
});
