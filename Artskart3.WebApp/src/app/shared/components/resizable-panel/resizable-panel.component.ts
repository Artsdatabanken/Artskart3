import { Component, input, output, signal, OnInit } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-resizable-panel',
  imports: [TranslateModule],
  templateUrl: './resizable-panel.component.html',
  styleUrl: './resizable-panel.component.css',
  host: {
    '(document:mousemove)': 'onMouseMove($event)',
    '(document:mouseup)': 'onMouseUp()',
  },
})
export class ResizablePanelComponent implements OnInit {
  readonly initialWidth = input(358);
  readonly minWidth = input(200);
  readonly maxWidth = input(500);
  readonly isDraggable = input(true);

  readonly widthChanged = output<number>();

  currentWidth = signal(0);
  isResizing = signal(false);
  private dragStartX = 0;
  private dragStartWidth = 0;

  ngOnInit() {
    this.currentWidth.set(this.initialWidth());
  }

  onResizeStart(event: MouseEvent) {
    if (!this.isDraggable()) return;

    event.preventDefault();
    this.isResizing.set(true);
    this.dragStartX = event.clientX;
    this.dragStartWidth = this.currentWidth();
  }

  onMouseMove(event: MouseEvent) {
    if (!this.isResizing()) return;

    const delta = event.clientX - this.dragStartX;
    let newWidth = this.dragStartWidth + delta;
    newWidth = Math.max(this.minWidth(), Math.min(this.maxWidth(), newWidth));
    this.currentWidth.set(newWidth);
    this.widthChanged.emit(newWidth);
  }

  onMouseUp() {
    if (this.isResizing()) {
      this.isResizing.set(false);
    }
  }
}
