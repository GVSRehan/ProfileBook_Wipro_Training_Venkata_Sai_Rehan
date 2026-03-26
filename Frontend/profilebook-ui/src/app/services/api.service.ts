import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Post, PostComment } from '../models/post';
import { environment } from '../../environments/environment';
import { FetchClientService } from './fetch-client.service';

@Injectable({
  providedIn: 'root'
})
export class ApiService {

  private baseUrl = `${environment.apiBaseUrl}/api`;

  constructor(private fetchClient: FetchClientService) {}

  getUsers(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/User`);
  }

  getCurrentUserProfile(): Observable<any> {
    return this.fetchClient.get<any>(`${this.baseUrl}/User/me`);
  }

  updateCurrentUserProfile(data: FormData): Observable<any> {
    return this.fetchClient.put(`${this.baseUrl}/User/me`, data);
  }

  getAdminUsers(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/Admin/users`);
  }

  updateAdminUser(id: number, data: any): Observable<any> {
    return this.fetchClient.put(`${this.baseUrl}/Admin/users/${id}`, data);
  }

  getPosts(search = ''): Observable<Post[]> {
    const query = this.buildQuery({ search });
    return this.fetchClient.get<Post[]>(`${this.baseUrl}/Post${query}`);
  }

  createPost(data: FormData): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Post`, data);
  }

  togglePostLike(postId: number): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Post/${postId}/like`, {});
  }

  addPostComment(postId: number, commentText: string): Observable<PostComment & { commentCount: number }> {
    return this.fetchClient.post<PostComment & { commentCount: number }>(`${this.baseUrl}/Post/${postId}/comments`, { commentText });
  }

  sharePost(postId: number, recipientUserId: number): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Post/${postId}/share`, { recipientUserId });
  }

  deleteUser(id: number): Observable<any> {
    return this.fetchClient.delete(`${this.baseUrl}/User/${id}`);
  }

  approvePost(id: number): Observable<any> {
    return this.fetchClient.put(`${this.baseUrl}/Post/approve/${id}`, {});
  }

  rejectPost(id: number): Observable<any> {
    return this.fetchClient.put(`${this.baseUrl}/Post/reject/${id}`, {});
  }

  rejectPostWithReason(id: number, payload: any): Observable<any> {
    return this.fetchClient.put(`${this.baseUrl}/Post/reject/${id}`, payload);
  }

  getMessages(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/Message`);
  }

  markConversationAsRead(friendId: number): Observable<any> {
    return this.fetchClient.put(`${this.baseUrl}/Message/read/${friendId}`, {});
  }

  sendMessage(data: any): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Message`, data);
  }

  reportUser(data: any): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Report`, data);
  }

  getReports(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/Report`);
  }

  takeReportAction(reportId: number, payload: any): Observable<any> {
    return this.fetchClient.put(`${this.baseUrl}/Report/${reportId}/action`, payload);
  }

  sendFriendRequest(receiverId: number): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/FriendRequest/send`, { receiverId });
  }

  respondToFriendRequest(requestId: number, action: 'accept' | 'reject'): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/FriendRequest/respond/${requestId}`, { action });
  }

  getMyFriendRequests(): Observable<any> {
    return this.fetchClient.get(`${this.baseUrl}/FriendRequest/mine`);
  }

  getFriends(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/FriendRequest/friends`);
  }

  getAlerts(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/Alert`);
  }

  getMyNotifications(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/UserNotification/mine`);
  }

  markAllNotificationsRead(): Observable<any> {
    return this.fetchClient.put(`${this.baseUrl}/UserNotification/read-all`, {});
  }

  broadcastAlert(content: string): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Alert/broadcast`, { content });
  }

  // Admin methods
  getDashboardStats(): Observable<any> {
    return this.fetchClient.get(`${this.baseUrl}/Admin/dashboard`);
  }

  createAdmin(data: any): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Admin/create-admin`, data);
  }

  createUser(data: any): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Admin/create-user`, data);
  }

  getAdminProfile(userId: number): Observable<any> {
    return this.fetchClient.get(`${this.baseUrl}/Admin/profile/${userId}`);
  }

  extendCredentials(userId: number, minutes: number): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Admin/extend-credentials/${userId}`, minutes);
  }

  getExpiringCredentials(): Observable<any> {
    return this.fetchClient.get(`${this.baseUrl}/Admin/expiring-credentials`);
  }

  getExpiredCredentials(): Observable<any> {
    return this.fetchClient.get(`${this.baseUrl}/Admin/expired-credentials`);
  }

  deactivateExpired(): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Admin/deactivate-expired`, {});
  }

  setMainAdmin(userId: number): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Admin/set-main-admin/${userId}`, {});
  }

  getDurationOptions(): Observable<any> {
    return this.fetchClient.get(`${this.baseUrl}/Admin/duration-options`);
  }

  getAllPosts(search = ''): Observable<any> {
    const query = this.buildQuery({ search });
    return this.fetchClient.get(`${this.baseUrl}/Post/all${query}`);
  }

  getPendingPosts(): Observable<any> {
    return this.fetchClient.get(`${this.baseUrl}/Post/pending`);
  }

  deletePost(id: number): Observable<any> {
    return this.fetchClient.delete(`${this.baseUrl}/Post/${id}`);
  }

  // Admin delete user (admin deletion endpoint)
  deleteAdminUser(id: number): Observable<any> {
    return this.fetchClient.delete(`${this.baseUrl}/Admin/users/${id}`);
  }

  getGroups(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/Group`);
  }

  getMyGroups(): Observable<any> {
    return this.fetchClient.get<any[]>(`${this.baseUrl}/Group/mine`);
  }

  createGroup(payload: any): Observable<any> {
    return this.fetchClient.post(`${this.baseUrl}/Group`, payload);
  }

  deleteGroup(groupId: number): Observable<any> {
    return this.fetchClient.delete(`${this.baseUrl}/Group/${groupId}`);
  }

  private buildQuery(params: Record<string, string | number | null | undefined>): string {
    const searchParams = new URLSearchParams();

    Object.entries(params).forEach(([key, value]) => {
      if (value === null || value === undefined) {
        return;
      }

      const normalized = `${value}`.trim();
      if (normalized) {
        searchParams.set(key, normalized);
      }
    });

    const query = searchParams.toString();
    return query ? `?${query}` : '';
  }
}
