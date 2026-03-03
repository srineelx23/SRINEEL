import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (authService.isLoggedIn()) {
        const token = sessionStorage.getItem('token');
        const userRole = token ? authService.getRoleFromToken(token) : null;
        const expectedRoles = route.data['roles'] as Array<string>;

        if (!expectedRoles || expectedRoles.length === 0 || (userRole && expectedRoles.includes(userRole))) {
            return true;
        }

        // If logged in but role doesn't match, redirect to landing or appropriate dashboard
        console.warn('Unauthorized role access');
        router.navigate(['/']);
        return false;
    }

    // Not logged in, redirect to login page
    router.navigate(['/login']);
    return false;
};
