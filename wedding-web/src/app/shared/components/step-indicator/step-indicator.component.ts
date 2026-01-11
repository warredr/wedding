import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-step-indicator',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="step" aria-label="Voortgang">
      <div class="step__row">
        <div class="step__text">Stap {{ current }} van {{ total }}</div>
      </div>
      <div class="step__track" aria-hidden="true">
        <div class="step__fill" [style.width.%]="percent"></div>
      </div>
    </div>
  `,
  styles: [
    `
      .step {
        margin: 4px 0 14px;
      }

      .step__row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 8px;
      }

      .step__text {
        font-size: 12px;
        letter-spacing: 0.08em;
        text-transform: uppercase;
        opacity: 0.75;
      }

      .step__track {
        height: 6px;
        border-radius: 999px;
        background: rgba(0, 0, 0, 0.08);
        overflow: hidden;
      }

      .step__fill {
        height: 100%;
        border-radius: 999px;
        background: rgba(0, 0, 0, 0.35);
        transition: width 180ms ease-out;
      }
    `,
  ],
})
export class StepIndicatorComponent {
  @Input({ required: true }) current!: number;
  @Input({ required: true }) total!: number;

  get percent(): number {
    if (!Number.isFinite(this.current) || !Number.isFinite(this.total) || this.total <= 0) {
      return 0;
    }

    const clamped = Math.max(0, Math.min(this.current, this.total));
    return (clamped / this.total) * 100;
  }
}
