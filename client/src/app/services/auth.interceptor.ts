import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private isRefreshing = false;

  constructor(private auth: AuthService) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const token = this.auth.getToken();
    let authReq = req;
    if (token) {
      authReq = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
    }

    return next.handle(authReq).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401 && !req.url.includes('/auth/')) {
          if (!this.isRefreshing) {
            this.isRefreshing = true;
            return this.auth.refreshToken().pipe(
              switchMap(res => {
                this.isRefreshing = false;
                const retryReq = req.clone({ setHeaders: { Authorization: `Bearer ${res.accessToken}` } });
                return next.handle(retryReq);
              }),
              catchError(err => { this.isRefreshing = false; return throwError(() => err); })
            );
          }
        }
        return throwError(() => error);
      })
    );
  }
}
