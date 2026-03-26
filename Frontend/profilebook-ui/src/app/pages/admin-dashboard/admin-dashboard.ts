import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';
import { Router } from '@angular/router';
import { AdminStateService } from '../../services/admin-state.service';
import { SignalRService } from '../../services/signalr.service';
import { Subscription, finalize, take, timeout } from 'rxjs';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './admin-dashboard.html',
  styleUrls: ['./admin-dashboard.css']
})
export class AdminDashboardComponent implements OnInit, OnDestroy {

  totalUsers = 0;
  pendingPosts = 0;
  reports = 0;
  expiringAdmins: any[] = [];
  // Add admin form state
  showAddAdmin = false;
  showAdminMenu = false;
  showExtendModal = false;
  showAdminProfileModal = false;
  isAdminProfileLoading = false;
  selectedAdminForExtend: any = null;
  adminProfile: any = null;
  isMainAdmin = false;
  alertMessage = '';
  extendMinutes = 1440; // 1 day default
  newAdmin: { email: string; username: string; password: string; mobileNumber: string; durationOption?: number } = { email: '', username: '', password: '', mobileNumber: '', durationOption: 1440 };
  // duration options
  durationOptions: any[] = [];
  private appNotificationSubscription: Subscription | null = null;

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private router: Router,
    private cd: ChangeDetectorRef,
    private toast: ToastService,
    private adminState: AdminStateService,
    private signalR: SignalRService
  ) {}

  ngOnInit(): void {
    this.isMainAdmin = this.auth.isMainAdmin();
    this.adminState.dashboardStats$.subscribe((stats) => {
      this.totalUsers = stats.totalUsers;
      this.pendingPosts = stats.pendingPosts;
      this.reports = stats.reports;
    });
    this.adminState.expiringAdmins$.subscribe((admins) => {
      this.expiringAdmins = admins;
    });
    this.loadDashboard();
    this.signalR.startConnection().catch(() => undefined);
    this.appNotificationSubscription = this.signalR.getAppNotifications().subscribe((notification) => {
      if (notification?.type === 'PostSubmitted') {
        this.loadDashboard();
        this.toast.success(notification.message ?? 'A new post is waiting for review');
      }
    });
    if (this.isMainAdmin) {
      this.loadDurationOptions();
      this.loadExpiringAdmins();
      setInterval(() => this.loadExpiringAdmins(), 5 * 60 * 1000);
    }
  }

  ngOnDestroy(): void {
    if (this.appNotificationSubscription) {
      this.appNotificationSubscription.unsubscribe();
    }
  }

  loadDashboard() {
    this.api.getDashboardStats().subscribe({
      next: (res:any) => {
        this.totalUsers = res?.totalUsers ?? this.totalUsers;
        this.pendingPosts = res?.pendingPosts ?? this.pendingPosts;
        this.reports = res?.reports ?? this.reports;
        this.adminState.setDashboardStats({
          totalUsers: this.totalUsers,
          pendingPosts: this.pendingPosts,
          reports: this.reports
        });
        try { this.cd.detectChanges(); } catch {}
      },
      error: (err:any) => console.error('failed to load dashboard', err)
    });
  }

  loadDurationOptions() {
    this.api.getDurationOptions().subscribe({
      next: (res:any) => this.durationOptions = Array.isArray(res) ? res : [],
      error: (err:any) => console.error('failed to load duration options', err)
    });
  }

  loadExpiringAdmins() {
    this.api.getExpiringCredentials().subscribe({
      next: (res:any) => {
        this.expiringAdmins = Array.isArray(res) ? res : [];
        this.adminState.setExpiringAdmins(this.expiringAdmins);
        try { this.cd.detectChanges(); } catch {}
      },
      error: (err:any) => console.error('failed to load expiring admins', err)
    });
  }

  openExtendModal(admin: any) {
    this.selectedAdminForExtend = admin;
    this.showExtendModal = true;
    this.extendMinutes = 1440; // Reset to 1 day
  }

  extendAdminCredentials() {
    if (!this.selectedAdminForExtend || !this.extendMinutes) {
      this.toast.error('Please select a valid time extension');
      return;
    }

    this.api.extendCredentials(this.selectedAdminForExtend.userId, this.extendMinutes).subscribe({
      next: (res:any) => {
        this.toast.success('Admin credentials extended successfully');
        this.showExtendModal = false;
        this.selectedAdminForExtend = null;
        this.loadExpiringAdmins();
      },
      error: (err:any) => {
        console.error('extend credentials error', err);
        const msg = extractServerMessage(err) ?? 'Failed to extend credentials';
        this.toast.error(msg);
      }
    });
  }

  closeExtendModal() {
    this.showExtendModal = false;
    this.selectedAdminForExtend = null;
  }

  toggleAddAdmin() {
    if (!this.isMainAdmin) {
      this.toast.error('Only the main admin can create admins');
      return;
    }
    this.showAddAdmin = !this.showAddAdmin;
  }

  toggleAdminMenu() {
    this.showAdminMenu = !this.showAdminMenu;
  }

  viewAdminProfile() {
    this.showAdminMenu = false;
    const userId = this.auth.getUserId();
    if (!userId) {
      this.toast.error('Unable to identify the logged-in admin');
      return;
    }

    this.adminProfile = null;
    this.isAdminProfileLoading = true;
    this.showAdminProfileModal = true;

    this.api.getAdminProfile(userId)
      .pipe(
        take(1),
        timeout(8000),
        finalize(() => {
          this.isAdminProfileLoading = false;
          try { this.cd.detectChanges(); } catch {}
        })
      )
      .subscribe({
        next: (res: any) => {
          this.adminProfile = res;
        },
        error: (err: any) => {
          this.showAdminProfileModal = false;
          console.error('admin profile error', err);
          const serverMsg = extractServerMessage(err) ?? 'Failed to load admin profile';
          this.toast.error(serverMsg);
        }
      });
  }

  submitNewAdmin() {
    if (!this.isMainAdmin) {
      this.toast.error('Only the main admin can create admins');
      return;
    }

    if (!this.newAdmin.email || !this.newAdmin.username || !this.newAdmin.password || !this.newAdmin.mobileNumber) {
      this.toast.error('Please fill all fields');
      return;
    }

    // Validate mobile number format (10 digits)
    if (!/^\d{10}$/.test(this.newAdmin.mobileNumber)) {
      this.toast.error('Mobile number must be exactly 10 digits');
      return;
    }

    const payload = {
      email: this.newAdmin.email.trim(),
      username: this.newAdmin.username.trim(),
      password: this.newAdmin.password,
      mobileNumber: this.newAdmin.mobileNumber.trim(),
      durationOption: Number(this.newAdmin.durationOption ?? 1440)
    };

    this.api.createAdmin(payload).subscribe({
      next: (res:any) => {
        const msg = typeof res?.message === 'string' ? res.message : (res?.message ?? 'Admin created');
        this.toast.success(msg);
        this.newAdmin = { email: '', username: '', password: '', mobileNumber: '', durationOption: 1440 };
        this.showAddAdmin = false;
        this.loadExpiringAdmins();
      },
      error: (err:any) => {
        console.error('create admin error', err);
        const serverMsg = extractServerMessage(err) ?? 'Failed to create admin';
        this.toast.error(serverMsg);
      }
    });
  }

  logout() {
    this.auth.logout();
  }

  closeAdminProfile() {
    this.showAdminProfileModal = false;
    this.isAdminProfileLoading = false;
    this.adminProfile = null;
  }

  sendAlert() {
    const content = this.alertMessage.trim();
    if (!content) {
      this.toast.error('Please enter an alert message');
      return;
    }

    this.api.broadcastAlert(content).subscribe({
      next: (res: any) => {
        this.toast.success(res?.message ?? 'Alert sent successfully');
        this.alertMessage = '';
      },
      error: (err: any) => {
        const serverMsg = extractServerMessage(err) ?? 'Failed to send alert';
        this.toast.error(serverMsg);
      }
    });
  }

}

function extractServerMessage(err: any): string | null {
  if (!err) return null;
  if (typeof err === 'string') return err;
  if (err.error) {
    if (typeof err.error === 'string') return err.error;
    if (err.error.message) return err.error.message;
  }
  if (err.message) return err.message;
  return null;
}
