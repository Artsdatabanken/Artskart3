import { Injectable, inject } from "@angular/core";
import { DOCUMENT } from "@angular/common";
import { ApplicationInsights } from "@microsoft/applicationinsights-web";
import { AngularPlugin } from "@microsoft/applicationinsights-angularplugin-js";
import { environment } from "../../environments/environment";

interface CookieInformationWindow {
    CookieInformation?: {
        getConsentGivenFor(category: string): boolean;
    };
}

@Injectable()
export class LoggingService {
    private appInsights: ApplicationInsights | null = null;
    private window: (Window & CookieInformationWindow) | null;

    constructor() {
        this.window = inject(DOCUMENT).defaultView;

        if (!environment.production) {
            this.initAppInsights();
            return;
        }

        this.window?.addEventListener('CookieInformationConsentGiven', () => {
            if (this.hasStatisticsConsent()) {
                this.initAppInsights();
            }
        });
    }

    private hasStatisticsConsent(): boolean {
        return this.window?.CookieInformation?.getConsentGivenFor('cookie_cat_statistic') ?? false;
    }

    private initAppInsights(): void {
        if (this.appInsights) {
            return;
        }

        const angularPlugin = new AngularPlugin();
        this.appInsights = new ApplicationInsights({
            config: {
                connectionString: environment.applicationInsights.connectionString,
                enableCorsCorrelation: true,
                extensions: [angularPlugin],
                enableAutoRouteTracking: true,
            }
        });
        this.appInsights.loadAppInsights();
    }

    logPageView(name?: string, url?: string) {
        this.appInsights?.trackPageView({ name, uri: url });
    }

    logEvent(name: string, properties?: Record<string, unknown>) {
        this.appInsights?.trackEvent({ name }, properties);
    }

    logMetric(name: string, average: number, properties?: Record<string, unknown>) {
        this.appInsights?.trackMetric({ name, average }, properties);
    }

    logException(exception: Error, properties?: Record<string, unknown>) {
        this.appInsights?.trackException({ exception }, properties);
    }

    logTrace(message: string, properties?: Record<string, unknown>) {
        this.appInsights?.trackTrace({ message }, properties);
    }
}