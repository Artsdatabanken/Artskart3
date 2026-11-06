import { normalizeCadastralUnitId } from './cadastral-unit.service';

describe('normalizeCadastralUnitId', () => {
  it.each([
    ['1101-1/2', '1101-1/2'],
    [' 1101 - 1 / 2 ', '1101-1/2'],
    ['0301-007/02', '0301-7/2'],
    ['3413-325/2/1', '3413-325/2/1'],
    ['3413-6/501/0/2', '3413-6/501/0/2'],
    ['1101-1/2/0', '1101-1/2'],
    ['1101-1/2/0/0', '1101-1/2'],
    ['1101-1/2/3/0', '1101-1/2/3'],
  ])('gjør %j om til %j', (input, expected) => {
    expect(normalizeCadastralUnitId(input)).toBe(expected);
  });

  it.each(['', 'abc', '1101-1', '1101-1/', '101-1/2', '1101/1/2', '1101-1\\2', '1101-1/2/3/4/5', '1101-123456/1'])(
    'avviser %j',
    (input) => {
      expect(normalizeCadastralUnitId(input)).toBeNull();
    },
  );
});
