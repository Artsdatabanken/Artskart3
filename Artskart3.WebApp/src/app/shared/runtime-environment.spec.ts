import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { RUNTIME_ENVIRONMENT } from './runtime-environment';

type TestWindow = Window & { artskartEnv?: { environmentName?: string } };

describe('RUNTIME_ENVIRONMENT', () => {
  afterEach(() => {
    delete (window as TestWindow).artskartEnv;
  });

  it('uses the environment name from env.js', () => {
    (window as TestWindow).artskartEnv = { environmentName: 'staging' };

    expect(TestBed.inject(RUNTIME_ENVIRONMENT).environmentName).toBe('staging');
  });

  it('falls back to the build-time environment name when env.js sets none', () => {
    expect(TestBed.inject(RUNTIME_ENVIRONMENT).environmentName).toBe(environment.environmentName);
  });

  it('falls back to the build-time environment name when the value is empty', () => {
    (window as TestWindow).artskartEnv = { environmentName: '' };

    expect(TestBed.inject(RUNTIME_ENVIRONMENT).environmentName).toBe(environment.environmentName);
  });
});
