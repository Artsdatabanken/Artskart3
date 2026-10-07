export class AbbreviateNumberHelper {
  static format(num: number, lang?: string): string {
    const isNorwegian = lang === 'no';
    const decimalSeparator = isNorwegian ? ',' : '.';
    const unitSeparator = isNorwegian ? ' ' : '';
    if (num >= 1000000) {
      const value = (num / 1000000).toFixed(1).replace(/\.0$/, '');
      return `${value.replace('.', decimalSeparator)}${unitSeparator}M`;
    }
    if (num >= 1000) {
      const value = (num / 1000).toFixed(1).replace(/\.0$/, '');
      return `${value.replace('.', decimalSeparator)}${unitSeparator}k`;
    }
    return String(num);
  }
}
