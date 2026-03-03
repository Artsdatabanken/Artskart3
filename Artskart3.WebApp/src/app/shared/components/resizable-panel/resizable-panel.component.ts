import {
  Component,
  Input,
  Output,
  EventEmitter,
  HostListener,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-resizable-panel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './resizable-panel.component.html',
  styleUrl: './resizable-panel.component.css',
})
export class ResizablePanelComponent {
  @Input() initialWidth: number = 280;
  @Input() minWidth: number = 200;
  @Input() maxWidth: number = 500;
  @Input() isDraggable: boolean = true;

  @Output() widthChanged = new EventEmitter<number>();

  currentWidth = signal<number>(this.minWidth);
  isResizing = signal(false);
  private dragStartX = 0;
  private dragStartWidth = 0;

  ngOnInit() {
    this.currentWidth.set(this.initialWidth);
  }

  onResizeStart(event: MouseEvent) {
    if (!this.isDraggable) return;

    event.preventDefault();
    this.isResizing.set(true);
    this.dragStartX = event.clientX;
    this.dragStartWidth = this.currentWidth();
  }

  @HostListener('document:mousemove', ['$event'])
  onMouseMove(event: MouseEvent) {
    if (!this.isResizing()) return;

    const delta = event.clientX - this.dragStartX;
    let newWidth = this.dragStartWidth + delta;
    newWidth = Math.max(this.minWidth, Math.min(this.maxWidth, newWidth));
    this.currentWidth.set(newWidth);
    this.widthChanged.emit(newWidth);
  }

  @HostListener('document:mouseup')
  onMouseUp() {
    if (this.isResizing()) {
      this.isResizing.set(false);
    }
  }
}
