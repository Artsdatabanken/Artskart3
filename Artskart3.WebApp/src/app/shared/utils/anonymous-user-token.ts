const STORAGE_KEY = 'artskart.anonymousUserId';
let inMemoryToken: string | null = null;

export function peekAnonymousToken(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? inMemoryToken;
  } catch {
    return inMemoryToken;
  }
}

export function storeAnonymousToken(token: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, token);
    inMemoryToken = null;
  } catch {
    inMemoryToken = token;
  }
}

export function clearAnonymousToken(expectedToken?: string): void {
  if (expectedToken && peekAnonymousToken() !== expectedToken) return;

  inMemoryToken = null;
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    // localStorage kan være utilgjengelig.
  }
}
