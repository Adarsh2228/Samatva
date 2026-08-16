import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

type Step = 'email' | 'otp' | 'password';

@Component({
  selector: 'app-forgot-password',
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
            <span class="logo-icon">💸</span>
            <h1>SplitWise <span class="gradient-text">Pro</span></h1>
          </div>
          <p class="subtitle">Reset your password securely via email OTP</p>
        </div>

        <!-- Step Indicator -->
        <div class="step-bar">
          <div class="step-item" [class.active]="step() === 'email'" [class.done]="step() === 'otp' || step() === 'password'">
            <div class="step-dot">{{ step() === 'email' ? '1' : '✓' }}</div>
            <span>Email</span>
          </div>
          <div class="step-line" [class.done]="step() === 'otp' || step() === 'password'"></div>
          <div class="step-item" [class.active]="step() === 'otp'" [class.done]="step() === 'password'">
            <div class="step-dot">{{ step() === 'password' ? '✓' : '2' }}</div>
            <span>OTP</span>
          </div>
          <div class="step-line" [class.done]="step() === 'password'"></div>
          <div class="step-item" [class.active]="step() === 'password'">
            <div class="step-dot">3</div>
            <span>Password</span>
          </div>
        </div>

        <!-- Step 1: Enter Email -->
        @if (step() === 'email') {
          <div class="step-content">
            <h2 class="step-title">Enter Your Email</h2>
            <p class="step-desc">We'll send a 5-digit OTP to your registered email address.</p>
            <div class="form-group">
              <label>Email Address</label>
              <input class="form-input" type="email" [(ngModel)]="email"
                placeholder="you@email.com" autocomplete="email">
            </div>
            @if (error()) { <div class="error-banner">⚠️ {{ error() }}</div> }
            @if (success()) { <div class="success-banner">✅ {{ success() }}</div> }
            <button class="btn btn-primary btn-lg w-full" (click)="sendOtp()" [disabled]="loading() || !email.trim()">
              @if (loading()) { <span class="spinner"></span> Sending OTP... }
              @else { 📧 Send OTP }
            </button>
          </div>
        }

        <!-- Step 2: Enter OTP -->
        @if (step() === 'otp') {
          <div class="step-content">
            <h2 class="step-title">Enter OTP</h2>
            <p class="step-desc">Check your email <strong>{{ email }}</strong> for a 5-digit code. Valid for 10 minutes.</p>
            @if (success()) { <div class="success-banner">✅ {{ success() }}</div> }
            <div class="otp-inputs">
              @for (i of [0,1,2,3,4]; track i) {
                <input class="otp-box" type="text" maxlength="1"
                  [value]="otpDigits[i]"
                  (input)="onOtpInput($event, i)"
                  (keydown)="onOtpKeydown($event, i)"
                  [id]="'otp-' + i">
              }
            </div>
            <div class="resend-row">
              @if (resendCooldown() > 0) {
                <span class="resend-timer">Resend in {{ resendCooldown() }}s</span>
              } @else {
                <button class="btn-link" (click)="sendOtp()">Resend OTP</button>
              }
            </div>
            @if (error()) { <div class="error-banner">⚠️ {{ error() }}</div> }
            <button class="btn btn-primary btn-lg w-full" (click)="verifyOtp()" [disabled]="loading() || otp().length < 5">
              @if (loading()) { <span class="spinner"></span> Verifying... }
              @else { ✅ Verify OTP }
            </button>
            <button class="btn btn-ghost w-full" (click)="step.set('email')">← Change Email</button>
          </div>
        }

        <!-- Step 3: New Password -->
        @if (step() === 'password') {
          <div class="step-content">
            <h2 class="step-title">Set New Password</h2>
            <p class="step-desc">Choose a strong password for your account.</p>
            <div class="form-group">
              <label>New Password</label>
              <div class="pw-wrap">
                <input class="form-input" [type]="showPw() ? 'text' : 'password'"
                  [(ngModel)]="newPassword" placeholder="Min 8 characters" autocomplete="new-password">
                <button type="button" class="toggle-pw" (click)="showPw.set(!showPw())">{{ showPw() ? '🙈' : '👁️' }}</button>
              </div>
              <div class="pw-strength">
                <div class="pw-bar" [class]="pwStrengthClass()"></div>
                <span class="pw-label">{{ pwStrengthLabel() }}</span>
              </div>
            </div>
            <div class="form-group">
              <label>Confirm Password</label>
              <input class="form-input" [type]="showPw() ? 'text' : 'password'"
                [(ngModel)]="confirmPassword" placeholder="Repeat password">
            </div>
            @if (error()) { <div class="error-banner">⚠️ {{ error() }}</div> }
            <button class="btn btn-primary btn-lg w-full" (click)="resetPassword()"
              [disabled]="loading() || newPassword.length < 8 || newPassword !== confirmPassword">
              @if (loading()) { <span class="spinner"></span> Saving... }
              @else { 🔐 Reset Password & Login }
            </button>
          </div>
        }

        <p class="auth-footer"><a routerLink="/login">← Back to Login</a></p>
      </div>
    </div>
  `,
  styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent {
  private readonly API = `${environment.apiUrl}/auth`;

  step = signal<Step>('email');
  loading = signal(false);
  error = signal('');
  success = signal('');
  showPw = signal(false);

  email = '';
  otpDigits: string[] = ['', '', '', '', ''];
  newPassword = '';
  confirmPassword = '';
  resendCooldown = signal(0);
  private cooldownTimer?: ReturnType<typeof setInterval>;

  constructor(private http: HttpClient, private router: Router) {}

  otp() { return this.otpDigits.join(''); }

  sendOtp() {
    this.loading.set(true); this.error.set(''); this.success.set('');
    this.http.post(`${this.API}/forgot-password`, { email: this.email }).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set('OTP sent! Check your email inbox (and spam folder).');
        this.step.set('otp');
        this.startCooldown();
      },
      error: (e) => { this.loading.set(false); this.error.set(e?.error?.message || 'Failed to send OTP.'); }
    });
  }

  onOtpInput(event: Event, index: number) {
    const val = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(-1);
    this.otpDigits[index] = val;
    if (val && index < 4) {
      document.getElementById(`otp-${index + 1}`)?.focus();
    }
    this.error.set('');
  }

  onOtpKeydown(event: KeyboardEvent, index: number) {
    if (event.key === 'Backspace' && !this.otpDigits[index] && index > 0) {
      this.otpDigits[index - 1] = '';
      document.getElementById(`otp-${index - 1}`)?.focus();
    }
  }

  verifyOtp() {
    this.loading.set(true); this.error.set('');
    this.http.post(`${this.API}/verify-otp`, { email: this.email, otp: this.otp() }).subscribe({
      next: () => { this.loading.set(false); this.step.set('password'); },
      error: (e) => { this.loading.set(false); this.error.set(e?.error?.message || 'Invalid OTP.'); }
    });
  }

  resetPassword() {
    if (this.newPassword !== this.confirmPassword) {
      this.error.set('Passwords do not match.'); return;
    }
    this.loading.set(true); this.error.set('');
    this.http.post<any>(`${this.API}/reset-password`, {
      email: this.email, otp: this.otp(), newPassword: this.newPassword
    }).subscribe({
      next: (res) => {
        this.loading.set(false);
        // Store auth tokens and redirect to dashboard
        localStorage.setItem('swp_access_token', res.accessToken);
        localStorage.setItem('swp_refresh_token', res.refreshToken);
        localStorage.setItem('swp_user', JSON.stringify(res.user));
        this.router.navigate(['/dashboard']);
      },
      error: (e) => { this.loading.set(false); this.error.set(e?.error?.message || 'Failed to reset password.'); }
    });
  }

  pwStrengthClass(): string {
    const p = this.newPassword;
    if (p.length < 6) return 'weak';
    if (p.length < 8 || !/[0-9]/.test(p)) return 'fair';
    if (/[A-Z]/.test(p) && /[^a-zA-Z0-9]/.test(p)) return 'strong';
    return 'good';
  }
  pwStrengthLabel(): string {
    const c = this.pwStrengthClass();
    return { weak: '🔴 Weak', fair: '🟡 Fair', good: '🟢 Good', strong: '💪 Strong' }[c] ?? '';
  }

  private startCooldown() {
    this.resendCooldown.set(60);
    clearInterval(this.cooldownTimer);
    this.cooldownTimer = setInterval(() => {
      const c = this.resendCooldown();
      if (c <= 1) { clearInterval(this.cooldownTimer); this.resendCooldown.set(0); }
      else this.resendCooldown.set(c - 1);
    }, 1000);
  }
}
