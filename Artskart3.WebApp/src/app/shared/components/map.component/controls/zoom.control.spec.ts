import OlMap from 'ol/Map';
import { ArtskartZoomControl } from './zoom.control';

describe('ArtskartZoomControl', () => {
  const labels = { zoomInTipLabel: 'Zoom in', zoomOutTipLabel: 'Zoom out', zoomInTooltip: 'Move closer', zoomOutTooltip: 'Move further out' };

  it('describes both buttons with tooltips and renders into the given target', () => {
    const target = document.createElement('div');
    const control = new ArtskartZoomControl(labels, target);
    new OlMap({ target: document.createElement('div'), controls: [control], interactions: [] });

    expect(control['element'].parentElement).toBe(target);
    const descriptions = Array.from(control['element'].querySelectorAll('button')).map(
      (button) => control['element'].querySelector(`#${button.getAttribute('aria-describedby')}`)?.textContent,
    );
    expect(descriptions).toEqual(['Move closer', 'Move further out']);
  });
});
