const identifierPattern = /^[a-z0-9][a-z0-9-]*$/;

export function required(value: string, message: string): string | null {
  return value.trim() ? null : message;
}

export function minLength(value: string, min: number, label: string): string | null {
  return value.trim().length >= min ? null : `${label} must be at least ${min} characters.`;
}

export function maxLength(value: string, max: number, label: string): string | null {
  return value.length <= max ? null : `${label} must be ${max} characters or fewer.`;
}

export function validEmail(value: string): string | null {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.trim()) ? null : 'Enter a valid email address.';
}

export function validHttpUrl(value: string, message: string): string | null {
  try {
    const url = new URL(value.trim());
    return url.protocol === 'https:' || url.protocol === 'http:' ? null : message;
  } catch {
    return message;
  }
}

export function validSlug(value: string, label: string): string | null {
  return identifierPattern.test(value.trim()) ? null : `${label} must contain lowercase letters, numbers, or hyphens.`;
}

export function firstError(...errors: Array<string | null>): string | null {
  return errors.find((error): error is string => error !== null) ?? null;
}
