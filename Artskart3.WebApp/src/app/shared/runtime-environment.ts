import { InjectionToken, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { environment } from '../../environments/environment';

export interface RuntimeEnvironment {
  environmentName: string;
  // Unknown names count as production, so the public site never shows pre-release UI by mistake.
  isProduction: boolean;
}

const PRE_RELEASE_ENVIRONMENTS = ['local', 'test', 'staging'];

// Set by /env.js. In the web container nginx generates it from APP_* environment
// variables; elsewhere public/env.js leaves it empty and the build-time values apply.
interface RuntimeEnvironmentWindow {
  artskartEnv?: { environmentName?: string };
}

export const RUNTIME_ENVIRONMENT = new InjectionToken<RuntimeEnvironment>('RUNTIME_ENVIRONMENT', {
  providedIn: 'root',
  factory: () => {
    const runtimeValues = (inject(DOCUMENT).defaultView as RuntimeEnvironmentWindow | null)?.artskartEnv;
    const environmentName = runtimeValues?.environmentName || environment.environmentName;
    return {
      environmentName,
      isProduction: !PRE_RELEASE_ENVIRONMENTS.includes(environmentName),
    };
  },
});
