import { Injectable, signal } from '@angular/core';
import { TToast, TToastTone } from '../../models/notifications/toast.types';

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly $toasts = signal<TToast[]>([]);
  private nextId = 0;

  readonly $items = this.$toasts.asReadonly();

  success(title: string, message?: string): void {
    this.show('success', title, message);
  }

  info(title: string, message?: string): void {
    this.show('info', title, message);
  }

  warning(title: string, message?: string): void {
    this.show('warning', title, message);
  }

  danger(title: string, message?: string): void {
    this.show('danger', title, message);
  }

  dismiss(id: number): void {
    this.$toasts.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }

  private show(tone: TToastTone, title: string, message?: string): void {
    const toast: TToast = { id: this.nextId++, tone, title, message };
    this.$toasts.update((toasts) => [...toasts, toast]);
  }
}
