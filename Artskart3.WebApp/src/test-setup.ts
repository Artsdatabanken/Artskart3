// jsdom does not implement ResizeObserver, which OpenLayers' Map constructor requires.
class ResizeObserverStub {
  observe(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
  unobserve(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
  disconnect(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
}

globalThis.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;

// jsdom has no native dialog top layer. Real focus containment is exercised in browser checks.
for (const method of ['show', 'showModal'] as const) {
  if (!HTMLDialogElement.prototype[method]) {
    Object.defineProperty(HTMLDialogElement.prototype, method, {
      configurable: true,
      value(this: HTMLDialogElement) {
        this.setAttribute('open', '');
      },
    });
  }
}
if (!HTMLDialogElement.prototype.close) {
  Object.defineProperty(HTMLDialogElement.prototype, 'close', {
    configurable: true,
    value(this: HTMLDialogElement) {
      this.removeAttribute('open');
    },
  });
}
