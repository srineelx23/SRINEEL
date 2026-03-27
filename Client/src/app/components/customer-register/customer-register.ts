import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';

@Component({
  selector: 'app-customer-register',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customer-register.html',
  styleUrl: './customer-register.css',
})
export class CustomerRegister {
  protected readonly themeService = inject(ThemeService);
  firstName = '';
  lastName = '';
  email = '';
  password = '';
  confirmPassword = '';
  securityQuestion = '';
  securityAnswer = '';

  errorMessage = signal('');
  successMessage = signal('');

  // Field interaction tracking
  firstNameTouched = signal(false);
  lastNameTouched = signal(false);
  emailTouched = signal(false);
  passwordTouched = signal(false);
  confirmPasswordTouched = signal(false);
  securityQuestionTouched = signal(false);
  securityAnswerTouched = signal(false);

  private authService = inject(AuthService);
  private router = inject(Router);

  // List of security questions
  securityQuestions = [
    "Father's Name",
    "Mother's Name",
    "Wife's Name",
    "Pet's Name"
  ];

  isValidEmail(email: string): boolean {
    return /^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$/.test(email.trim());
  }

  isPasswordStrong(password: string): boolean {
    return password.length >= 6;
  }

  passwordsMatch(): boolean {
    return this.password === this.confirmPassword && this.password !== '';
  }

  register() {
    this.errorMessage.set('');
    this.successMessage.set('');
    this.emailTouched.set(true);

    if (!this.firstName || !this.lastName || !this.email || !this.password || !this.securityQuestion || !this.securityAnswer) {
      const missingFields = [];
      if (!this.firstName) missingFields.push('First Name');
      if (!this.lastName) missingFields.push('Last Name');
      if (!this.email) missingFields.push('Email');
      if (!this.password) missingFields.push('Password');
      if (!this.securityQuestion) missingFields.push('Security Question');
      if (!this.securityAnswer) missingFields.push('Security Answer');

      this.errorMessage.set(`Missing required fields: ${missingFields.join(', ')}`);

      this.firstNameTouched.set(true);
      this.lastNameTouched.set(true);
      this.emailTouched.set(true);
      this.passwordTouched.set(true);
      this.confirmPasswordTouched.set(true);
      this.securityQuestionTouched.set(true);
      this.securityAnswerTouched.set(true);
      return;
    }

    if (!this.isValidEmail(this.email)) {
      this.errorMessage.set('Please enter a valid email address.');
      return;
    }

    if (!this.isPasswordStrong(this.password)) {
      this.errorMessage.set('Password must be at least 6 characters long.');
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.errorMessage.set('Passwords do not match.');
      return;
    }

    // Combine for the backend DTO
    const fullName = `${this.firstName.trim()} ${this.lastName.trim()}`;

    const payload = {
      FullName: fullName,
      Email: this.email.trim(),
      Password: this.password,
      SecurityQuestion: this.securityQuestion,
      SecurityAnswer: this.securityAnswer.trim().toLowerCase()
    };

    this.authService.registerCustomer(payload).subscribe({
      next: (response) => {
        // Backend returns plain text on successful registration
        this.successMessage.set('Registration successful! Redirecting to login...');

        // Wait 2.5 seconds before redirecting so the user can see the success toast
        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 2500);
      },
      error: (err: any) => {
        // Extract message from the structured error response or fallback to generic
        const msg = err.error?.message || (typeof err.error === 'string' ? err.error : 'Registration failed. Please check your details.');
        this.errorMessage.set(msg);
        this.autoHideToast();
      }
    });
  }

  private autoHideToast() {
    setTimeout(() => {
      this.errorMessage.set('');
    }, 5000);
  }

  clearError() {
    this.errorMessage.set('');
  }

  clearSuccess() {
    this.successMessage.set('');
  }
}
