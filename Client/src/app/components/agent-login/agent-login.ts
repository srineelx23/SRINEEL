import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-agent-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './agent-login.html',
  styleUrl: './agent-login.css',
})
export class AgentLogin {
  email = '';
  password = '';
  errorMessage = signal('');

  private authService = inject(AuthService);
  private router = inject(Router);

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
            this.router.navigate(['/customer-dashboard']);
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

        setTimeout(() => {
          this.errorMessage.set('');
        }, 5000);
      }
    });
  }
}
