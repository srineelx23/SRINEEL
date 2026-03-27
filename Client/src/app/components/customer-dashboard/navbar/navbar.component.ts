import { Component, Input, Output, EventEmitter, Signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationBellComponent } from '../../notification-bell/notification-bell.component';
import { ThemeService } from '../../../services/theme.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, NotificationBellComponent],
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.css',
})
export class NavbarComponent {
  protected readonly themeService = inject(ThemeService);
  @Input() activeTab!: Signal<string>;
  @Input() incomingTransfers!: Signal<any[]>;
  @Input() customerName!: Signal<string>;
  @Input() userRole!: Signal<string>;
  @Input() showUserDropdown!: Signal<boolean>;

  @Output() onSwitchTab = new EventEmitter<string>();
  @Output() onGoHome = new EventEmitter<void>();
  @Output() onLogout = new EventEmitter<void>();
  @Output() onToggleUserDropdown = new EventEmitter<boolean>();

  switchTab(tab: string) {
    this.onSwitchTab.emit(tab);
  }
}
