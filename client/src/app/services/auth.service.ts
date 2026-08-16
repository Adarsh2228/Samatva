import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError, BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, LoginRequest, RegisterRequest, UserDto, RefreshTokenRequest } from '../models/interfaces';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly API = `${environment.apiUrl}/auth`;
  private readonly TOKEN_KEY = 'swp_access_token';
  private readonly REFRESH_KEY = 'swp_refresh_token';
  private readonly USER_KEY = 'swp_user';

  private currentUser = signal<UserDto | null>(null);
  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => !!this.currentUser());

  constructor(private http: HttpClient, private router: Router) {
    this.loadStoredUser();
  }

  private loadStoredUser(): void {
    try {
      const stored = localStorage.getItem(this.USER_KEY);
      if (stored) this.currentUser.set(JSON.parse(stored));
    } catch { this.clearTokens(); }
  }

  register(req: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API}/register`, req).pipe(
      tap(res => this.storeAuth(res)),
      catchError(err => throwError(() => err))
    );
  }

  login(req: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API}/login`, req).pipe(
      tap(res => this.storeAuth(res)),
      catchError(err => throwError(() => err))
    );
  }

  refreshToken(): Observable<AuthResponse> {
    const req: RefreshTokenRequest = {
      accessToken: this.getToken() || '',
      refreshToken: this.getRefreshToken() || ''
    };
    return this.http.post<AuthResponse>(`${this.API}/refresh`, req).pipe(
      tap(res => this.storeAuth(res)),
      catchError(err => { this.logout(); return throwError(() => err); })
    );
  }

  logout(): void {
    const token = this.getToken();
    if (token) {
      this.http.post(`${this.API}/logout`, {}).subscribe({ error: () => {} });
    }
    this.clearTokens();
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null { return localStorage.getItem(this.TOKEN_KEY); }
  getRefreshToken(): string | null { return localStorage.getItem(this.REFRESH_KEY); }

  getProfile(): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.API}/profile`);
  }

  private storeAuth(res: AuthResponse): void {
    localStorage.setItem(this.TOKEN_KEY, res.accessToken);
    localStorage.setItem(this.REFRESH_KEY, res.refreshToken);
    localStorage.setItem(this.USER_KEY, JSON.stringify(res.user));
    this.currentUser.set(res.user);
  }

  private clearTokens(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_KEY);
    localStorage.removeItem(this.USER_KEY);
  }
}
