import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-user-search-users',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './user-search-users.html',
  styleUrl: './user-search-users.css'
})
export class UserSearchUsersComponent implements OnInit {
  searchTerm = '';
  users: any[] = [];
  friends: any[] = [];
  incomingRequests: any[] = [];
  outgoingRequests: any[] = [];
  isLoading = false;

  constructor(
    private api: ApiService,
    private auth: AuthService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadPageData();
  }

  loadPageData(): void {
    this.isLoading = true;
    this.api.getUsers().subscribe({
      next: (res) => {
        this.users = Array.isArray(res) ? res.filter((user) => user?.role === 'User') : [];
        this.loadFriends();
        this.loadRequests();
      },
      error: () => {
        this.isLoading = false;
        this.toast.error('Failed to load users');
        this.safeDetectChanges();
      }
    });
  }

  loadFriends(): void {
    this.api.getFriends().subscribe({
      next: (res) => {
        this.friends = Array.isArray(res) ? res : [];
        this.safeDetectChanges();
      },
      error: () => {
        this.toast.error('Failed to load friends');
      }
    });
  }

  loadRequests(): void {
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

  getFilteredUsers(): any[] {
    const currentUserId = this.auth.getUserId();
    const baseUsers = this.users.filter((user) => user.userId !== currentUserId);
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) {
      return baseUsers;
    }

    return baseUsers.filter((user) =>
      `${user?.username ?? ''}`.toLowerCase().includes(term) ||
      `${user?.email ?? ''}`.toLowerCase().includes(term)
    );
  }

  sendFriendRequest(receiverId: number): void {
    this.api.sendFriendRequest(receiverId).subscribe({
      next: () => {
        this.toast.success('Friend request sent');
        this.loadRequests();
      },
      error: (err) => {
        this.toast.error(err?.error?.message ?? err?.error ?? 'Failed to send friend request');
      }
    });
  }

  reportUser(userId: number): void {
    const reason = prompt('Enter reason for report:')?.trim();
    if (!reason) {
      return;
    }

    this.api.reportUser({ reportedUserId: userId, reason }).subscribe({
      next: (res) => {
        this.toast.success(res?.message ?? 'User reported');
      },
      error: (err) => {
        this.toast.error(err?.error?.message ?? err?.error ?? 'Error reporting user');
      }
    });
  }

  canSendFriendRequest(userId: number): boolean {
    const alreadyFriend = this.friends.some((friend) => friend.userId === userId);
    const outgoingPending = this.outgoingRequests.some((request) => request.receiverId === userId && request.status === 'Pending');
    const incomingPending = this.incomingRequests.some((request) => request.senderId === userId && request.status === 'Pending');
    return !alreadyFriend && !outgoingPending && !incomingPending;
  }

  getFriendActionLabel(userId: number): string {
    if (this.friends.some((friend) => friend.userId === userId)) return 'Friend';
    if (this.outgoingRequests.some((request) => request.receiverId === userId && request.status === 'Pending')) return 'Requested';
    if (this.incomingRequests.some((request) => request.senderId === userId && request.status === 'Pending')) return 'Respond in Requests';
    return 'Add Friend';
  }

  private safeDetectChanges(): void {
    try {
      this.cdr.detectChanges();
    } catch {
      // no-op
    }
  }
}
