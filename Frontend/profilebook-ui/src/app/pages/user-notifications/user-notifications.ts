import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-user-notifications',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './user-notifications.html',
  styleUrl: './user-notifications.css'
})
export class UserNotificationsComponent implements OnInit {
  notifications: any[] = [];
  isLoading = false;

  constructor(
    private api: ApiService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.isLoading = true;
    this.api.getMyNotifications().subscribe({
      next: (res) => {
        this.notifications = Array.isArray(res) ? res : [];
        this.isLoading = false;
        this.safeDetectChanges();
      },
      error: () => {
        this.isLoading = false;
        this.toast.error('Failed to load notifications');
        this.safeDetectChanges();
      }
    });
  }

  markAllRead(): void {
    this.api.markAllNotificationsRead().subscribe({
      next: () => {
        this.notifications = this.notifications.map((notification) => ({
          ...notification,
          isRead: true
        }));
        this.toast.success('Notifications marked as read');
        this.safeDetectChanges();
      },
      error: () => {
        this.toast.error('Failed to mark notifications as read');
      }
    });
  }

  getUnreadCount(): number {
    return this.notifications.filter((notification) => !notification.isRead).length;
  }

  private safeDetectChanges(): void {
    try {
      this.cdr.detectChanges();
    } catch {
      // no-op
    }
  }
}
