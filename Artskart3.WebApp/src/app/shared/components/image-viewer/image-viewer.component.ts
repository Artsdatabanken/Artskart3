import { afterRenderEffect, Component, ElementRef, input, output, viewChild } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ViewerImage, webUrl } from './image-source';

@Component({
  imports: [TranslateModule],
  selector: 'app-image-viewer',
  styleUrl: './image-viewer.component.css',
  templateUrl: './image-viewer.component.html',
})
export class ImageViewerComponent {
  readonly image = input<ViewerImage | null>(null);
  readonly closed = output<void>();
  readonly failed = output<void>();
  readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly webUrl = webUrl;

  constructor() {
    afterRenderEffect(() => {
      const dialog = this.dialog().nativeElement;
      if (this.image()) {
        if (!dialog.open) dialog.showModal();
      } else if (dialog.open) dialog.close();
    });
  }

  cancel(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.closed.emit();
  }
}
