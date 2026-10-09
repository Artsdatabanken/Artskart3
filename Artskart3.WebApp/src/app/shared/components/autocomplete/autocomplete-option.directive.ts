import { Directive, TemplateRef, inject, input } from '@angular/core';
import { Observable } from 'rxjs';

export type AutocompleteSearch<T> = (term: string) => Observable<T[]>;

export interface AutocompleteOptionContext<T> {
  $implicit: T;
  term: string;
}

/**
 * The template for one option in `<app-autocomplete>`. Bind it to the same search
 * function as the autocomplete so `let-item` is typed: `[appAutocompleteOption]="search"`.
 */
@Directive({ selector: 'ng-template[appAutocompleteOption]' })
export class AutocompleteOptionDirective<T> {
  readonly search = input.required<AutocompleteSearch<T>>({ alias: 'appAutocompleteOption' });
  readonly template = inject<TemplateRef<AutocompleteOptionContext<T>>>(TemplateRef);

  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- the parameters only exist for the type predicate
  static ngTemplateContextGuard<T>(_directive: AutocompleteOptionDirective<T>, context: unknown): context is AutocompleteOptionContext<T> {
    return true;
  }
}
