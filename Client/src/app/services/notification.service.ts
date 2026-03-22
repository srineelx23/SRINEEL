import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { AuthService } from './auth.service';

export enum NotificationType {
  PolicyApproved = 0,
  PolicyRejected = 1,
  PremiumPaymentDue = 2,
  ClaimSubmitted = 3,
  PolicyExpiring = 4,
  ClaimApproved = 5,
  ClaimRejected = 6,
  PolicyRequestSubmitted = 7,
  PolicyTransferStatusChanged = 8,
  NewPolicyRequestAssigned = 9,
  NewClaimAssigned = 10
}

export interface Notification {
  notificationId: number;
  userId: number;
  title: string;
  message: string;
  type: NotificationType;
  isRead: boolean;
  createdAt: string;
  entityName?: string;
  entityId?: string;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private hubConnection!: signalR.HubConnection;
  private readonly baseUrl = 'https://localhost:7257/api/Notifications';
  private readonly hubUrl = 'https://localhost:7257/notificationHub';

  private notificationsSubject = new BehaviorSubject<Notification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();

  private unreadCountSubject = new BehaviorSubject<number>(0);
  public unreadCount$ = this.unreadCountSubject.asObservable();

  constructor(private http: HttpClient, private authService: AuthService) {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.startConnection();
        this.loadInitialData();
      } else {
        this.stopConnection();
      }
    });
  }

  private startConnection() {
    if (this.hubConnection) {
        this.hubConnection.stop();
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => this.authService.getToken() || '',
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => {
        console.log('SignalR Notification Hub connected.');
        const role = this.authService.getUserRole();
        if (role) {
          // Join group based on role for role-specific broadcasts
          this.hubConnection.invoke('JoinRoleGroup', role).catch(err => console.error(err));
        }
      })
      .catch(err => console.error('Error while starting hub connection: ' + err));

    this.hubConnection.on('ReceiveNotification', (notification: Notification) => {
      console.log('Notification received:', notification);
      const current = this.notificationsSubject.value;
      this.notificationsSubject.next([notification, ...current]);
      this.unreadCountSubject.next(this.unreadCountSubject.value + 1);
    });
  }

  private stopConnection() {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }

  private loadInitialData() {
    this.getUnreadCount().subscribe({
        next: (count) => this.unreadCountSubject.next(count),
        error: (err) => console.error('Error loading unread count', err)
    });
    this.getUnreadNotifications().subscribe({
        next: (notifs) => this.notificationsSubject.next(notifs),
        error: (err) => console.error('Error loading unread notifications', err)
    });
  }

  getNotifications(): Observable<Notification[]> {
    return this.http.get<Notification[]>(this.baseUrl);
  }

  getUnreadNotifications(): Observable<Notification[]> {
    return this.http.get<Notification[]>(`${this.baseUrl}/unread`);
  }

  getUnreadCount(): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/unread/count`);
  }

  markAsRead(id: number): Observable<any> {
    return this.http.post(`${this.baseUrl}/${id}/read`, {}).pipe(
      tap(() => {
        const notifs = this.notificationsSubject.value.map(n => 
          n.notificationId === id ? { ...n, isRead: true } : n
        );
        this.notificationsSubject.next(notifs);
        this.unreadCountSubject.next(Math.max(0, this.unreadCountSubject.value - 1));
      })
    );
  }

  markAllAsRead(): Observable<any> {
    return this.http.post(`${this.baseUrl}/read-all`, {}).pipe(
      tap(() => {
        const notifs = this.notificationsSubject.value.map(n => ({ ...n, isRead: true }));
        this.notificationsSubject.next(notifs);
        this.unreadCountSubject.next(0);
      })
    );
  }

  deleteNotification(id: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/${id}`).pipe(
      tap(() => {
        const notifs = this.notificationsSubject.value.filter(n => n.notificationId !== id);
        const wasUnread = this.notificationsSubject.value.find(n => n.notificationId === id)?.isRead === false;
        this.notificationsSubject.next(notifs);
        if (wasUnread) {
          this.unreadCountSubject.next(Math.max(0, this.unreadCountSubject.value - 1));
        }
      })
    );
  }
}
