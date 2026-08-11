import { inject, Injectable, Injector } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';

@Injectable({
  providedIn: 'root'
})
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly translate = inject(TranslateService);
  private readonly injector = inject(Injector);

  private get router(): Router {
    return this.injector.get(Router);
  }

  constructor() {
    super();

    Promise.resolve().then(() => {
      this.translate.onLangChange.subscribe(() => {
        this.updateTitle(this.router.routerState.snapshot);
      });
    });
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const title = this.buildTitle(snapshot);

    if (typeof title === 'string') {
      const translatedTitle = this.translate.instant(title);
      this.title.setTitle(translatedTitle !== title ? translatedTitle : title);
    }
  }
}
