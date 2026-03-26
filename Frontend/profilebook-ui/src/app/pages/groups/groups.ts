import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { ToastService } from '../../shared/toast.service';
import { AdminStateService } from '../../services/admin-state.service';

@Component({
  selector: 'app-groups',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './groups.html',
  styleUrl: './groups.css'
})
export class GroupsComponent implements OnInit {
  groups: any[] = [];
  users: any[] = [];
  isLoadingGroups = false;
  isLoadingUsers = false;
  isCreatingGroup = false;
  newGroup = {
    groupName: '',
    memberUserIds: [] as number[]
  };

  constructor(
    private api: ApiService,
    private router: Router,
    private toast: ToastService,
    private adminState: AdminStateService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.adminState.groups$.subscribe((groups) => {
      this.groups = Array.isArray(groups) ? [...groups] : [];
      try { this.cdr.detectChanges(); } catch {}
    });
    this.loadGroups();
    this.loadUsers();
  }

  loadGroups(): void {
    this.isLoadingGroups = true;
    this.api.getGroups().subscribe({
      next: (res) => {
        this.groups = Array.isArray(res) ? [...res] : [];
        this.adminState.setGroups(this.groups);
        this.isLoadingGroups = false;
        try { this.cdr.detectChanges(); } catch {}
      },
      error: () => {
        this.isLoadingGroups = false;
        this.toast.error('Failed to load groups');
        try { this.cdr.detectChanges(); } catch {}
      }
    });
  }

  loadUsers(): void {
    this.isLoadingUsers = true;
    this.api.getAdminUsers().subscribe({
      next: (res) => {
        this.users = Array.isArray(res) ? res.filter((user) => user.role === 'User') : [];
        this.isLoadingUsers = false;
        try { this.cdr.detectChanges(); } catch {}
      },
      error: () => {
        this.isLoadingUsers = false;
        this.toast.error('Failed to load users for groups');
        try { this.cdr.detectChanges(); } catch {}
      }
    });
  }

  onMemberToggle(userId: number, checked: boolean): void {
    const next = new Set(this.newGroup.memberUserIds);
    if (checked) {
      next.add(userId);
    } else {
      next.delete(userId);
    }
    this.newGroup.memberUserIds = Array.from(next);
  }

  createGroup(): void {
    if (!this.newGroup.groupName.trim()) {
      this.toast.error('Enter a group name');
      return;
    }

    this.isCreatingGroup = true;
    const requestedName = this.newGroup.groupName.trim();
    const requestedMemberIds = [...this.newGroup.memberUserIds];

    this.api.createGroup({
      groupName: requestedName,
      memberUserIds: requestedMemberIds
    }).subscribe({
      next: (res: any) => {
        const createdGroup = {
          groupId: res?.groupId ?? Date.now(),
          groupName: requestedName,
          memberUserIds: requestedMemberIds,
          createdAt: new Date().toISOString(),
          createdBy: 'Admin'
        };

        this.groups = [createdGroup, ...this.groups];
        this.adminState.setGroups(this.groups);
        this.toast.success(res?.message ?? 'Group created successfully');
        this.newGroup = { groupName: '', memberUserIds: [] };
        this.isCreatingGroup = false;
        try { this.cdr.detectChanges(); } catch {}
        this.loadGroups();
      },
      error: (err: any) => {
        this.isCreatingGroup = false;
        this.toast.error(err?.error?.message ?? 'Failed to create group');
        try { this.cdr.detectChanges(); } catch {}
      }
    });
  }

  deleteGroup(groupId: number): void {
    if (!confirm('Delete this group?')) {
      return;
    }

    this.api.deleteGroup(groupId).subscribe({
      next: (res: any) => {
        this.groups = this.groups.filter((group) => group.groupId !== groupId);
        this.adminState.setGroups(this.groups);
        this.toast.success(res?.message ?? 'Group deleted successfully');
        try { this.cdr.detectChanges(); } catch {}
        this.loadGroups();
      },
      error: (err: any) => {
        this.toast.error(err?.error?.message ?? 'Failed to delete group');
      }
    });
  }

  getUsername(userId: number): string {
    return this.users.find((user) => user.userId === userId)?.username ?? `User ${userId}`;
  }

  getGroupMemberNames(group: any): string {
    const memberIds = Array.isArray(group?.memberUserIds) ? group.memberUserIds : [];
    return memberIds.map((memberId: number) => this.getUsername(memberId)).join(', ');
  }

  trackByGroupId(_index: number, group: any): number {
    return Number(group?.groupId ?? _index);
  }

  trackByUserId(_index: number, user: any): number {
    return Number(user?.userId ?? _index);
  }

  backToDashboard(): void {
    this.router.navigate(['/admin/dashboard']);
  }
}
