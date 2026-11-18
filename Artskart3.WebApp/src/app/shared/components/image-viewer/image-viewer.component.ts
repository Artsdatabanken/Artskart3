import { afterRenderEffect, Component, CUSTOM_ELEMENTS_SCHEMA, ElementRef, input, output, viewChild } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ImageInfoComponent } from '../image-info/image-info.component';
import { ViewerImage } from './image-source';

@Component({
  imports: [TranslateModule, ImageInfoComponent],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  selector: 'app-image-viewer',
  styleUrl: './image-viewer.component.css',
  templateUrl: './image-viewer.component.html',
  host: {
    // On the document: a step button that gets disabled at the end of the list takes the focus with it.
    '(document:keydown.arrowleft)': 'showPrevious()',
    '(document:keydown.arrowright)': 'showNext()',
  },
})
export class ImageViewerComponent {
  readonly image = input<ViewerImage | null>(null);
  readonly hasPrevious = input(false);
  readonly hasNext = input(false);
  readonly closed = output<void>();
  readonly failed = output<void>();
  readonly previous = output<void>();
  readonly next = output<void>();
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly closeButton = viewChild.required<ElementRef<HTMLElement>>('closeButton');

  constructor() {
    afterRenderEffect(() => {
      const dialog = this.dialog().nativeElement;
      if (this.image()) {
        if (dialog.open) return;
        dialog.showModal();
        // Autofocus can't reach the button inside the shadow root.
        this.closeButton().nativeElement.shadowRoot?.querySelector('button')?.focus();
      } else if (dialog.open) dialog.close();
    });
  }

  cancel(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.closed.emit();
  }

  showPrevious(): void {
    if (this.hasPrevious()) this.previous.emit();
  }

  showNext(): void {
    if (this.hasNext()) this.next.emit();
  }
}
