import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-user-friend-requests',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './user-friend-requests.html',
  styleUrl: './user-friend-requests.css'
})
export class UserFriendRequestsComponent implements OnInit {
  incomingRequests: any[] = [];
  outgoingRequests: any[] = [];
  isLoading = false;

  constructor(
    private api: ApiService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadRequests();
  }

  loadRequests(): void {
    this.isLoading = true;
    this.api.getMyFriendRequests().subscribe({
      next: (res: any) => {
        this.incomingRequests = Array.isArray(res?.incoming) ? res.incoming : [];
        this.outgoingRequests = Array.isArray(res?.outgoing) ? res.outgoing : [];
        this.isLoading = false;
        this.safeDetectChanges();
      },
      error: () => {
        this.isLoading = false;
        this.toast.error('Failed to load friend requests');
        this.safeDetectChanges();
      }
    });
  }

  respondToFriendRequest(requestId: number, action: 'accept' | 'reject'): void {
    this.api.respondToFriendRequest(requestId, action).subscribe({
      next: () => {
        this.toast.success(`Friend request ${action}ed`);
        this.loadRequests();
      },
      error: (err) => {
        this.toast.error(err?.error?.message ?? err?.error ?? `Failed to ${action} request`);
      }
    });
  }

  private safeDetectChanges(): void {
    try {
      this.cdr.detectChanges();
    } catch {
      // no-op
    }
  }
}
