import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { Router } from '@angular/router';
import { ToastService } from '../../shared/toast.service';
import { SignalRService } from '../../services/signalr.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-review-posts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './review-posts.html',
  styleUrl: './review-posts.css',
})
export class ReviewPostsComponent implements OnInit, OnDestroy {
  posts: any[] = [];
  isLoading = false;
  isSubmitting = false;
  loadError = '';
  rawPostCount = 0;
  showRejectionForm = false;
  rejectionReason = '';
  selectedPostId: number | null = null;
  private appNotificationSubscription: Subscription | null = null;

  constructor(
    private api: ApiService,
    private router: Router,
    private toast: ToastService,
    private cdr: ChangeDetectorRef,
    private signalR: SignalRService
  ) {}

  ngOnInit() {
    this.loadPosts();
    this.signalR.startConnection().catch(() => undefined);
    this.appNotificationSubscription = this.signalR.getAppNotifications().subscribe((notification) => {
      if (notification?.type === 'PostSubmitted') {
        this.loadPosts();
      }
    });
  }

  ngOnDestroy(): void {
    if (this.appNotificationSubscription) {
      this.appNotificationSubscription.unsubscribe();
    }
  }

  loadPosts() {
    this.isLoading = true;
    this.loadError = '';
    this.api.getAllPosts().subscribe({
      next: (allPosts) => {
        const normalizedPosts = Array.isArray(allPosts) ? allPosts : [];
        this.rawPostCount = normalizedPosts.length;
        this.posts = this.filterPendingPosts(normalizedPosts);
        if (this.selectedPostId && !this.posts.some((post) => post.postId === this.selectedPostId)) {
          this.cancelRejection();
        }
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error loading all posts', err);
        this.posts = [];
        this.rawPostCount = 0;
        this.isLoading = false;
        this.loadError = err?.error?.message || err?.message || 'Failed to load posts';
        this.toast.error(this.loadError);
        this.cdr.detectChanges();
      }
    });
  }

  private filterPendingPosts(posts: any[]): any[] {
    return posts
      .filter((post) => (post?.status ?? '').toString().trim().toLowerCase().includes('pending'))
      .sort((a, b) => new Date(b?.createdAt ?? 0).getTime() - new Date(a?.createdAt ?? 0).getTime());
  }

  approvePost(id: number) {
    if (this.isSubmitting) return;
    this.isSubmitting = true;
    this.api.approvePost(id).subscribe({
      next: (res) => {
        this.posts = this.posts.filter((post) => post.postId !== id);
        this.rawPostCount = Math.max(0, this.rawPostCount - 1);
        this.isSubmitting = false;
        this.toast.success(res?.message ?? 'Post approved successfully');
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error approving post', err);
        this.isSubmitting = false;
        this.toast.error(extractServerMessage(err) ?? 'Failed to approve post');
      }
    });
  }

  openRejectionForm(postId: number) {
    this.selectedPostId = postId;
    this.showRejectionForm = true;
    this.rejectionReason = '';
  }

  rejectPost() {
    if (!this.selectedPostId || this.isSubmitting) return;
    
    if (!this.rejectionReason.trim()) {
      this.toast.error('Please provide a rejection reason');
      return;
    }

    const postId = this.selectedPostId;
    this.isSubmitting = true;
    const payload = { reason: this.rejectionReason };
    this.api.rejectPostWithReason(postId, payload).subscribe({
      next: (res) => {
        this.posts = this.posts.filter((post) => post.postId !== postId);
        this.rawPostCount = Math.max(0, this.rawPostCount - 1);
        this.isSubmitting = false;
        this.showRejectionForm = false;
        this.selectedPostId = null;
        this.rejectionReason = '';
        this.toast.success(res?.message ?? 'Post rejected successfully');
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error rejecting post', err);
        this.isSubmitting = false;
        this.toast.error(extractServerMessage(err) ?? 'Failed to reject post');
      }
    });
  }

  cancelRejection() {
    this.showRejectionForm = false;
    this.selectedPostId = null;
    this.rejectionReason = '';
  }

  deletePost(id: number) {
    if (this.isSubmitting) return;
    if (confirm('Are you sure you want to delete this post?')) {
      this.isSubmitting = true;
      this.api.deletePost(id).subscribe({
        next: (res) => {
          this.posts = this.posts.filter((post) => post.postId !== id);
          this.rawPostCount = Math.max(0, this.rawPostCount - 1);
          this.isSubmitting = false;
          this.toast.success(res?.message ?? 'Post deleted successfully');
          this.cdr.detectChanges();
        },
        error: (err) => {
          console.error('Error deleting post', err);
          this.isSubmitting = false;
          this.toast.error(extractServerMessage(err) ?? 'Failed to delete post');
        }
      });
    }
  }

  backToDashboard() {
    this.router.navigate(['/admin/dashboard']);
  }

  trackByPostId(index: number, post: any): number {
    return post.postId;
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
