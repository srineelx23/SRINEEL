import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { extractErrorMessage } from '../../utils/error-handler';

@Component({
  selector: 'app-error-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './error-page.html',
  styleUrl: './error-page.css',
})
export class ErrorPage {
  private router = inject(Router);

  errorData: { status: number, message: string, title?: string };

  constructor() {
    const navigation = this.router.getCurrentNavigation();
    const rawData = navigation?.extras?.state as any;
    
    this.errorData = {
      status: rawData?.status || 404,
      message: extractErrorMessage(rawData?.message || rawData),
      title: rawData?.title || 'Wait! Something went wrong'
    };
  }

  goBack() {
    window.history.back();
  }

  goHome() {
    this.router.navigate(['/']);
  }
}
