import { Injectable } from "@angular/core";
import { ApplicationInsights } from "@microsoft/applicationinsights-web";
import { AngularPlugin } from "@microsoft/applicationinsights-angularplugin-js";
import { Router } from "@angular/router";
import { environment } from "../../environments/environment";

@Injectable()
export class LoggingService {
    appInsights: ApplicationInsights;
    constructor(private router: Router) {
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
        this.appInsights.trackPageView({ name, uri: url });
    }

    logEvent(name: string, properties?: { [key: string]: any }) {
        this.appInsights.trackEvent({ name }, properties);
    }

    logMetric(name: string, average: number, properties?: { [key: string]: any }) {
        this.appInsights.trackMetric({ name, average }, properties);
    }

    logException(exception: Error, properties?: { [key: string]: any }) {
        this.appInsights.trackException({ exception }, properties);
    }

    logTrace(message: string, properties?: { [key: string]: any }) {
        this.appInsights.trackTrace({ message }, properties);
    }
}