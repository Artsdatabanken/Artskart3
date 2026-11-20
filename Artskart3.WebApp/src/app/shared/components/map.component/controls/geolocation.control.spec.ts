import OlMap from 'ol/Map';
import { GeolocationMapControl } from './geolocation.control';

describe('GeolocationMapControl tooltip', () => {
  const labels = { tipLabel: 'My location', tooltip: 'Find my location', deniedTooltip: 'Location is blocked' };

  const create = () => {
    const control = new GeolocationMapControl(labels, { onClick: () => Promise.resolve() });
    const button = control['element'].querySelector('button')!;
    const tooltip = control['element'].querySelector('.map-control-tooltip')!;
    return { control, button, tooltip };
  };

  it('describes the button with the tooltip', () => {
    const { button, tooltip } = create();
    expect(button.getAttribute('aria-label')).toBe('My location');
    expect(button.getAttribute('aria-describedby')).toBe(tooltip.id);
    expect(tooltip.textContent).toBe('Find my location');
    expect(tooltip.classList).not.toContain('map-control-tooltip--immediate');
  });

  it('replaces the tooltip with the denied explanation, shown without delay', () => {
    const { control, button, tooltip } = create();

    control.setDenied(true);
    expect(button.getAttribute('aria-disabled')).toBe('true');
    expect(tooltip.textContent).toBe('Location is blocked');
    expect(tooltip.classList).toContain('map-control-tooltip--immediate');

    control.updateLabels({ ...labels, deniedTooltip: 'Posisjon er blokkert' });
    expect(tooltip.textContent).toBe('Posisjon er blokkert');

    control.setDenied(false);
    expect(tooltip.textContent).toBe('Find my location');
    expect(tooltip.classList).not.toContain('map-control-tooltip--immediate');
  });

  it('renders into the given target instead of the map overlay', () => {
    const target = document.createElement('div');
    const control = new GeolocationMapControl(labels, { onClick: () => Promise.resolve() }, target);
    new OlMap({ target: document.createElement('div'), controls: [control], interactions: [] });
    expect(control['element'].parentElement).toBe(target);
  });
});
