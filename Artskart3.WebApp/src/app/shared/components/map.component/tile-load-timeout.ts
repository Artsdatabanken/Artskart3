import type ImageTile from 'ol/ImageTile';
import type { LoadFunction } from 'ol/Tile';
import TileState from 'ol/TileState';

/**
 * Loads an image tile but gives up after `timeoutMs`. OpenLayers has no timeout of its own, so a
 * stalled download would hold one of its few tile loading slots until the page is reloaded.
 */
export function loadTileWithTimeout(timeoutMs: number): LoadFunction {
  return (tile, src) => {
    const image = (tile as ImageTile).getImage() as HTMLImageElement;
    const timer = setTimeout(() => {
      if (tile.getState() !== TileState.LOADING) return;
      image.src = '';
      // Clearing src cancels the download, but OpenLayers isn't told, so the tile is failed here.
      tile.setState(TileState.ERROR);
    }, timeoutMs);
    const stopTimer = () => clearTimeout(timer);
    image.addEventListener('load', stopTimer, { once: true });
    image.addEventListener('error', stopTimer, { once: true });
    image.src = src;
  };
}
