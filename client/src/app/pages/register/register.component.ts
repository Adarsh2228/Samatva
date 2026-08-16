import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="auth-page">
      <div class="auth-bg"><div class="orb orb-1"></div><div class="orb orb-2"></div><div class="orb orb-3"></div></div>
      <div class="auth-card glass">
        <div class="auth-header">
          <div class="logo"><span class="logo-icon">⚖️</span><h1>Sama<span class="gradient-text">tva</span></h1></div>
          <p class="subtitle">Create your account — it's free!</p>
        </div>
        <form (ngSubmit)="onRegister()" class="auth-form">
          <div class="form-group">
            <label>Full Name</label>
            <input class="form-input" type="text" [(ngModel)]="form.displayName" name="displayName" placeholder="Adarsh Shukla" required>
          </div>
          <div class="form-group">
            <label>Email</label>
            <input class="form-input" type="email" [(ngModel)]="form.email" name="email" placeholder="you&#64;email.com" required>
          </div>
          <div class="form-group">
            <label>Password</label>
            <input class="form-input" type="password" [(ngModel)]="form.password" name="password" placeholder="Min. 8 characters" required minlength="8">
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Phone (optional)</label>
              <input class="form-input" type="tel" [(ngModel)]="form.phoneNumber" name="phone" placeholder="+91 98765...">
            </div>
            <div class="form-group">
              <label>UPI ID (optional)</label>
              <input class="form-input" type="text" [(ngModel)]="form.upiId" name="upi" placeholder="you&#64;upi">
            </div>
          </div>
          @if (errorMsg()) { <div class="error-banner">⚠️ {{ errorMsg() }}</div> }
          <button class="btn btn-primary btn-lg w-full" type="submit" [disabled]="isLoading()">
            @if (isLoading()) { <span class="spinner"></span> Creating account... } @else { Create Account }
          </button>
        </form>
        <p class="auth-footer">Already have an account? <a routerLink="/login">Sign in</a></p>
      </div>
    </div>
  `,
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  form = { displayName: '', email: '', password: '', phoneNumber: '', upiId: '', defaultCurrency: 'INR' };
  isLoading = signal(false);
  errorMsg = signal('');

  constructor(private auth: AuthService, private router: Router) {}

  onRegister() {
    this.isLoading.set(true); this.errorMsg.set('');
    this.auth.register(this.form).subscribe({
      next: () => { this.isLoading.set(false); this.router.navigate(['/dashboard']); },
      error: (err) => { this.isLoading.set(false); this.errorMsg.set(err.error?.message || 'Registration failed.'); }
    });
  }
}
