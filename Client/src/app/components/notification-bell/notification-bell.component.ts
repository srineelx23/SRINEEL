import { Component, OnInit, signal, effect, OnDestroy, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NotificationService, Notification, NotificationType } from '../../services/notification.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.css'
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  @Output() onViewAll = new EventEmitter<void>();

  notifications = signal<Notification[]>([]);
  unreadCount = signal<number>(0);
  showDropdown = signal<boolean>(false);
  
  private subs: Subscription = new Subscription();

  constructor(private notificationService: NotificationService) {}

  ngOnInit() {
    this.subs.add(
      this.notificationService.notifications$.subscribe(notifs => {
        this.notifications.set(notifs.slice(0, 5)); // Show only top 5 in dropdown
      })
    );

    this.subs.add(
      this.notificationService.unreadCount$.subscribe(count => {
        this.unreadCount.set(count);
      })
    );
  }

  ngOnDestroy() {
    this.subs.unsubscribe();
  }

  toggleDropdown() {
    this.showDropdown.update(v => !v);
  }

  markAsRead(id: number, event: Event) {
    event.stopPropagation();
    this.notificationService.markAsRead(id).subscribe();
  }

  markAllAsRead() {
    this.notificationService.markAllAsRead().subscribe();
  }

  getNotificationIcon(type: NotificationType): string {
    switch(type) {
      case NotificationType.PolicyApproved: return 'fa-circle-check text-success';
      case NotificationType.PolicyRejected: return 'fa-circle-xmark text-danger';
      case NotificationType.ClaimApproved: return 'fa-money-bill-trend-up text-success';
      case NotificationType.ClaimRejected: return 'fa-circle-xmark text-danger';
      case NotificationType.PolicyExpiring: return 'fa-hourglass-half text-warning';
      case NotificationType.PremiumPaymentDue: return 'fa-file-invoice-dollar text-primary';
      case NotificationType.NewPolicyRequestAssigned: return 'fa-briefcase text-info';
      case NotificationType.NewClaimAssigned: return 'fa-magnifying-glass-chart text-info';
      default: return 'fa-bell text-secondary';
    }
  }

  onNotificationClick(notif: Notification) {
    if (!notif.isRead) {
        this.notificationService.markAsRead(notif.notificationId).subscribe();
    }
    this.showDropdown.set(false);
    // Navigate or emit based on entity
  }

  viewAll() {
    this.showDropdown.set(false);
    this.onViewAll.emit();
  }
}
