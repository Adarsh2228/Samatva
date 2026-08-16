import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="auth-page">
      <div class="auth-bg">
        <div class="orb orb-1"></div>
        <div class="orb orb-2"></div>
        <div class="orb orb-3"></div>
      </div>
      <div class="auth-card glass">
        <div class="auth-header">
          <div class="logo">
            <span class="logo-icon">⚖️</span>
            <h1>Sama<span class="gradient-text">tva</span></h1>
          </div>
          <p class="subtitle">Welcome back! Sign in to manage your expenses.</p>
        </div>
        <form (ngSubmit)="onLogin()" class="auth-form">
          <div class="form-group">
            <label>Email</label>
            <input class="form-input" type="email" [(ngModel)]="email" name="email" placeholder="you&#64;email.com" required autocomplete="email">
          </div>
          <div class="form-group">
            <div style="display:flex; align-items:center; justify-content:space-between;">
              <label>Password</label>
              <a routerLink="/forgot-password" style="font-size:var(--fs-xs); color:var(--brand-primary); text-decoration:none; font-weight:600;">Forgot password?</a>
            </div>
            <input class="form-input" [type]="showPassword() ? 'text' : 'password'" [(ngModel)]="password" name="password" placeholder="••••••••" required>
            <button type="button" class="toggle-pw" (click)="showPassword.set(!showPassword())">{{ showPassword() ? '🙈' : '👁️' }}</button>
          </div>
          @if (errorMsg()) {
            <div class="error-banner">⚠️ {{ errorMsg() }}</div>
          }
          <button class="btn btn-primary btn-lg w-full" type="submit" [disabled]="isLoading()">
            @if (isLoading()) { <span class="spinner"></span> Signing in... }
            @else { Sign In }
          </button>
        </form>
        <p class="auth-footer">Don't have an account? <a routerLink="/register">Create one</a></p>
      </div>
    </div>
  `,
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  email = ''; password = '';
  isLoading = signal(false);
  errorMsg = signal('');
  showPassword = signal(false);

  constructor(private auth: AuthService, private router: Router) {
    if (this.auth.isAuthenticated()) this.router.navigate(['/dashboard']);
  }

  onLogin() {
    this.isLoading.set(true); this.errorMsg.set('');
    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: () => { this.isLoading.set(false); this.router.navigate(['/dashboard']); },
      error: (err) => { this.isLoading.set(false); this.errorMsg.set(err.error?.message || 'Login failed. Please try again.'); }
    });
  }
}
