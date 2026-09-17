export type TToastTone = 'success' | 'info' | 'warning' | 'danger';

export type TToast = {
  id: number;
  tone: TToastTone;
  title: string;
  message?: string;
};
