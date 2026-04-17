import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LanguageDropdown } from './language-dropdown';

describe('LanguageDropdown', () => {
  let component: LanguageDropdown;
  let fixture: ComponentFixture<LanguageDropdown>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [LanguageDropdown]
    })
    .compileComponents();

    fixture = TestBed.createComponent(LanguageDropdown);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
