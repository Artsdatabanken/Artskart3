import OlMap from 'ol/Map';
import { ArtskartFullscreenControl } from './fullscreen.control';

describe('ArtskartFullscreenControl tooltip', () => {
  const labels = { tipLabel: 'Fullscreen', tooltip: 'Show the map in fullscreen', exitTooltip: 'Exit fullscreen' };

  it('describes the button with a tooltip that follows the fullscreen state', () => {
    const control = new ArtskartFullscreenControl(labels);
    const button = control['element'].querySelector('button')!;
    const tooltip = control['element'].querySelector('.map-control-tooltip')!;
    expect(button.title).toBe('');
    expect(button.getAttribute('aria-label')).toBe('Fullscreen');
    expect(button.getAttribute('aria-describedby')).toBe(tooltip.id);
    expect(tooltip.textContent).toBe('Show the map in fullscreen');
    expect(button.getAttribute('aria-pressed')).toBe('false');

    control.dispatchEvent('enterfullscreen');
    expect(tooltip.textContent).toBe('Exit fullscreen');
    expect(button.getAttribute('aria-label')).toBe('Fullscreen');
    expect(button.getAttribute('aria-pressed')).toBe('true');

    control.updateLabels({ ...labels, exitTooltip: 'Avslutt fullskjerm' });
    expect(tooltip.textContent).toBe('Avslutt fullskjerm');

    control.dispatchEvent('leavefullscreen');
    expect(tooltip.textContent).toBe('Show the map in fullscreen');
  });

  it('renders into the given target instead of the map overlay', () => {
    const target = document.createElement('div');
    const control = new ArtskartFullscreenControl(labels, { target });
    new OlMap({ target: document.createElement('div'), controls: [control], interactions: [] });
    expect(control['element'].parentElement).toBe(target);
  });

  it('toggles pseudo-fullscreen and its accessible state when unsupported', () => {
    const source = document.createElement('div');
    const control = new ArtskartFullscreenControl(labels, { source });
    const button = control['element'].querySelector('button')!;
    const tooltip = control['element'].querySelector('.map-control-tooltip')!;
    const initialIcon = button.innerHTML;
    control['element'].classList.add('ol-unsupported');

    button.click();
    expect(source.classList.contains('map-pseudo-fullscreen')).toBe(true);
    expect(button.getAttribute('aria-pressed')).toBe('true');
    expect(tooltip.textContent).toBe('Exit fullscreen');
    expect(button.innerHTML).not.toBe(initialIcon);

    button.click();
    expect(source.classList.contains('map-pseudo-fullscreen')).toBe(false);
    expect(button.getAttribute('aria-pressed')).toBe('false');
    expect(tooltip.textContent).toBe('Show the map in fullscreen');
    expect(button.innerHTML).toBe(initialIcon);
  });

  it('does nothing when the control is not marked unsupported', () => {
    const source = document.createElement('div');
    const control = new ArtskartFullscreenControl(labels, { source });
    const button = control['element'].querySelector('button')!;
    button.click();

    expect(source.classList.contains('map-pseudo-fullscreen')).toBe(false);
  });
});
