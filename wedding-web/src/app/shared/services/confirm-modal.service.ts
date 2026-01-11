import { Injectable, signal } from '@angular/core';

export interface ConfirmModalState {
  open: boolean;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class ConfirmModalService {
  private resolver: ((value: boolean) => void) | null = null;

  readonly state = signal<ConfirmModalState>({
    open: false,
    message: '',
  });

  open(message: string): Promise<boolean> {
    if (this.state().open) {
      return Promise.resolve(false);
    }

    this.state.set({ open: true, message });

    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
    });
  }

  confirm(): void {
    const resolve = this.resolver;
    this.resolver = null;
    this.state.set({ open: false, message: '' });
    resolve?.(true);
  }

  cancel(): void {
    const resolve = this.resolver;
    this.resolver = null;
    this.state.set({ open: false, message: '' });
    resolve?.(false);
  }
}
