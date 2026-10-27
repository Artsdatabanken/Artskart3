// jsdom does not implement ResizeObserver, which OpenLayers' Map constructor requires.
class ResizeObserverStub {
  observe(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
  unobserve(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
  disconnect(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
}

globalThis.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;

// Newer Node versions ship a global localStorage that shadows jsdom's and is undefined without --localstorage-file.
if (typeof globalThis.localStorage === 'undefined') {
  const items = new Map<string, string>();
  const memoryStorage: Storage = {
    get length() {
      return items.size;
    },
    clear: () => items.clear(),
    getItem: key => items.get(key) ?? null,
    key: index => Array.from(items.keys())[index] ?? null,
    removeItem: key => void items.delete(key),
    setItem: (key, value) => void items.set(key, String(value)),
  };
  Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: memoryStorage });
}

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
