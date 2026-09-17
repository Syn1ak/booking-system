export type TSelectOption<TValue> = { value: TValue; label: string };

/** Turns an `{ [enumValue]: label }` map into select options, in declaration order. */
export function enumToSelectList<TValue extends string | number>(
  labels: Record<TValue, string>,
): TSelectOption<TValue>[] {
  return (Object.entries(labels) as [string, string][]).map(([value, label]) => ({
    value: (Number.isNaN(Number(value)) ? value : Number(value)) as TValue,
    label,
  }));
}
