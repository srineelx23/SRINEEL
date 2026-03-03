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

  private authService = inject(AuthService);
  private router = inject(Router);

  // List of security questions
  securityQuestions = [
    "Father's Name",
    "Mother's Name",
    "Wife's Name",
    "Pet's Name"
  ];

  register() {
    this.errorMessage.set('');
    this.successMessage.set('');

    if (!this.firstName || !this.lastName || !this.email || !this.password || !this.securityQuestion || !this.securityAnswer) {
      this.errorMessage.set('Please fill out all required fields.');
      this.autoHideToast();
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.errorMessage.set('Passwords do not match.');
      this.autoHideToast();
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
      error: (err) => {
        if (err.error && err.error.message) {
          this.errorMessage.set(err.error.message);
        } else if (typeof err.error === 'string') {
          this.errorMessage.set(err.error);
        } else {
          this.errorMessage.set('Registration failed. Please check your details and try again.');
        }
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
