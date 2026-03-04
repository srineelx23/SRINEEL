import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
    const router = inject(Router);
    const token = typeof window !== 'undefined' ? sessionStorage.getItem('token') : null;

    let authReq = req;
    if (token) {
        authReq = req.clone({
            headers: req.headers.set('Authorization', `Bearer ${token}`)
        });
    }

    return next(authReq).pipe(
        catchError((error: HttpErrorResponse) => {
            // Only redirect on actual errors, not for simple auth checks if needed
            // But for this requirement, we'll redirect all HTTP errors
            router.navigate(['/error'], {
                state: {
                    status: error.status,
                    message: typeof error.error === 'string' ? error.error : (error.error?.message || error.message || 'An unexpected error occurred'),
                    title: 'System Exception'
                }
            });
            return throwError(() => error);
        })
    );
};
