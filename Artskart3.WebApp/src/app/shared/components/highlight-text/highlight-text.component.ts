import { Component, computed, inject, input } from '@angular/core';
import { DOCUMENT } from '@angular/common';

export interface HighlightSegment {
  text: string;
  isMatch: boolean;
  italic: boolean;
}

/** Text with the words of a search term marked. */
@Component({
  selector: 'app-highlight-text',
  // Every text sits tight inside an element: a bare {{ }} would pick up the
  // surrounding line breaks as spaces inside words.
  template: `@for (segment of segments(); track $index) {
    @if (segment.italic && segment.isMatch) {
      <i
        ><mark>{{ segment.text }}</mark></i
      >
    } @else if (segment.italic) {
      <i>{{ segment.text }}</i>
    } @else if (segment.isMatch) {
      <mark>{{ segment.text }}</mark>
    } @else {
      <ng-container>{{ segment.text }}</ng-container>
    }
  }`,
  styleUrl: './highlight-text.component.css',
})
export class HighlightTextComponent {
  private readonly document = inject(DOCUMENT);

  readonly text = input<string | null | undefined>();
  readonly term = input('');
  /** The text is NorTaxa's formatted name, where `<i>` marks the italic parts. */
  readonly markup = input(false);

  readonly segments = computed<HighlightSegment[]>(() => {
    const text = this.text();
    if (!text) return [];
    const runs = this.markup() ? this.parseItalicRuns(text) : [{ text, italic: false }];
    // Match against the joined text so a match can cross a tag boundary, as in `×<i>multinervis</i>`.
    const plainText = runs.map((run) => run.text).join('');
    const isMatch = new Array<boolean>(plainText.length).fill(false);
    const words = this.term()
      .split(/\s+/)
      .filter((w) => w.length > 0);
    // One pass per word: a single alternation stops at the first word that matches, so
    // "kj kjøtt" would only mark "Kj" in "Kjøttmeis".
    for (const word of words) {
      const escaped = word.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      for (const match of plainText.matchAll(new RegExp(escaped, 'gi'))) {
        isMatch.fill(true, match.index, match.index + match[0].length);
      }
    }
    const segments: HighlightSegment[] = [];
    let offset = 0;
    for (const run of runs) {
      for (let i = 0; i < run.text.length; i++, offset++) {
        const last = segments.at(-1);
        if (i > 0 && last?.isMatch === isMatch[offset]) {
          last.text += run.text[i];
        } else {
          segments.push({ text: run.text[i], isMatch: isMatch[offset], italic: run.italic });
        }
      }
    }
    return segments;
  });

  private parseItalicRuns(markup: string): { text: string; italic: boolean }[] {
    // Template content is inert: nothing in it runs or loads, and it is only read as text.
    const template = this.document.createElement('template');
    template.innerHTML = markup;
    const walker = this.document.createTreeWalker(template.content, NodeFilter.SHOW_TEXT);
    const runs: { text: string; italic: boolean }[] = [];
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      runs.push({ text: node.textContent ?? '', italic: node.parentElement?.closest('i') != null });
    }
    return runs;
  }
}
