import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { DropdownListComponent, type DropdownListItem } from './dropdown-list.component';

describe('DropdownListComponent', () => {
  let fixture: ComponentFixture<DropdownListComponent<string>>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DropdownListComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DropdownListComponent<string>);
  });

  it('emits selectItem for enabled items', () => {
    const items: Array<DropdownListItem<string>> = [
      { id: 'a', primary: 'A', data: 'A' },
      { id: 'b', primary: 'B', data: 'B' },
    ];

    fixture.componentInstance.items = items;
    spyOn(fixture.componentInstance.selectItem, 'emit');

    fixture.detectChanges();

    const buttons = fixture.debugElement.queryAll(By.css('button'));
    expect(buttons.length).toBe(2);

    buttons[0]!.nativeElement.dispatchEvent(new PointerEvent('pointerup', { bubbles: true }));
    expect(fixture.componentInstance.selectItem.emit).toHaveBeenCalledWith(items[0]!);
  });

  it('does not emit for disabled items', () => {
    const items: Array<DropdownListItem<string>> = [
      { id: 'a', primary: 'A', disabled: true, data: 'A' },
    ];

    fixture.componentInstance.items = items;
    spyOn(fixture.componentInstance.selectItem, 'emit');

    fixture.detectChanges();

    const button = fixture.debugElement.query(By.css('button'));
    expect(button.nativeElement.disabled).toBeTrue();

    button.nativeElement.dispatchEvent(new PointerEvent('pointerup', { bubbles: true }));
    expect(fixture.componentInstance.selectItem.emit).not.toHaveBeenCalled();
  });
});
