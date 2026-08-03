import { Injectable, signal } from '@angular/core';

export interface ConfirmOptions {
  title?: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
}

interface ConfirmState extends Required<ConfirmOptions> {
  isOpen: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ConfirmService {

  private readonly defaults: Omit<ConfirmState, 'message'> = {
    isOpen: false,
    title: 'Confirm',
    confirmText: 'Confirm',
    cancelText: 'Cancel'
  };

  readonly state = signal<ConfirmState>({ ...this.defaults, message: '' });

  private resolver: ((result: boolean) => void) | null = null;

  confirm(options: ConfirmOptions): Promise<boolean> {
    this.state.set({
      ...this.defaults,
      ...options,
      isOpen: true
    });

    return new Promise<boolean>(resolve => {
      this.resolver = resolve;
    });
  }

  respond(result: boolean): void {
    this.state.update(state => ({ ...state, isOpen: false }));
    this.resolver?.(result);
    this.resolver = null;
  }
}
