import { Injectable, inject } from "@angular/core";
import { DOCUMENT } from "@angular/common";
import { ApplicationInsights } from "@microsoft/applicationinsights-web";
import { AngularPlugin } from "@microsoft/applicationinsights-angularplugin-js";
import { environment } from "../../environments/environment";

/**
 * Log level enumeration
 */
export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3
}

/** Logger configuration constants */
const LOGGER_CONFIG = {
  EnableDebugLogging: true,
  LogLevelFilter: 'INFO',
} as const;

interface CookieInformationWindow {
    CookieInformation?: {
        getConsentGivenFor(category: string): boolean;
    };
}

@Injectable({ providedIn: 'root' })
export class LoggingService {
    private appInsights: ApplicationInsights | null = null;
    private window: (Window & CookieInformationWindow) | null;
    private logLevel: LogLevel = this.parseLogLevel(LOGGER_CONFIG.LogLevelFilter);
    private enableDebugLogging = LOGGER_CONFIG.EnableDebugLogging;

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
        try {
            this.appInsights.loadAppInsights();
        } catch (error: unknown) {
            console.error('Failed to initialize Application Insights', error);
        }
    }

    /**
     * Log debug message (console only)
     */
    debug(message: string, context?: string, data?: unknown): void {
        if (this.canLog(LogLevel.DEBUG)) {
            if (data !== undefined) {
                console.debug(`[${context || 'DEBUG'}] ${message}`, data);
            } else {
                console.debug(`[${context || 'DEBUG'}] ${message}`);
            }
        }
    }

    /**
     * Log info message (console + Application Insights)
     */
    info(message: string, context?: string, data?: unknown): void {
        if (this.canLog(LogLevel.INFO)) {
            if (data !== undefined) {
                console.info(`[${context || 'INFO'}] ${message}`, data);
            } else {
                console.info(`[${context || 'INFO'}] ${message}`);
            }
            this.appInsights?.trackTrace({ message }, { level: 'INFO', context, data: data?.toString() });
        }
    }

    /**
     * Log warning message (console + Application Insights)
     */
    warn(message: string, context?: string, data?: unknown): void {
        if (this.canLog(LogLevel.WARN)) {
            if (data !== undefined) {
                console.warn(`[${context || 'WARN'}] ${message}`, data);
            } else {
                console.warn(`[${context || 'WARN'}] ${message}`);
            }
            this.appInsights?.trackTrace({ message }, { level: 'WARN', context, data: data?.toString() });
        }
    }

    /**
     * Log error message (console + Application Insights exception tracking)
     */
    error(message: string, context?: string, error?: unknown): void {
        if (this.canLog(LogLevel.ERROR)) {
            if (error !== undefined) {
                console.error(`[${context || 'ERROR'}] ${message}`, error);
            } else {
                console.error(`[${context || 'ERROR'}] ${message}`);
            }
            // Track error in Application Insights
            this.appInsights?.trackException({
                exception: new Error(message),
                severityLevel: 3,
                properties: { context, originalError: error?.toString() }
            });
        }
    }

    /**
     * Log page view to Application Insights
     */
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

    setLogLevel(level: LogLevel | string): void {
        this.logLevel = typeof level === 'string' ? this.parseLogLevel(level) : level;
    }

    private canLog(level: LogLevel): boolean {
        if (!this.enableDebugLogging && level === LogLevel.DEBUG) {
            return false;
        }
        return level >= this.logLevel;
    }

    private parseLogLevel(level: string): LogLevel {
        switch (level.toUpperCase()) {
            case 'DEBUG':
                return LogLevel.DEBUG;
            case 'INFO':
                return LogLevel.INFO;
            case 'WARN':
                return LogLevel.WARN;
            case 'ERROR':
                return LogLevel.ERROR;
            default:
                return LogLevel.INFO;
        }
    }
}
