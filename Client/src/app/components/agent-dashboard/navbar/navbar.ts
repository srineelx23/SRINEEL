import { Component, input, output, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationBellComponent } from '../../notification-bell/notification-bell.component';
import { ThemeService } from '../../../services/theme.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, NotificationBellComponent],
  templateUrl: './navbar.html',
  styleUrl: './navbar.css'
})
export class NavbarComponent {
  protected readonly themeService = inject(ThemeService);
  activeTab = input.required<string>();
  agentName = input.required<string>();
  userRole = input.required<string>();
  showUserDropdown = signal(false);

  onTabSwitch = output<string>();
  onLogout = output<void>();
  onGoHome = output<void>();

  switchTab(tab: string) {
    this.onTabSwitch.emit(tab);
  }

  logout() {
    this.onLogout.emit();
  }

  goHome() {
    this.onGoHome.emit();
  }
}
