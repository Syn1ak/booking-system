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
