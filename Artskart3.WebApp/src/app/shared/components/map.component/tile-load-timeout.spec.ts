import type Tile from 'ol/Tile';
import TileState from 'ol/TileState';
import { loadTileWithTimeout } from './tile-load-timeout';

describe('loadTileWithTimeout', () => {
  const src = 'https://tiles.test/14/1/2.png';
  let image: HTMLImageElement;
  let tile: Tile;

  beforeEach(() => {
    vi.useFakeTimers();
    image = new Image();
    tile = { getImage: () => image, setState: vi.fn() } as unknown as Tile;
  });

  afterEach(() => vi.useRealTimers());

  it('starts the download', () => {
    loadTileWithTimeout(1000)(tile, src);

    expect(image.getAttribute('src')).toBe(src);
  });

  it('cancels a download that stalls and marks the tile as failed', () => {
    loadTileWithTimeout(1000)(tile, src);

    vi.advanceTimersByTime(1000);

    expect(image.getAttribute('src')).toBe('');
    expect(tile.setState).toHaveBeenCalledWith(TileState.ERROR);
  });

  it('leaves a tile that loaded in time alone', () => {
    loadTileWithTimeout(1000)(tile, src);

    image.dispatchEvent(new Event('load'));
    vi.advanceTimersByTime(1000);

    expect(image.getAttribute('src')).toBe(src);
    expect(tile.setState).not.toHaveBeenCalled();
  });
});
