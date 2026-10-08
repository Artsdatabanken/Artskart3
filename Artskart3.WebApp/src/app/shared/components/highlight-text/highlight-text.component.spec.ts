import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HighlightTextComponent } from './highlight-text.component';

describe('HighlightTextComponent', () => {
  let fixture: ComponentFixture<HighlightTextComponent>;

  const marked = (text: string, italic = false) => ({ text, isMatch: true, italic });
  const plain = (text: string, italic = false) => ({ text, isMatch: false, italic });

  function render(text: string | null | undefined, term: string, markup = false) {
    fixture.componentRef.setInput('text', text);
    fixture.componentRef.setInput('term', term);
    fixture.componentRef.setInput('markup', markup);
    fixture.detectChanges();
    return fixture.componentInstance.segments();
  }

  beforeEach(() => {
    fixture = TestBed.createComponent(HighlightTextComponent);
  });

  it('should mark the matching substring', () => {
    expect(render('Kjøttmeis', 'kjøtt')).toEqual([marked('Kjøtt'), plain('meis')]);
  });

  it('should mark a match inside a word', () => {
    expect(render('Sargassoulke', 'ulke')).toEqual([plain('Sargasso'), marked('ulke')]);
  });

  it('should highlight multiple words independently', () => {
    expect(render('Kjøttmeis', 'kj m')).toEqual([marked('Kj'), plain('øtt'), marked('m'), plain('eis')]);
  });

  it('should mark the whole match when one search word contains another', () => {
    expect(render('Kjøttmeis', 'kj kjøtt')).toEqual([marked('Kjøtt'), plain('meis')]);
  });

  it('should mark overlapping matches of different words', () => {
    expect(render('Kjøttmeis', 'øtt kjø')).toEqual([marked('Kjøtt'), plain('meis')]);
  });

  it('should return the whole text unmarked when no search term', () => {
    expect(render('Kjøttmeis', '')).toEqual([plain('Kjøttmeis')]);
  });

  it('should return no segments for null/undefined input', () => {
    expect(render(null, 'test')).toEqual([]);
    expect(render(undefined, 'test')).toEqual([]);
  });

  it('should escape regex special characters in search term', () => {
    expect(render('this is test(1) here', 'test(1)')).toEqual([plain('this is '), marked('test(1)'), plain(' here')]);
  });

  it('should keep the italic parts of a formatted name', () => {
    expect(render('<i>Betula pubescens </i>subsp.<i> pubescens</i>', 'pub sub', true)).toEqual([
      plain('Betula ', true),
      marked('pub', true),
      plain('escens ', true),
      marked('sub'),
      plain('sp.'),
      plain(' ', true),
      marked('pub', true),
      plain('escens', true),
    ]);
  });

  it('should highlight a match that crosses a tag boundary', () => {
    expect(render('<i>Salix </i>×<i>multinervis</i>', '×multi', true)).toEqual([
      plain('Salix ', true),
      marked('×'),
      marked('multi', true),
      plain('nervis', true),
    ]);
  });

  it('should render only the text of tags other than i', () => {
    expect(render('<img src="x"><b>Parus</b> <i>major</i>', '', true)).toEqual([plain('Parus'), plain(' '), plain('major', true)]);
  });

  it('should treat markup as plain text unless markup is set', () => {
    expect(render('<i>Parus</i>', '')).toEqual([plain('<i>Parus</i>')]);
  });

  it('should render matches as mark elements without adding whitespace', () => {
    render('Kjøttmeis', 'kjøtt');
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('mark')?.textContent).toBe('Kjøtt');
    expect(element.textContent).toBe('Kjøttmeis');
  });

  it('should render italic matches as marks inside i elements', () => {
    render('<i>Betula pubescens </i>subsp.<i> pubescens</i>', 'pubescens', true);
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toBe('Betula pubescens subsp. pubescens');
    expect([...element.querySelectorAll('i')].map((i) => i.textContent)).toEqual(['Betula ', 'pubescens', ' ', ' ', 'pubescens']);
    expect(element.querySelectorAll('i mark').length).toBe(2);
  });
});
