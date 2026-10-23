export class AbbreviateNumberHelper {
  static format(num: number, lang?: string): string {
    const isNorwegian = lang === 'no';
    const decimalSeparator = isNorwegian ? ',' : '.';
    const unitSeparator = isNorwegian ? ' ' : '';
    if (num >= 1000000) {
      const value = AbbreviateNumberHelper.round(num / 1000000);
      return `${value.replace('.', decimalSeparator)}${unitSeparator}M`;
    }
    if (num >= 1000) {
      const value = AbbreviateNumberHelper.round(num / 1000);
      return `${value.replace('.', decimalSeparator)}${unitSeparator}k`;
    }
    return String(num);
  }

  private static round(value: number): string {
    return value.toFixed(value >= 100 ? 0 : 1).replace(/\.0$/, '');
  }
}
