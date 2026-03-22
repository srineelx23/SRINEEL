import { Component, OnInit, signal, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, Notification, NotificationType } from '../../services/notification.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notifications.html',
  styleUrl: './notifications.css'
})
export class NotificationsComponent implements OnInit, OnDestroy {
  notifications = signal<Notification[]>([]);
  filter = signal<'all' | 'unread'>('all');
  loading = signal<boolean>(true);
  
  private subs = new Subscription();

  constructor(private notificationService: NotificationService) {}

  ngOnInit() {
    this.loadNotifications();
  }

  ngOnDestroy() {
    this.subs.unsubscribe();
  }

  loadNotifications() {
    this.loading.set(true);
    const obs = this.filter() === 'all' 
      ? this.notificationService.getNotifications() 
      : this.notificationService.getUnreadNotifications();
      
    obs.subscribe({
      next: (data) => {
        this.notifications.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.loading.set(false);
      }
    });
  }

  setFilter(f: 'all' | 'unread') {
    this.filter.set(f);
    this.loadNotifications();
  }

  markAsRead(id: number) {
    this.notificationService.markAsRead(id).subscribe(() => {
      if (this.filter() === 'unread') {
        this.notifications.update(list => list.filter(n => n.notificationId !== id));
      } else {
        this.notifications.update(list => list.map(n => 
          n.notificationId === id ? { ...n, isRead: true } : n
        ));
      }
    });
  }

  markAllAsRead() {
    this.notificationService.markAllAsRead().subscribe(() => {
      if (this.filter() === 'unread') {
        this.notifications.set([]);
      } else {
        this.notifications.update(list => list.map(n => ({ ...n, isRead: true })));
      }
    });
  }

  delete(id: number, event: Event) {
    event.stopPropagation();
    this.notificationService.deleteNotification(id).subscribe(() => {
      this.notifications.update(list => list.filter(n => n.notificationId !== id));
    });
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
}
