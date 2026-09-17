import { HttpErrorResponse } from '@angular/common/http';

export interface IProblemDetails {
  type?: string;
  title?: string;
  status: number;
  detail?: string;
}

/** ASP.NET's shape: field name to messages, e.g. `{ ClosesAtUtc: ['…'] }`. */
export interface IValidationProblemDetails extends IProblemDetails {
  errors: Record<string, string[]>;
}

export function toProblemDetails(error: HttpErrorResponse): IProblemDetails {
  const body: unknown = error.error;

  if (body && typeof body === 'object') {
    return { ...(body as Partial<IProblemDetails>), status: error.status };
  }

  return { status: error.status, title: error.statusText };
}

export function isValidationProblem(
  problem: IProblemDetails,
): problem is IValidationProblemDetails {
  const errors = (problem as Partial<IValidationProblemDetails>).errors;
  return !!errors && typeof errors === 'object';
}
