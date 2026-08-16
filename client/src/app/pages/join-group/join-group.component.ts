import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { GroupService } from '../../services/group.service';

@Component({
  selector: 'app-join-group',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="join-page">
      <div class="auth-bg"><div class="orb orb-1"></div><div class="orb orb-2"></div></div>
      <div class="join-card glass">
        @if (isLoading()) {
          <div class="join-loading">
            <span class="spinner-lg"></span>
            <h2>Joining group...</h2>
            <p>Please wait while we process your invitation.</p>
          </div>
        }
        @if (!isLoading() && success()) {
          <div class="join-success">
            <span class="join-icon">🎉</span>
            <h2>You're In!</h2>
            <p>{{ message() }}</p>
            <a routerLink="/group/{{ joinedGroupId() }}" class="btn btn-primary btn-lg">Open Group →</a>
          </div>
        }
        @if (!isLoading() && !success()) {
          <div class="join-error">
            <span class="join-icon">😕</span>
            <h2>Oops!</h2>
            <p>{{ message() }}</p>
            <div class="join-actions">
              <a routerLink="/login" class="btn btn-primary">Sign In</a>
              <a routerLink="/register" class="btn btn-secondary">Register</a>
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .join-page { display: flex; align-items: center; justify-content: center; min-height: 100dvh; padding: var(--sp-md); position: relative; overflow: hidden; }
    .auth-bg { position: absolute; inset: 0; z-index: 0; background: var(--bg-primary); }
    .orb { position: absolute; border-radius: 50%; filter: blur(80px); opacity: .5; animation: float 8s ease-in-out infinite; }
    .orb-1 { width: 300px; height: 300px; background: var(--brand-primary); top: -60px; right: -40px; }
    .orb-2 { width: 250px; height: 250px; background: var(--brand-accent); bottom: -40px; left: -20px; animation-delay: 2s; }
    @keyframes float { 0%,100% { transform: translateY(0); } 50% { transform: translateY(-20px); } }
    .join-card { position: relative; z-index: 1; width: 100%; max-width: 420px; padding: var(--sp-2xl); border-radius: var(--radius-xl); text-align: center; box-shadow: var(--shadow-xl); }
    .join-icon { font-size: 4rem; display: block; margin-bottom: var(--sp-md); }
    .join-card h2 { font-size: var(--fs-xl); font-weight: 800; margin-bottom: var(--sp-sm); }
    .join-card p { color: var(--text-secondary); margin-bottom: var(--sp-lg); }
    .join-loading { display: flex; flex-direction: column; align-items: center; gap: var(--sp-md); }
    .spinner-lg { width: 40px; height: 40px; border: 3px solid rgba(108,92,231,.2); border-top-color: var(--brand-primary); border-radius: 50%; animation: spin .8s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .join-actions { display: flex; gap: var(--sp-sm); justify-content: center; }
  `]
})
export class JoinGroupComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private auth = inject(AuthService);
  private groupService = inject(GroupService);

  isLoading = signal(true);
  success = signal(false);
  message = signal('');
  joinedGroupId = signal('');

  ngOnInit() {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.isLoading.set(false);
      this.message.set('No invitation token found.');
      return;
    }

    if (!this.auth.isAuthenticated()) {
      this.isLoading.set(false);
      this.message.set('Please sign in or register first, then click the invite link again.');
      return;
    }

    this.groupService.joinViaToken(token).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        this.success.set(true);
        this.message.set(res.message);
        this.joinedGroupId.set(res.groupId);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.success.set(false);
        this.message.set(err.error?.message || 'This invite link is invalid or expired.');
      }
    });
  }
}
