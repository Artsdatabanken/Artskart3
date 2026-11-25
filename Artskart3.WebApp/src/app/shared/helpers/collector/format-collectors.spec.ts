import { formatCollectors } from './format-collectors';

describe('formatCollectors', () => {
  it.each([
    ['Ola Nordmann', 'Ola Nordmann'],
    ['Ellen Svalheim|Liv Guri Velle', 'Ellen Svalheim, Liv Guri Velle'],
    [' Ola Nordmann | Kari Nordmann |', 'Ola Nordmann, Kari Nordmann'],
    ['', ''],
    [null, ''],
    [undefined, ''],
  ])('formats %j as %j', (collector, expected) => {
    expect(formatCollectors(collector)).toBe(expected);
  });
});
