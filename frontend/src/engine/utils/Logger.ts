export class Logger {
  constructor(private scope: string) {}

  info(message: string, ...args: unknown[]): void {
    console.info(`[${this.scope}] ${message}`, ...args);
  }

  warn(message: string, ...args: unknown[]): void {
    console.warn(`[${this.scope}] ${message}`, ...args);
  }

  error(message: string, ...args: unknown[]): void {
    console.error(`[${this.scope}] ${message}`, ...args);
  }
}

