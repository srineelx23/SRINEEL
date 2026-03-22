import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationBellComponent } from '../../notification-bell/notification-bell.component';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, NotificationBellComponent],

  templateUrl: './navbar.html',
  styleUrl: './navbar.css',
})
export class NavbarComponent {
  adminName = input.required<string>();
  userRole = input.required<string>();
  activeTab = input.required<string>();

  onTabSwitch = output<string>();
  onLogout = output<void>();
  onGoHome = output<void>();

  showUserDropdown = signal(false);

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
