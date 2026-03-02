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

    isLoggedIn(): boolean {
        return !!sessionStorage.getItem('token');
    }

    logout(): void {
        sessionStorage.removeItem('token');
        this.router.navigate(['/login']);
    }
}
