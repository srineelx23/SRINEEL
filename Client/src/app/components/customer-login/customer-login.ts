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
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');

          if (returnUrl) {
            this.router.navigateByUrl(returnUrl);
          } else if (role === 'Admin') {
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

  // ==== Forgot Password Flow ====
  isForgotPasswordMode = signal(false);
  forgotPasswordStep = signal(1); // 1 = Enter Email, 2 = Answer & New Password
  forgotEmail = '';
  fetchedSecurityQuestion = '';
  securityAnswer = '';
  newPassword = '';
  confirmNewPassword = '';

  successMessage = signal('');

  openForgotPassword() {
    this.errorMessage.set('');
    this.successMessage.set('');
    this.isForgotPasswordMode.set(true);
    this.forgotPasswordStep.set(1);
    this.forgotEmail = this.email; // Pre-fill if they typed it
    this.fetchedSecurityQuestion = '';
    this.securityAnswer = '';
    this.newPassword = '';
    this.confirmNewPassword = '';
  }

  cancelForgotPassword() {
    this.isForgotPasswordMode.set(false);
    this.errorMessage.set('');
    this.successMessage.set('');
  }

  fetchSecurityQuestion() {
    this.errorMessage.set('');
    if (!this.forgotEmail) {
      this.errorMessage.set('Please enter your email.');
      this.autoHideToast();
      return;
    }

    this.authService.getSecurityQuestion(this.forgotEmail.trim()).subscribe({
      next: (res: any) => {
        if (res && res.question) {
          this.fetchedSecurityQuestion = res.question;
          this.forgotPasswordStep.set(2);
        }
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || err.error || 'Failed to fetch security question.');
        this.autoHideToast();
      }
    });
  }

  submitResetPassword() {
    this.errorMessage.set('');
    if (!this.securityAnswer || !this.newPassword || !this.confirmNewPassword) {
      this.errorMessage.set('Please fill out all fields.');
      this.autoHideToast();
      return;
    }
    if (this.newPassword !== this.confirmNewPassword) {
      this.errorMessage.set('New passwords do not match.');
      this.autoHideToast();
      return;
    }

    const payload = {
      Email: this.forgotEmail.trim(),
      SecurityAnswer: this.securityAnswer.trim().toLowerCase(),
      NewPassword: this.newPassword
    };

    this.authService.resetPassword(payload).subscribe({
      next: (res) => {
        this.successMessage.set('Password reset successfully! You can now log in.');
        this.isForgotPasswordMode.set(false);
        setTimeout(() => this.successMessage.set(''), 5000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || err.error || 'Failed to reset password.');
        this.autoHideToast();
      }
    });
  }

  private autoHideToast() {
    setTimeout(() => {
      this.errorMessage.set('');
    }, 5000);
  }
}
