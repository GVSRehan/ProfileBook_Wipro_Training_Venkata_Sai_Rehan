import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { Router } from '@angular/router';
import { ToastService } from '../../shared/toast.service';
import { AuthService } from '../../services/auth.service';
import { AdminStateService } from '../../services/admin-state.service';

@Component({
  selector: 'app-manage-users',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './manage-users.html',
  styleUrls: ['./manage-users.css'],
})
export class ManageUsersComponent implements OnInit {

  users: any[] = [];
  showCreateUser = false;
  showEditUser = false;
  newUser: any = { username: '', email: '', password: '', mobileNumber: '' };
  editUserForm: any = { userId: 0, username: '', email: '', mobileNumber: '', password: '', isActive: true };

  constructor(
    private api: ApiService,
    private router: Router,
    private cd: ChangeDetectorRef,
    private toast: ToastService,
    private auth: AuthService,
    private adminState: AdminStateService
  ) {}

  ngOnInit(): void {
    if (!this.auth.isMainAdmin()) {
      this.toast.error('Only the main admin can manage users');
      this.router.navigate(['/admin/dashboard']);
      return;
    }

    this.adminState.managedUsers$.subscribe((users) => {
      this.users = users;
    });

    this.fetchUsers();
  }

  fetchUsers() {
    this.api.getAdminUsers().subscribe({
      next: (res:any) => {
        this.users = Array.isArray(res) ? res : [];
        this.adminState.setManagedUsers(this.users);
        try { this.cd.detectChanges(); } catch {}
      },
      error: err => {
        console.error('failed to load users', err);
        this.users = [];
        this.toast.error(extractServerMessage(err) ?? 'Failed to load users');
      }
    });
  }

  deleteUser(user: any) {
    const userId = Number(user?.userId ?? user?.id);
    if (!userId) {
      this.toast.error('Invalid user id. Refresh and try again.');
      return;
    }

    if (user?.isMainAdmin) {
      this.toast.error('Main admin cannot be deleted');
      return;
    }

    if (!confirm(`Delete user ${user.username}?`)) return;
    this.api.deleteAdminUser(userId).subscribe({
      next: () => {
        this.fetchUsers();
        this.toast.success(`User ${user.username} deleted`);
      },
      error: err => {
        console.error('delete failed', err);
        const serverMsg = extractServerMessage(err) ?? 'Failed to delete user';
        this.toast.error(serverMsg);
      }
    });
  }

  toggleCreateUser() {
    this.showCreateUser = !this.showCreateUser;
  }

  openEditUser(user: any) {
    this.editUserForm = {
      userId: user.userId,
      username: user.username,
      email: user.email,
      mobileNumber: user.mobileNumber,
      password: '',
      isActive: !!user.isActive
    };
    this.showEditUser = true;
  }

  closeEditUser() {
    this.showEditUser = false;
    this.editUserForm = { userId: 0, username: '', email: '', mobileNumber: '', password: '', isActive: true };
  }

  updateUser() {
    const payload = {
      username: this.editUserForm.username.trim(),
      email: this.editUserForm.email.trim(),
      mobileNumber: this.editUserForm.mobileNumber.trim(),
      password: this.editUserForm.password?.trim() || undefined,
      isActive: !!this.editUserForm.isActive
    };

    this.api.updateAdminUser(this.editUserForm.userId, payload).subscribe({
      next: (res: any) => {
        this.toast.success(res?.message ?? 'User updated successfully');
        this.closeEditUser();
        this.fetchUsers();
      },
      error: (err: any) => {
        const serverMsg = extractServerMessage(err) ?? 'Failed to update user';
        this.toast.error(serverMsg);
      }
    });
  }

  createUser() {
    if (!this.auth.isMainAdmin()) {
      this.toast.error('Only the main admin can create users');
      return;
    }

    if (!this.newUser.email || !this.newUser.username || !this.newUser.password || !this.newUser.mobileNumber) {
      this.toast.error('Please fill all fields');
      return;
    }

    if (!/^\d{10}$/.test(this.newUser.mobileNumber)) {
      this.toast.error('Mobile number must be exactly 10 digits');
      return;
    }

    const payload = {
      ...this.newUser,
      username: this.newUser.username.trim(),
      email: this.newUser.email.trim(),
      mobileNumber: this.newUser.mobileNumber.trim()
    };

    this.api.createUser(payload).subscribe({
      next: (res:any) => {
        const msg = typeof res?.message === 'string' ? res.message : (res?.message ?? 'User created');
        this.toast.success(msg);
        this.newUser = { username: '', email: '', password: '', mobileNumber: '' };
        this.showCreateUser = false;
        this.fetchUsers();
      },
      error: (err:any) => {
        console.error('create user error', err);
        const serverMsg = extractServerMessage(err) ?? 'Failed to create user';
        this.toast.error(serverMsg);
      }
    });
  }

  backToDashboard() {
    this.router.navigate(['/admin/dashboard']);
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
