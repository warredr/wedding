import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { RsvpApi } from '../../../../api/rsvp-api';
import type { SearchResultDto } from '../../../../api/types';
import { SearchScreenComponent } from './search-screen.component';

describe('SearchScreenComponent', () => {
  let fixture: ComponentFixture<SearchScreenComponent>;

  beforeEach(async () => {
    const apiMock = {
      search: jasmine.createSpy('search').and.returnValue(of([])),
    } as Pick<RsvpApi, 'search'>;

    const routerSpy = {
      navigate: jasmine.createSpy('navigate'),
    } as unknown as Router;

    await TestBed.configureTestingModule({
      imports: [SearchScreenComponent],
      providers: [
        { provide: RsvpApi, useValue: apiMock },
        { provide: Router, useValue: routerSpy },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SearchScreenComponent);
  });

  it('disables confirmed groups in the results list', () => {
    const component = fixture.componentInstance;

    component.q = 'a';
    component.results = [
      {
        personId: 'p1',
        fullName: 'Alice',
        groupId: 'g1',
        groupLabelFirstNames: 'Alice',
        groupStatus: 'Open',
      },
      {
        personId: 'p2',
        fullName: 'Bob',
        groupId: 'g2',
        groupLabelFirstNames: 'Bob',
        groupStatus: 'Confirmed',
      },
    ] satisfies SearchResultDto[];

    fixture.detectChanges();

    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('.dropdown-list button')
    ) as HTMLButtonElement[];
    expect(buttons.length).toBe(2);
    expect(buttons[0]!.disabled).toBeFalse();
    expect(buttons[1]!.disabled).toBeTrue();
    expect(buttons[1]!.textContent).toContain('Bevestigd');
  });
});
