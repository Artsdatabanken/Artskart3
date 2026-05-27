import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  let component: PaginationComponent;
  let fixture: ComponentFixture<PaginationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PaginationComponent, TranslateModule.forRoot()],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(PaginationComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.componentRef.setInput('currentPage', 1);
    fixture.componentRef.setInput('totalVisiblePages', 5);
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  describe('visiblePages', () => {
    it('should show pages 1-5 when on page 1 with 10 total', () => {
      fixture.componentRef.setInput('currentPage', 1);
      fixture.componentRef.setInput('totalVisiblePages', 10);
      fixture.detectChanges();
      expect(component.visiblePages()).toEqual([1, 2, 3, 4, 5]);
    });

    it('should center the window around current page', () => {
      fixture.componentRef.setInput('currentPage', 5);
      fixture.componentRef.setInput('totalVisiblePages', 10);
      fixture.detectChanges();
      expect(component.visiblePages()).toEqual([3, 4, 5, 6, 7]);
    });

    it('should cap at totalVisiblePages', () => {
      fixture.componentRef.setInput('currentPage', 9);
      fixture.componentRef.setInput('totalVisiblePages', 10);
      fixture.detectChanges();
      expect(component.visiblePages()).toEqual([6, 7, 8, 9, 10]);
    });

    it('should show fewer pages if total is less than 5', () => {
      fixture.componentRef.setInput('currentPage', 1);
      fixture.componentRef.setInput('totalVisiblePages', 3);
      fixture.detectChanges();
      expect(component.visiblePages()).toEqual([1, 2, 3]);
    });
  });

  describe('showLeadingEllipsis', () => {
    it('should not show leading ellipsis when first page is 1', () => {
      fixture.componentRef.setInput('currentPage', 1);
      fixture.componentRef.setInput('totalVisiblePages', 10);
      fixture.detectChanges();
      expect(component.showLeadingEllipsis()).toBe(false);
    });

    it('should show leading ellipsis when first visible page > 1', () => {
      fixture.componentRef.setInput('currentPage', 7);
      fixture.componentRef.setInput('totalVisiblePages', 10);
      fixture.detectChanges();
      expect(component.showLeadingEllipsis()).toBe(true);
    });
  });

  describe('navigation', () => {
    it('should not have previous page when on page 1', () => {
      fixture.componentRef.setInput('currentPage', 1);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.detectChanges();
      expect(component.hasPreviousPage()).toBe(false);
    });

    it('should have previous page when on page > 1', () => {
      fixture.componentRef.setInput('currentPage', 3);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.detectChanges();
      expect(component.hasPreviousPage()).toBe(true);
    });

    it('should have next page when current < total', () => {
      fixture.componentRef.setInput('currentPage', 3);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.detectChanges();
      expect(component.hasNextPage()).toBe(true);
    });

    it('should have next page when hasMorePages is true', () => {
      fixture.componentRef.setInput('currentPage', 5);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.componentRef.setInput('hasMorePages', true);
      fixture.detectChanges();
      expect(component.hasNextPage()).toBe(true);
    });

    it('should not have next page at end without more pages', () => {
      fixture.componentRef.setInput('currentPage', 5);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.componentRef.setInput('hasMorePages', false);
      fixture.detectChanges();
      expect(component.hasNextPage()).toBe(false);
    });
  });

  describe('outputs', () => {
    it('should emit pageChange on goToPage', () => {
      fixture.componentRef.setInput('currentPage', 1);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.detectChanges();

      const spy = vi.fn();
      component.pageChange.subscribe(spy);
      component.goToPage(3);
      expect(spy).toHaveBeenCalledWith(3);
    });

    it('should emit pageChange with next page on nextPage', () => {
      fixture.componentRef.setInput('currentPage', 2);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.detectChanges();

      const spy = vi.fn();
      component.pageChange.subscribe(spy);
      component.nextPage();
      expect(spy).toHaveBeenCalledWith(3);
    });

    it('should emit pageChange with previous page on previousPage', () => {
      fixture.componentRef.setInput('currentPage', 3);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.detectChanges();

      const spy = vi.fn();
      component.pageChange.subscribe(spy);
      component.previousPage();
      expect(spy).toHaveBeenCalledWith(2);
    });

    it('should not go below page 1 on previousPage', () => {
      fixture.componentRef.setInput('currentPage', 1);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.detectChanges();

      const spy = vi.fn();
      component.pageChange.subscribe(spy);
      component.previousPage();
      expect(spy).toHaveBeenCalledWith(1);
    });

    it('should emit pageSizeChange on page size selection', () => {
      fixture.componentRef.setInput('currentPage', 1);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.componentRef.setInput('pageSizeOptions', [10, 25, 50]);
      fixture.componentRef.setInput('pageSize', 10);
      fixture.detectChanges();

      const spy = vi.fn();
      component.pageSizeChange.subscribe(spy);
      const event = { target: { value: '25' } } as unknown as Event;
      component.onPageSizeChange(event);
      expect(spy).toHaveBeenCalledWith(25);
    });
  });

  describe('rangeLabel', () => {
    it('should compute correct range label', () => {
      fixture.componentRef.setInput('currentPage', 2);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.componentRef.setInput('pageSize', 10);
      fixture.detectChanges();
      expect(component.rangeLabel()).toBe('11-20');
    });

    it('should return empty string when pageSize is 0', () => {
      fixture.componentRef.setInput('currentPage', 1);
      fixture.componentRef.setInput('totalVisiblePages', 5);
      fixture.componentRef.setInput('pageSize', 0);
      fixture.detectChanges();
      expect(component.rangeLabel()).toBe('');
    });
  });
});
