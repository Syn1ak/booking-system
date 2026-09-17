import { HttpErrorResponse } from '@angular/common/http';
import { IProblemDetails, IValidationProblemDetails } from './problem-details.types';

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
