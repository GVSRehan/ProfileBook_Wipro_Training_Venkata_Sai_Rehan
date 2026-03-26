import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { ToastService } from '../../shared/toast.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-user-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './user-profile.html',
  styleUrl: './user-profile.css'
})
export class UserProfileComponent implements OnInit, OnDestroy {
  @ViewChild('profileImageInput') profileImageInput?: ElementRef<HTMLInputElement>;

  profile: any | null = null;
  form = {
    username: '',
    email: '',
    mobileNumber: ''
  };
  draftProfile = {
    username: '',
    email: '',
    mobileNumber: ''
  };
  selectedProfileImageFile: File | null = null;
  profileImagePreviewUrl: string | null = null;
  isSaving = false;

  constructor(
    private api: ApiService,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.loadProfile();
  }

  ngOnDestroy(): void {
    this.revokePreview();
  }

  loadProfile(): void {
    this.api.getCurrentUserProfile().subscribe({
      next: (res) => {
        const profile = this.normalizeProfile(res);
        this.profile = profile;
        this.form = {
          username: profile.username,
          email: profile.email,
          mobileNumber: profile.mobileNumber
        };
        this.draftProfile = { ...this.form };
      },
      error: (err) => {
        this.toast.error(err?.error?.message ?? err?.error ?? 'Failed to load your profile');
      }
    });
  }

  onProfileImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files.length > 0 ? input.files[0] : null;

    this.selectedProfileImageFile = file;
    this.revokePreview();
    this.profileImagePreviewUrl = file ? URL.createObjectURL(file) : null;
  }

  saveProfile(): void {
    const username = `${this.draftProfile.username ?? ''}`.trim();
    if (!username) {
      this.toast.error('Username is required');
      return;
    }

    const formData = new FormData();
    formData.append('username', username);

    if (this.selectedProfileImageFile) {
      formData.append('profileImageFile', this.selectedProfileImageFile);
    }

    this.isSaving = true;
    this.api.updateCurrentUserProfile(formData).subscribe({
      next: (res) => {
        this.isSaving = false;
        this.toast.success(res?.message ?? 'Profile updated successfully');
        this.selectedProfileImageFile = null;
        this.revokePreview();
        this.profileImagePreviewUrl = null;

        if (this.profileImageInput?.nativeElement) {
          this.profileImageInput.nativeElement.value = '';
        }

        if (res?.user) {
          const profile = this.normalizeProfile(res.user);
          this.profile = profile;
          this.form = {
            username: profile.username,
            email: profile.email,
            mobileNumber: profile.mobileNumber
          };
          this.draftProfile = { ...this.form };
        } else {
          this.loadProfile();
        }
      },
      error: (err) => {
        this.isSaving = false;
        this.toast.error(err?.error?.message ?? err?.error ?? 'Failed to update profile');
      }
    });
  }

  getAvatarInitial(name?: string | null): string {
    const value = `${name ?? ''}`.trim();
    return value ? value.charAt(0).toUpperCase() : '?';
  }

  getProfileImageUrl(path?: string | null): string | null {
    if (this.profileImagePreviewUrl) {
      return this.profileImagePreviewUrl;
    }

    if (!path) {
      return null;
    }

    return path.startsWith('http') ? path : `${environment.apiBaseUrl}${path}`;
  }

  private revokePreview(): void {
    if (this.profileImagePreviewUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(this.profileImagePreviewUrl);
    }
  }

  private normalizeProfile(source: any): { username: string; email: string; mobileNumber: string; profileImage: string | null } {
    return {
      username: `${source?.username ?? source?.Username ?? ''}`,
      email: `${source?.email ?? source?.Email ?? ''}`,
      mobileNumber: `${source?.mobileNumber ?? source?.MobileNumber ?? ''}`,
      profileImage: source?.profileImage ?? source?.ProfileImage ?? null
    };
  }
}
