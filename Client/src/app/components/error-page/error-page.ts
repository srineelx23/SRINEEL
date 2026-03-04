import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';

@Component({
  selector: 'app-error-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './error-page.html',
  styles: [`
    .error-container {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
      background: var(--bg-dark);
      background-image: 
        radial-gradient(circle at 10% 20%, rgba(220, 38, 38, 0.05) 0%, transparent 40%),
        radial-gradient(circle at 90% 80%, rgba(220, 38, 38, 0.05) 0%, transparent 40%);
    }

    .error-card {
      width: 100%;
      max-width: 600px;
      text-align: center;
      padding: 60px 40px;
      background: rgba(20, 20, 20, 0.6);
      backdrop-filter: blur(20px);
      border: 1px solid rgba(220, 38, 38, 0.2);
      border-radius: 32px;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
    }

    .error-icon {
      width: 100px;
      height: 100px;
      background: rgba(220, 38, 38, 0.1);
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto 32px;
      color: #ef4444;
      font-size: 3rem;
      border: 1px solid rgba(220, 38, 38, 0.2);
    }

    .status-code {
      font-size: 5rem;
      font-weight: 800;
      color: #ef4444;
      line-height: 1;
      margin-bottom: 16px;
      opacity: 0.9;
      letter-spacing: -2px;
    }

    .error-title {
      font-size: 2rem;
      font-weight: 700;
      color: var(--text-main);
      margin-bottom: 16px;
    }

    .error-message {
      color: var(--text-muted);
      font-size: 1.125rem;
      line-height: 1.6;
      margin-bottom: 40px;
      padding: 24px;
      background: rgba(0, 0, 0, 0.2);
      border-radius: 16px;
      border: 1px solid rgba(255, 255, 255, 0.05);
    }

    .btn-group {
      display: flex;
      gap: 16px;
      justify-content: center;
    }

    .btn-retry {
      background: #ef4444;
      color: white;
      border: none;
      padding: 14px 32px;
      border-radius: 12px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.3s ease;
    }

    .btn-retry:hover {
      background: #dc2626;
      transform: translateY(-2px);
      box-shadow: 0 10px 20px rgba(220, 38, 38, 0.2);
    }

    .btn-home {
      background: rgba(255, 255, 255, 0.05);
      color: var(--text-main);
      border: 1px solid rgba(255, 255, 255, 0.1);
      padding: 14px 32px;
      border-radius: 12px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.3s ease;
    }

    .btn-home:hover {
      background: rgba(255, 255, 255, 0.1);
      transform: translateY(-2px);
    }
  `]
})
export class ErrorPage {
  private router = inject(Router);

  errorData: { status: number, message: string, title?: string };

  constructor() {
    const navigation = this.router.getCurrentNavigation();
    this.errorData = navigation?.extras?.state as any || {
      status: 404,
      message: 'The page you are looking for might have been removed or is temporarily unavailable.',
      title: 'Wait! Something went wrong'
    };
  }

  goBack() {
    window.history.back();
  }

  goHome() {
    this.router.navigate(['/']);
  }
}
