import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { CaptchaService } from '../../services/captcha.service';

@Component({
  selector: 'app-admin-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-login.html',
  styleUrl: './admin-login.css',
})
export class AdminLogin implements OnInit {
  email = '';
  password = '';
  errorMessage = signal('');
  captchaCode = signal('');
  userCaptcha = '';

  private authService = inject(AuthService);
  private captchaService = inject(CaptchaService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  ngOnInit() {
    this.refreshCaptcha();
  }

  refreshCaptcha() {
    this.captchaCode.set(this.captchaService.generateCaptcha());
    this.userCaptcha = '';
  }

  login() {
    this.errorMessage.set('');
    if (!this.email || !this.password) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: 'Please enter both email and password.', title: 'Validation Error' }
      });
      return;
    }

    if (!this.captchaService.validateCaptcha(this.userCaptcha)) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: 'Invalid Authentication CAPTCHA. Access Denied.', title: 'Security Violation' }
      });
      this.refreshCaptcha();
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
            this.router.navigate(['/customer-dashboard']);
          }
        }
      },
      error: (err: any) => {
        const errorStatus = err.status || 500;
        const errorMessage = typeof err.error === 'string' ? err.error : (err.error?.message || 'Login failed. Please check your credentials.');

        this.router.navigate(['/error'], {
          state: {
            status: errorStatus,
            message: errorMessage,
            title: 'Administrative Access Denied'
          }
        });
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
      error: (err: any) => {
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
      error: (err: any) => {
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
