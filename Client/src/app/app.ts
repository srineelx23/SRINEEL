import { Component, signal, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './services/auth.service';
import { ThemeService } from './services/theme.service';
import { ChatbotWidgetComponent } from './components/chatbot-widget/chatbot-widget';
import { AdminAiAssistantComponent } from './components/admin-ai-assistant/admin-ai-assistant';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CommonModule, ChatbotWidgetComponent, AdminAiAssistantComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  protected readonly themeService = inject(ThemeService);
  private readonly chatbotContextHandler = () => this.evaluateChatbotVisibility(this.router.url);

  protected readonly title = signal('Client');
  showCustomerChatbot = signal(false);
  showAdminAssistant = signal(false);

  ngOnInit() {
    this.evaluateChatbotVisibility(this.router.url);
    this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event) => {
        const navEvent = event as NavigationEnd;
        this.evaluateChatbotVisibility(navEvent.urlAfterRedirects);
      });

    window.addEventListener('vims-chatbot-context-changed', this.chatbotContextHandler);
  }

  ngOnDestroy(): void {
    window.removeEventListener('vims-chatbot-context-changed', this.chatbotContextHandler);
  }

  toggleTheme() {
    this.themeService.toggleTheme();
  }

  private evaluateChatbotVisibility(url: string): void {
    this.showCustomerChatbot.set(false);
    this.showAdminAssistant.set(false);

    if (!this.authService.isLoggedIn()) {
      return;
    }

    const normalizedUrl = (url || '').toLowerCase().split('?')[0].split('#')[0];
    const hideOnRoutes = ['/', '/login', '/register', '/error'];
    if (hideOnRoutes.includes(normalizedUrl)) {
      return;
    }

    const role = this.authService.getUserRole();
    if (!role) {
      return;
    }

    if (role === 'Customer') {
      this.showCustomerChatbot.set(normalizedUrl === '/explore-plans');
      return;
    }

    if (role === 'Agent') {
      if (normalizedUrl !== '/agent-dashboard') {
        return;
      }

      const activeTab = (localStorage.getItem('vims.agent.activeTab') || '').toLowerCase();
      this.showCustomerChatbot.set(activeTab === 'applications');
      return;
    }

    if (role === 'ClaimsOfficer') {
      if (normalizedUrl !== '/claims-dashboard') {
        return;
      }

      const activeTab = (localStorage.getItem('vims.claimsOfficer.activeTab') || '').toLowerCase();
      this.showCustomerChatbot.set(activeTab === 'pending');
      return;
    }

    if (role === 'Admin') {
      this.showAdminAssistant.set(normalizedUrl === '/admin-dashboard');
    }
  }
}
