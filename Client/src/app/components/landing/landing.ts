import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { jwtDecode } from 'jwt-decode';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './landing.html',
  styleUrl: './landing.css',
})
export class Landing implements OnInit {
  private router = inject(Router);
  private authService = inject(AuthService);
  userName = signal<string | null>(null);
  isLoggedIn = signal<boolean>(false);

  ngOnInit() {
    this.isLoggedIn.set(this.authService.isLoggedIn());
    const token = typeof window !== 'undefined' ? sessionStorage.getItem('token') : null;
    if (token) {
      try {
        const decodedToken: any = jwtDecode(token);
        const name = decodedToken['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name']
          || decodedToken.name
          || decodedToken.FullName
          || 'User';
        this.userName.set(name);
      } catch { }
    }
  }

  explorePlans(event: Event) {
    event.preventDefault();
    this.router.navigate(['/explore-plans']);
  }

  goToDashboard() {
    const token = typeof window !== 'undefined' ? sessionStorage.getItem('token') : null;
    if (token) {
      try {
        const decodedToken: any = jwtDecode(token);
        const role = decodedToken['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
        if (role === 'Customer') this.router.navigate(['/customer-dashboard']);
        else if (role === 'Admin') this.router.navigate(['/admin-dashboard']);
        else if (role === 'Agent') this.router.navigate(['/agent-dashboard']);
        else if (role === 'ClaimsOfficer') this.router.navigate(['/claims-dashboard']);
      } catch { }
    }
  }

  logout() {
    this.authService.logout();
    this.isLoggedIn.set(false);
    this.userName.set(null);
  }
}
