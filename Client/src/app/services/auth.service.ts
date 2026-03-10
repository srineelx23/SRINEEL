import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { jwtDecode } from 'jwt-decode';
import { Router } from '@angular/router';

@Injectable({
    providedIn: 'root'
})
export class AuthService {
    private http = inject(HttpClient);
    private router = inject(Router);
    private backendUrl = 'https://localhost:7257/api/Auth';

    constructor() { }

    login(credentials: any): Observable<any> {
        return this.http.post(`${this.backendUrl}/login`, credentials);
    }

    registerCustomer(data: any): Observable<any> {
        // The backend returns a plain string: "Customer Registered Successfully"
        return this.http.post(`${this.backendUrl}/customer/register`, data, { responseType: 'text' });
    }

    getRoleFromToken(token: string): string | null {
        try {
            const decoded: any = jwtDecode(token);
            // .NET Identity typically places roles here, or a custom "Role" claim.
            return decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || decoded.role || decoded.Role || null;
        } catch (error) {
            console.error('Error decoding token', error);
            return null;
        }
    }

    getUserName(): string | null {
        const token = sessionStorage.getItem('token');
        if (!token) return null;
        try {
            const decoded: any = jwtDecode(token);
            return decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name']
                || decoded.name
                || decoded.FullName
                || decoded.unique_name
                || 'User';
        } catch {
            return null;
        }
    }

    getRoleFromStoredToken(): string | null {
        const token = sessionStorage.getItem('token');
        if (!token) return null;
        return this.getRoleFromToken(token);
    }

    isLoggedIn(): boolean {
        return !!sessionStorage.getItem('token');
    }

    changePassword(data: { currentPassword: string; newPassword: string }): Observable<any> {
        return this.http.put(`${this.backendUrl}/change-password`, data, {
            responseType: 'text'
        });
    }

    getSecurityQuestion(email: string): Observable<{ question: string }> {
        return this.http.get<{ question: string }>(`${this.backendUrl}/forgot-password/security-question/${encodeURIComponent(email)}`);
    }

    resetPassword(data: any): Observable<any> {
        return this.http.post(`${this.backendUrl}/forgot-password/reset`, data, { responseType: 'text' });
    }

    setSecurityQuestion(data: any): Observable<any> {
        return this.http.post(`${this.backendUrl}/set-security-question`, data, {
            responseType: 'text'
        });
    }

    logout(): void {
        sessionStorage.removeItem('token');
        this.router.navigate(['/login']);
    }
}
