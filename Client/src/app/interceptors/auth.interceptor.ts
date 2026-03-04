import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { extractErrorMessage } from '../utils/error-handler';

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
            const displayMessage = extractErrorMessage(error);

            router.navigate(['/error'], {
                state: {
                    status: error.status,
                    message: displayMessage,
                    title: 'System Exception'
                }
            });
            return throwError(() => error);
        })
    );
};
