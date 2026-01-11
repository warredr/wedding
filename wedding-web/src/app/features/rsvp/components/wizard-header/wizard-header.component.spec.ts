import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WizardHeaderComponent } from './wizard-header.component';

describe('WizardHeaderComponent', () => {
  let fixture: ComponentFixture<WizardHeaderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WizardHeaderComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(WizardHeaderComponent);
  });

  it('shows checkmarks for completed people and emits selection', () => {
    const component = fixture.componentInstance;

    component.members = [
      { personId: 'p1', fullName: 'Alice', attending: null, hasAllergies: null, allergiesText: null },
      { personId: 'p2', fullName: 'Bob', attending: null, hasAllergies: null, allergiesText: null },
    ];
    component.order = ['p1', 'p2'];
    component.currentPersonId = 'p1';
    component.completedByPersonId = { p1: true, p2: false };

    spyOn(component.selectPerson, 'emit');

    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Alice');
    expect(text).toContain('Bob');
    expect(text).toContain('✓');

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
    buttons[1]!.click();
    expect(component.selectPerson.emit).toHaveBeenCalledWith('p2');
  });
});
