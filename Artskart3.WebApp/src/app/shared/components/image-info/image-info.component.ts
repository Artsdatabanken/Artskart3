import { Component, input } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { SpeciesNames, ViewerImage, webUrl } from '../image-viewer/image-source';

export type MediaInfo = Pick<ViewerImage, 'description' | 'rightsHolder' | 'license'>;

@Component({
  selector: 'app-image-info',
  imports: [TranslateModule],
  templateUrl: './image-info.component.html',
  styleUrl: './image-info.component.css',
})
export class ImageInfoComponent {
  readonly names = input<SpeciesNames>({});
  readonly media = input<MediaInfo | undefined>({});
  /** Off where the photographer is already shown next to the info. */
  readonly withRightsHolder = input(true);
  readonly webUrl = webUrl;
}
