// jsdom does not implement ResizeObserver, which OpenLayers' Map constructor requires.
class ResizeObserverStub {
  observe(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
  unobserve(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
  disconnect(): void {} // eslint-disable-line @typescript-eslint/no-empty-function
}

globalThis.ResizeObserver = ResizeObserverStub as unknown as typeof ResizeObserver;
