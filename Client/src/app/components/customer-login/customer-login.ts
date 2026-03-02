import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-customer-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customer-login.html',
  styleUrl: './customer-login.css',
})
export class CustomerLogin {
  email = '';
  password = '';
  errorMessage = signal('');

  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  login() {
    this.errorMessage.set('');
    if (!this.email || !this.password) {
      this.errorMessage.set('Please enter both email and password.');
      return;
    }

    const payload = {
      Email: this.email,
      Password: this.password
    };

    this.authService.login(payload).subscribe({
      next: (response) => {
        if (response && response.token) {
          sessionStorage.setItem('token', response.token);

          const role = this.authService.getRoleFromToken(response.token);
          if (role === 'Admin') {
            this.router.navigate(['/admin-dashboard']);
          } else if (role === 'Agent') {
            this.router.navigate(['/agent-dashboard']);
          } else if (role === 'ClaimsOfficer' || role === 'Claims') {
            this.router.navigate(['/claims-dashboard']);
          } else {
            const hasIntent = this.route.snapshot.queryParamMap.get('quote_intent');
            if (hasIntent) {
              this.router.navigate(['/explore-plans'], { queryParams: { open_quote: hasIntent } });
            } else {
              this.router.navigate(['/customer-dashboard']);
            }
          }
        }
      },
      error: (err) => {
        if (err.error && err.error.message) {
          this.errorMessage.set(err.error.message);
        } else if (typeof err.error === 'string') {
          this.errorMessage.set(err.error);
        } else {
          this.errorMessage.set('Login failed. Please check your credentials.');
        }

        // Auto-hide the toast after 5 seconds
        setTimeout(() => {
          this.errorMessage.set('');
        }, 5000);
      }
    });
  }
}
