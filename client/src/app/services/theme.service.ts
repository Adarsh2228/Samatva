import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly THEME_KEY = 'swp_theme';
  private _isDark = signal(this.loadTheme());
  readonly isDark = this._isDark.asReadonly();

  constructor() {
    this.applyTheme();
  }

  toggle(): void {
    this._isDark.set(!this._isDark());
    localStorage.setItem(this.THEME_KEY, this._isDark() ? 'dark' : 'light');
    this.applyTheme();
  }

  private loadTheme(): boolean {
    const stored = localStorage.getItem(this.THEME_KEY);
    if (stored) return stored === 'dark';
    return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
  }

  private applyTheme(): void {
    document.documentElement.setAttribute('data-theme', this._isDark() ? 'dark' : 'light');
  }
}
