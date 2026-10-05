import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { NotificationModel } from '../../types/api.types';

interface CookieInformationWindow {
  CookieInformation?: {
    getConsentGivenFor(category: string): boolean;
  };
}

const COOKIE_NAME = 'artskart.notifications.dismissed';
const COOKIE_MAX_AGE_SECONDS = 180 * 24 * 60 * 60;
const NECESSARY_CONSENT_CATEGORY = 'cookie_cat_necessary';

// Limit antall avviste varslinger for å holde cookien under 4KB
const MAX_DISMISSED_KEYS = 50;

/** Remembers dismissed notifications across sessions via a cookie, when the user has consented to necessary cookies. */
@Injectable({
  providedIn: 'root',
})
export class NotificationDismissalService {
  private readonly document = inject(DOCUMENT);
  private readonly window = this.document.defaultView as (Window & CookieInformationWindow) | null;

  private readonly dismissedKeys = signal<ReadonlySet<string>>(this.readDismissedKeys());

  isDismissed(notification: NotificationModel): boolean {
    const key = notification.id;
    return key != null && this.dismissedKeys().has(key);
  }

  dismiss(notification: NotificationModel): void {
    const key = notification.id;
    if (key == null) {
      return;
    }

    const currentKeys = [...this.dismissedKeys()];
    
    // Legg til den nye nøkkelen og behold bare de nyeste MAX_DISMISSED_KEYS
    const updated = [...currentKeys, key]
      .slice(-MAX_DISMISSED_KEYS);
    
    // Oppdater signal med det trimmede settet
    this.dismissedKeys.set(new Set(updated));

    if (this.hasNecessaryConsent()) {
      this.writeCookie(new Set(updated));
    }
  }

  private hasNecessaryConsent(): boolean {
    return this.window?.CookieInformation?.getConsentGivenFor(NECESSARY_CONSENT_CATEGORY) ?? false;
  }

  private readDismissedKeys(): ReadonlySet<string> {
    const entry = this.document.cookie
      .split('; ')
      .find(part => part.startsWith(`${COOKIE_NAME}=`));

    if (!entry) {
      return new Set();
    }

    try {
      const value = decodeURIComponent(entry.substring(COOKIE_NAME.length + 1));
      const parsed: unknown = JSON.parse(value);
      return Array.isArray(parsed)
        ? new Set(parsed.filter((key): key is string => typeof key === 'string'))
        : new Set();
    } catch {
      return new Set();
    }
  }

  private writeCookie(keys: ReadonlySet<string>): void {
    const value = encodeURIComponent(JSON.stringify([...keys]));
    this.document.cookie = `${COOKIE_NAME}=${value}; max-age=${COOKIE_MAX_AGE_SECONDS}; path=/; SameSite=Lax; Secure`;
  }
}