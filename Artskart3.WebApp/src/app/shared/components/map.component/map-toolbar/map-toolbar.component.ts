import { Component, Output, EventEmitter, Input } from '@angular/core';

@Component({
  selector: 'app-map-toolbar',
  standalone: false,
  templateUrl: './map-toolbar.component.html',
  styleUrl: './map-toolbar.component.css',
})
export class MapToolbarComponent {
  @Input() map: any;
  @Input() mapEl!: HTMLDivElement;
  @Output() iconClick = new EventEmitter<string>();

  onButtonClick(iconName: string): void {
    this.handleIconClick(iconName);
  }

  private handleIconClick(iconName: string): void {
    if (iconName === 'plus') {
      const { zoom } = this.map.getCamera();
      this.map.setZoom(zoom + 1);
    } else if (iconName === 'minus') {
      const { zoom } = this.map.getCamera();
      this.map.setZoom(zoom - 1);
    } else if (iconName === 'posisjon') {
      this.map.zoomToGeolocation();
    } else if (iconName === 'expand') {
      this.toggleFullscreen();
    } else {
      this.iconClick.emit(iconName);
    }
  }

  private toggleFullscreen(): void {
    const mapContainer = this.mapEl;
    if (!document.fullscreenElement) {
      mapContainer.requestFullscreen().catch((err: any) => {
        console.error('Error attempting to enable fullscreen:', err);
      });
    } else {
      document.exitFullscreen().catch((err: any) => {
        console.error('Error attempting to exit fullscreen:', err);
      });
    }
  }
}
