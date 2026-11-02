import { InjectionToken, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { environment } from '../../environments/environment';

export interface RuntimeEnvironment {
  environmentName: string;
}

// Set by /env.js. In the web container nginx generates it from APP_* environment
// variables; elsewhere public/env.js leaves it empty and the build-time values apply.
interface RuntimeEnvironmentWindow {
  artskartEnv?: Partial<RuntimeEnvironment>;
}

export const RUNTIME_ENVIRONMENT = new InjectionToken<RuntimeEnvironment>('RUNTIME_ENVIRONMENT', {
  providedIn: 'root',
  factory: () => {
    const runtimeValues = (inject(DOCUMENT).defaultView as RuntimeEnvironmentWindow | null)?.artskartEnv;
    return {
      environmentName: runtimeValues?.environmentName || environment.environmentName,
    };
  },
});
