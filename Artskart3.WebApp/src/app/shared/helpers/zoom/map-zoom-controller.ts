import { ZoomCalculator } from './zoom-calculator';

export class MapZoomController {
  constructor(private map: any) {}

  getCurrentZoom(): number | null {
    try {
      if (typeof this.map.getZoom === 'function') {
        return this.map.getZoom();
      } else if (typeof this.map.zoomTo === 'function' && this.map.zoom !== undefined) {
        return this.map.zoom;
      } else if (this.map._map?.getZoom) {
        return this.map._map.getZoom();
      } else if (this.map.zoom !== undefined) {
        return this.map.zoom;
      }
    } catch (error) {
      return null;
    }
    return null;
  }

  setZoom(zoom: number): void {
    if (!this.map || isNaN(zoom)) return;

    try {
      const minZoom = this.map?.minZoom ?? 0;
      const maxZoom = this.map?.maxZoom ?? 18;
      const constrainedZoom = ZoomCalculator.constrainZoom(zoom, minZoom, maxZoom);

      if (typeof this.map.setZoom === 'function') {
        this.map.setZoom(constrainedZoom);
      } else if (typeof this.map.zoomTo === 'function') {
        this.map.zoomTo(constrainedZoom, { duration: 0 });
      } else if (this.map._map?.setZoom) {
        this.map._map.setZoom(constrainedZoom);
      } else {
        this.map.zoom = constrainedZoom;
      }
    } catch (error) {
    }
  }

  getMinZoom(): number {
    return this.map?.minZoom ?? 0;
  }

  getMaxZoom(): number {
    return this.map?.maxZoom ?? 18;
  }

  getLayerById(layerId: string): any {
    return this.map.getLayerById?.(layerId) || null;
  }

  removeLayer(layerId: string): void {
    try {
      this.map.removeLayer(layerId);
    } catch (e) {
    }
  }
}
