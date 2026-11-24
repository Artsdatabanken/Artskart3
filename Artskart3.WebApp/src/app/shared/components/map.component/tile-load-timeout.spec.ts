import type Tile from 'ol/Tile';
import TileState from 'ol/TileState';
import { loadTileWithTimeout } from './tile-load-timeout';

describe('loadTileWithTimeout', () => {
  const src = 'https://tiles.test/14/1/2.png';
  const timeoutMs = 1000;
  let image: HTMLImageElement;
  let state: number;
  let tile: Tile;

  beforeEach(() => {
    vi.useFakeTimers();
    image = new Image();
    state = TileState.LOADING;
    tile = { getImage: () => image, getState: () => state, setState: vi.fn() } as unknown as Tile;
  });

  afterEach(() => vi.useRealTimers());

  it('starts the download', () => {
    loadTileWithTimeout(timeoutMs)(tile, src);

    expect(image.getAttribute('src')).toBe(src);
  });

  it('cancels a download that takes too long and marks the tile as failed', () => {
    loadTileWithTimeout(timeoutMs)(tile, src);

    vi.advanceTimersByTime(timeoutMs);

    expect(image.getAttribute('src')).toBe('');
    expect(tile.setState).toHaveBeenCalledWith(TileState.ERROR);
  });

  it('leaves a tile that loaded in time alone', () => {
    loadTileWithTimeout(timeoutMs)(tile, src);

    image.dispatchEvent(new Event('load'));
    vi.advanceTimersByTime(timeoutMs);

    expect(image.getAttribute('src')).toBe(src);
    expect(tile.setState).not.toHaveBeenCalled();
  });

  it('leaves a tile that is no longer loading alone', () => {
    loadTileWithTimeout(timeoutMs)(tile, src);

    state = TileState.EMPTY;
    vi.advanceTimersByTime(timeoutMs);

    expect(image.getAttribute('src')).toBe(src);
    expect(tile.setState).not.toHaveBeenCalled();
  });
});
