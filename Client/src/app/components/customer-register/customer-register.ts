import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-customer-register',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customer-register.html',
  styleUrl: './customer-register.css',
})
export class CustomerRegister {
  firstName = '';
  lastName = '';
  email = '';
  password = '';
  confirmPassword = '';
  securityQuestion = '';
  securityAnswer = '';

  errorMessage = signal('');
  successMessage = signal('');
  emailTouched = signal(false);

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

  register() {
    this.errorMessage.set('');
    this.successMessage.set('');
    this.emailTouched.set(true);

    if (!this.firstName || !this.lastName || !this.email || !this.password || !this.securityQuestion || !this.securityAnswer) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: 'Please fill out all required fields.', title: 'Registration Error' }
      });
      return;
    }

    if (!this.isValidEmail(this.email)) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: 'Please enter a valid email address (e.g. user@example.com).', title: 'Validation Error' }
      });
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: 'Passwords do not match.', title: 'Validation Error' }
      });
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
        const errorStatus = err.status || 500;
        const errorMessage = typeof err.error === 'string' ? err.error : (err.error?.message || 'Registration failed. Please check your details.');

        this.router.navigate(['/error'], {
          state: {
            status: errorStatus,
            message: errorMessage,
            title: 'Registration Rejected'
          }
        });
      }
    });
  }

  private autoHideToast() {
    setTimeout(() => {
      this.errorMessage.set('');
    }, 5000);
  }
}
