import { Component, computed, CUSTOM_ELEMENTS_SCHEMA, effect, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Menu, MenuItem, MenuTrigger } from '@angular/aria/menu';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { LoggingService } from '@shared/logging.service';
import { LocaleDatePipe } from '@shared/pipes/locale-date.pipe';
import { ObservationListInfoDto } from '@shared/types/api.types';
import { ObservationTreeComponent } from '../observation-tree/observation-tree.component';
import {
  buildObservationTree,
  hasUnknownCategory,
  observationTreeIds,
  ObservationGrouping,
  ObservationRequestState,
  ObservationSelection,
} from './observation-list.model';

@Component({
  selector: 'app-observation-list',
  imports: [TranslateModule, ObservationTreeComponent, Menu, MenuItem, MenuTrigger],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
  templateUrl: './observation-list.component.html',
  styleUrl: './observation-list.component.css',
})
export class ObservationListComponent {
  readonly observationList = input<ObservationListInfoDto[]>([]);
  readonly selection = input.required<ObservationSelection>();
  readonly requestState = input<ObservationRequestState>('ready');
  readonly retry = output<void>();
  readonly observationActivated = output<number>();
  readonly filters: ObservationGrouping[] = ['taxonGroup', 'location', 'redList', 'alienSpecies'];
  readonly currentFilter = signal<ObservationGrouping>('taxonGroup');

  private readonly translate = inject(TranslateService);
  private readonly logger = inject(LoggingService);
  private readonly languageChange = toSignal(this.translate.onLangChange);
  private readonly datePipe = new LocaleDatePipe();

  readonly groups = computed(() => {
    const language = this.languageChange()?.lang ?? this.translate.getCurrentLang();
    return buildObservationTree(
      this.observationList(),
      this.currentFilter(),
      language,
      (key) => this.translate.instant(key),
      (date) => this.datePipe.transform(date, language),
    );
  });
  readonly resetKey = computed(() => `${this.selection().key}/${this.currentFilter()}`);
  readonly orderedObservationIds = computed(() => observationTreeIds(this.groups()));
  readonly location = computed(() => {
    if (this.selection().locationIds.length !== 1) return null;
    const observations = this.observationList();
    const unique = (field: 'locality' | 'municipalityName' | 'countyName') => {
      const names = new Set(observations.map((o) => o[field]?.trim() || null));
      return names.size === 1 ? [...names][0] : null;
    };
    return { name: unique('locality'), areas: [unique('municipalityName'), unique('countyName')].filter(Boolean).join(', ') };
  });

  constructor() {
    effect(() => {
      const unknown = this.observationList().filter(hasUnknownCategory);
      if (unknown.length)
        this.logger.warn(
          'Unknown observation assessment metadata',
          'ObservationList',
          unknown.map((o) => o.id),
        );
    });
  }

  setGrouping(value: unknown): void {
    const grouping = this.filters.find((filter) => filter === value);
    if (!grouping) {
      this.logger.error('Invalid observation grouping', 'ObservationList', value);
      return;
    }
    this.currentFilter.set(grouping);
  }
}
