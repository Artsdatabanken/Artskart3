import { ArtskartFullscreenControl } from './fullscreen.control';

class TestableFullscreenControl extends ArtskartFullscreenControl {
  get root(): HTMLElement {
    return this.element;
  }
}

describe('ArtskartFullscreenControl', () => {
  let source: HTMLElement;
  let control: TestableFullscreenControl;
  let button: HTMLButtonElement;

  beforeEach(() => {
    source = document.createElement('div');
    control = new TestableFullscreenControl({ tipLabel: 'Fullskjerm' }, source);
    button = control.root.querySelector('button') as HTMLButtonElement;
  });

  it('toggles the pseudo-fullscreen class when the Fullscreen API is unsupported', () => {
    control.root.classList.add('ol-unsupported');

    button.click();
    expect(source.classList.contains('map-pseudo-fullscreen')).toBe(true);

    button.click();
    expect(source.classList.contains('map-pseudo-fullscreen')).toBe(false);
  });

  it('does nothing when the Fullscreen API is supported', () => {
    button.click();

    expect(source.classList.contains('map-pseudo-fullscreen')).toBe(false);
  });
});
