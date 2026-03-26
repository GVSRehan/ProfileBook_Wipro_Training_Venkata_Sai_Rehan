import { ChangeDetectorRef, Component, ElementRef, NgZone, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { UserStateService } from '../../services/user-state.service';
import { ToastService } from '../../shared/toast.service';
import { Post } from '../../models/post';
import { environment } from '../../../environments/environment';

type DashboardPanel = 'messages' | 'notifications' | 'requests' | 'search';

@Component({
  selector: 'app-user-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './user-dashboard.html',
  styleUrl: './user-dashboard.css',
})
export class UserDashboardComponent implements OnInit, OnDestroy {
  @ViewChild('postImageInput') postImageInput?: ElementRef<HTMLInputElement>;

  posts: Post[] = [];
  currentUserProfile: any | null = null;
  newPost = { content: '' };
  selectedPostImageFile: File | null = null;
  postImagePreviewUrl: string | null = null;

  messages: any[] = [];
  newMessageContent = '';
  users: any[] = [];
  visibleUsers: any[] = [];
  friends: any[] = [];
  groups: any[] = [];
  alerts: any[] = [];
  userNotifications: any[] = [];
  incomingRequests: any[] = [];
  outgoingRequests: any[] = [];
  activePanel: DashboardPanel = 'messages';
  postSearchTerm = '';
  messageSearchTerm = '';
  userSearchTerm = '';
  shareSearchTerm = '';
  shareMenuPostId: number | null = null;
  expandedCommentsPostId: number | null = null;
  commentDrafts: Record<number, string> = {};
  selectedFriendId: number | null = null;
  selectedGroupId: number | null = null;
  selectedChatType: 'friend' | 'group' = 'friend';
  selectedFriend: any | null = null;
  selectedGroup: any | null = null;
  conversationMessages: any[] = [];
  unreadCounts: Record<number, number> = {};
  groupUnreadCounts: Record<number, number> = {};
  groupLastSeenAt: Record<number, string> = {};
  lastOwnMessageReadState = '';
  chatStatus = 'Connecting to live chat...';
  chatNotifications: { id: number; text: string }[] = [];
  notificationPermission: 'default' | 'denied' | 'granted' | 'unsupported' = 'default';
  typingFriendId: number | null = null;
  typingUsername = '';

  private typingResetHandle: number | null = null;
  private lastTypingSentAt = 0;
  private readConversationRequestInFlight = false;
  private messageSubscription: Subscription | null = null;
  private typingSubscription: Subscription | null = null;
  private messagesReadSubscription: Subscription | null = null;
  private appNotificationSubscription: Subscription | null = null;
  private postsStoreSubscription: Subscription | null = null;
  private notificationsStoreSubscription: Subscription | null = null;

  constructor(
    private auth: AuthService,
    private api: ApiService,
    private signalR: SignalRService,
    private cdr: ChangeDetectorRef,
    private ngZone: NgZone,
    private userState: UserStateService,
    private toast: ToastService
  ) {}

  ngOnInit(): void {
    this.notificationPermission = this.getNotificationPermission();
    this.groupLastSeenAt = this.readGroupSeenState();

    this.postsStoreSubscription = this.userState.posts$.subscribe((posts) => {
      this.posts = this.normalizePosts(posts);
    });

    this.notificationsStoreSubscription = this.userState.notifications$.subscribe((notifications) => {
      this.userNotifications = Array.isArray(notifications) ? [...notifications] : [];
    });

    this.loadPosts();
    this.loadCurrentUserProfile();
    this.loadNotifications();
    this.loadMessages();
    this.loadUsers();
    this.loadFriends();
    this.loadGroups();
    this.loadFriendRequests();
    this.loadAlerts();

    this.signalR.startConnection().catch(() => {
      this.chatStatus = 'Live chat is reconnecting...';
      this.safeDetectChanges();
    });

    this.messageSubscription = this.signalR.getMessageReceived().subscribe((message) => {
      this.ngZone.run(() => {
        if (!this.messages.some((existing) => existing.messageId === message.messageId)) {
          this.messages = [...this.messages, message];
        }

        if (!this.isOwnMessage(message)) {
          const senderName = this.getSenderName(message);
          this.pushChatNotification(`New message from ${senderName}`);
          this.showBrowserNotification(senderName, message.messageContent);

          if (message.groupId && this.selectedChatType === 'group' && this.selectedGroupId === message.groupId) {
            this.markSelectedGroupAsSeen();
            this.refreshDerivedState();
          } else if (!message.groupId && this.selectedChatType === 'friend' && this.selectedFriendId === message.senderId) {
            this.markSelectedConversationAsRead();
          }
        }

        this.chatStatus = '';
        this.refreshDerivedState();
        this.safeDetectChanges();
      });
    });

    this.typingSubscription = this.signalR.getTypingReceived().subscribe((payload) => {
      this.ngZone.run(() => {
        this.typingFriendId = payload.senderId;
        this.typingUsername = payload.senderUsername;

        if (this.typingResetHandle) {
          window.clearTimeout(this.typingResetHandle);
        }

        this.typingResetHandle = window.setTimeout(() => {
          this.typingFriendId = null;
          this.typingUsername = '';
          this.safeDetectChanges();
        }, 1800);

        this.safeDetectChanges();
      });
    });

    this.messagesReadSubscription = this.signalR.getMessagesRead().subscribe((payload) => {
      this.ngZone.run(() => {
        const currentUserId = this.auth.getUserId();
        if (!currentUserId) {
          return;
        }

        this.messages = this.messages.map((message) =>
          message.senderId === currentUserId &&
          message.receiverId === payload.readerId
            ? { ...message, isRead: true, readAt: payload.readAt }
            : message
        );

        this.refreshDerivedState();
        this.safeDetectChanges();
      });
    });

    this.appNotificationSubscription = this.signalR.getAppNotifications().subscribe((notification) => {
      this.ngZone.run(() => {
        this.userNotifications = [notification, ...this.userNotifications];
        this.userState.setNotifications(this.userNotifications);
        const isEmergencyAlert = this.isEmergencyAlert(notification);
        this.pushChatNotification(notification.title || notification.message, isEmergencyAlert);

        if (isEmergencyAlert) {
          this.playEmergencyAlertSound();
          this.showBrowserNotification(notification.title || 'Emergency alert', notification.message || 'An admin sent an emergency alert.');
          this.toast.error(notification.message || 'Emergency alert from admin', 8000);
        }

        this.safeDetectChanges();
      });
    });
  }

  ngOnDestroy(): void {
    this.signalR.stopConnection();
    this.messageSubscription?.unsubscribe();
    this.typingSubscription?.unsubscribe();
    this.messagesReadSubscription?.unsubscribe();
    this.appNotificationSubscription?.unsubscribe();
    this.postsStoreSubscription?.unsubscribe();
    this.notificationsStoreSubscription?.unsubscribe();

    if (this.typingResetHandle) {
      window.clearTimeout(this.typingResetHandle);
    }

    this.revokePostImagePreview();
  }

  loadPosts(): void {
    this.api.getPosts(this.postSearchTerm).subscribe({
      next: (res) => {
        this.posts = this.normalizePosts(res);
        this.userState.setPosts(this.posts);
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading posts', err);
        this.toast.error(err?.error?.message ?? err?.error ?? 'Unable to load approved posts');
      }
    });
  }

  onPostSearchChange(): void {
    this.loadPosts();
  }

  loadCurrentUserProfile(): void {
    this.api.getCurrentUserProfile().subscribe({
      next: (res) => {
        this.currentUserProfile = this.normalizeCurrentUserProfile(res);
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading current user profile', err);
      }
    });
  }

  loadNotifications(): void {
    this.api.getMyNotifications().subscribe({
      next: (res) => {
        this.userNotifications = Array.isArray(res) ? [...res] : [];
        this.userState.setNotifications(this.userNotifications);
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading notifications', err);
      }
    });
  }

  loadAlerts(): void {
    this.api.getAlerts().subscribe({
      next: (res) => {
        this.alerts = Array.isArray(res) ? [...res] : [];
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading alerts', err);
      }
    });
  }

  createPost(): void {
    const content = `${this.newPost.content ?? ''}`.trim();
    if (!content) {
      this.toast.error('Please enter some text before posting');
      return;
    }

    if (!this.auth.getUserId()) {
      this.toast.error('User not logged in');
      return;
    }

    const formData = new FormData();
    formData.append('content', content);

    if (this.selectedPostImageFile) {
      formData.append('postImageFile', this.selectedPostImageFile);
    }

    this.api.createPost(formData).subscribe({
      next: () => {
        this.toast.success('Post submitted for admin approval');
        this.resetPostComposer();
        this.loadPosts();
      },
      error: (err: any) => {
        this.toast.error(err?.error?.message ?? err?.error ?? 'Error creating post');
      }
    });
  }

  onPostImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files.length > 0 ? input.files[0] : null;

    this.selectedPostImageFile = file;
    this.revokePostImagePreview();
    this.postImagePreviewUrl = file ? URL.createObjectURL(file) : null;
  }

  setActivePanel(panel: DashboardPanel): void {
    this.activePanel = panel;
    this.shareMenuPostId = null;
    this.shareSearchTerm = '';
  }

  loadMessages(): void {
    this.api.getMessages().subscribe({
      next: (res) => {
        this.messages = Array.isArray(res) ? [...res] : [];
        this.chatStatus = this.friends.length === 0 && this.groups.length === 0
          ? 'Add a friend or wait to be added to a group to start chatting.'
          : '';
        this.refreshDerivedState();
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading messages', err);
      }
    });
  }

  sendMessage(): void {
    const content = `${this.newMessageContent ?? ''}`.trim();
    if (!content) {
      this.toast.error('Please enter a message');
      return;
    }

    if (!this.auth.getUserId()) {
      this.toast.error('User not logged in');
      return;
    }

    const activeGroupId = this.selectedGroupId ?? this.selectedGroup?.groupId ?? null;
    const activeFriendId = this.selectedFriendId ?? this.selectedFriend?.userId ?? null;

    if (this.selectedChatType === 'group' && activeGroupId) {
      this.sendGroupMessage(activeGroupId, content);
      return;
    }

    if (this.selectedChatType === 'friend' && activeFriendId) {
      this.signalR.sendMessage(activeFriendId, content)
        .then(() => {
          this.newMessageContent = '';
          this.chatStatus = '';
        })
        .catch((error) => {
          this.toast.error(error?.message || 'Unable to send message right now');
        });
      return;
    }

    if (activeGroupId) {
      this.sendGroupMessage(activeGroupId, content);
      return;
    }

    if (activeFriendId) {
      this.signalR.sendMessage(activeFriendId, content)
        .then(() => {
          this.newMessageContent = '';
          this.chatStatus = '';
        })
        .catch((error) => {
          this.toast.error(error?.message || 'Unable to send message right now');
        });
      return;
    }

    this.toast.info('Please choose a friend or group first');
  }

  private sendGroupMessage(groupId: number, content: string): void {
    this.api.sendMessage({ groupId, messageContent: content }).subscribe({
      next: (message) => {
        if (!this.messages.some((existing) => existing.messageId === message.messageId)) {
          this.messages = [...this.messages, message];
        }
        this.newMessageContent = '';
        this.chatStatus = '';
        this.refreshDerivedState();
        this.safeDetectChanges();
      },
      error: (err) => {
        this.toast.error(err?.error?.message || err?.error || 'Unable to send group message right now');
      }
    });
  }

  handleMessageInput(): void {
    if (this.selectedChatType !== 'friend' || !this.selectedFriendId || !this.newMessageContent.trim()) {
      return;
    }

    const now = Date.now();
    if (now - this.lastTypingSentAt < 1200) {
      return;
    }

    this.lastTypingSentAt = now;
    this.signalR.sendTyping(this.selectedFriendId).catch(() => {
      // no-op
    });
  }

  loadUsers(): void {
    this.api.getUsers().subscribe({
      next: (res) => {
        this.users = Array.isArray(res)
          ? res.filter((user) => user?.role === 'User')
          : [];
        this.refreshDerivedState();
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading users', err);
      }
    });
  }

  markNotificationsRead(): void {
    this.api.markAllNotificationsRead().subscribe({
      next: () => {
        this.userNotifications = this.userNotifications.map((notification) => ({
          ...notification,
          isRead: true
        }));
        this.userState.setNotifications(this.userNotifications);
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error marking notifications as read', err);
      }
    });
  }

  loadFriends(): void {
    this.api.getFriends().subscribe({
      next: (res) => {
        this.friends = Array.isArray(res) ? [...res] : [];

        if (this.selectedChatType === 'friend' && this.friends.length > 0 && !this.selectedFriendId) {
          this.selectedFriendId = this.friends[0].userId;
        }

        if (this.selectedChatType === 'friend' && this.friends.length > 0 && !this.friends.some((friend) => friend.userId === this.selectedFriendId)) {
          this.selectedFriendId = this.friends[0].userId;
        }

        if (this.selectedChatType === 'friend' && this.friends.length === 0 && this.groups.length === 0) {
          this.selectedFriendId = null;
          this.chatStatus = 'Add a friend or wait to be added to a group to start chatting.';
        }

        if (this.selectedFriendId) {
          this.markSelectedConversationAsRead();
        }

        this.refreshDerivedState();
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading friends', err);
      }
    });
  }

  loadGroups(): void {
    this.api.getMyGroups().subscribe({
      next: (res) => {
        this.groups = Array.isArray(res) ? [...res] : [];

        if (this.selectedChatType === 'group' && this.groups.length > 0 && !this.groups.some((group) => group.groupId === this.selectedGroupId)) {
          this.selectedGroupId = this.groups[0].groupId;
        }

        if (!this.selectedFriendId && !this.selectedGroupId && this.friends.length === 0 && this.groups.length > 0) {
          this.selectedChatType = 'group';
          const firstGroupId = this.groups[0].groupId;
          this.selectedGroupId = firstGroupId;
          this.markSelectedGroupAsSeen(firstGroupId);
        }

        this.refreshDerivedState();
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading groups', err);
      }
    });
  }

  loadFriendRequests(): void {
    this.api.getMyFriendRequests().subscribe({
      next: (res: any) => {
        this.incomingRequests = Array.isArray(res?.incoming) ? res.incoming : [];
        this.outgoingRequests = Array.isArray(res?.outgoing) ? res.outgoing : [];
        this.refreshDerivedState();
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading friend requests', err);
      }
    });
  }

  sendFriendRequest(receiverId: number): void {
    this.api.sendFriendRequest(receiverId).subscribe({
      next: () => {
        this.toast.success('Friend request sent');
        const requestedUser = this.users.find((user) => user.userId === receiverId);
        this.outgoingRequests = [
          {
            friendRequestId: 0,
            receiverId,
            receiverUsername: requestedUser?.username ?? '',
            status: 'Pending',
            createdAt: new Date().toISOString(),
            respondedAt: null
          },
          ...this.outgoingRequests.filter((request) => request.receiverId !== receiverId)
        ];
        this.refreshDerivedState();
        this.safeDetectChanges();
        this.loadFriendRequests();
      },
      error: (err) => {
        this.toast.error(err.error?.message || err.error || 'Failed to send friend request');
      }
    });
  }

  respondToFriendRequest(requestId: number, action: 'accept' | 'reject'): void {
    this.api.respondToFriendRequest(requestId, action).subscribe({
      next: () => {
        this.toast.success(`Friend request ${action}ed`);

        if (action === 'accept') {
          const acceptedRequest = this.incomingRequests.find((request) => request.friendRequestId === requestId);
          if (acceptedRequest && !this.friends.some((friend) => friend.userId === acceptedRequest.senderId)) {
            this.friends = [
              ...this.friends,
              {
                userId: acceptedRequest.senderId,
                username: acceptedRequest.senderUsername
              }
            ];

            if (!this.selectedFriendId) {
              this.selectedFriendId = acceptedRequest.senderId;
            }
          }
        }

        this.loadFriendRequests();
        this.loadFriends();
        this.refreshDerivedState();
        this.safeDetectChanges();
      },
      error: (err) => {
        this.toast.error(err.error?.message || err.error || `Failed to ${action} request`);
      }
    });
  }

  canSendFriendRequest(userId: number): boolean {
    const alreadyFriend = this.friends.some((friend) => friend.userId === userId);
    const outgoingPending = this.outgoingRequests.some((req) => req.receiverId === userId && req.status === 'Pending');
    const incomingPending = this.incomingRequests.some((req) => req.senderId === userId && req.status === 'Pending');
    return !alreadyFriend && !outgoingPending && !incomingPending;
  }

  getVisibleUsers(): any[] {
    return this.visibleUsers;
  }

  isFriend(userId: number): boolean {
    return this.friends.some((friend) => friend.userId === userId);
  }

  hasOutgoingRequest(userId: number): boolean {
    return this.outgoingRequests.some((request) => request.receiverId === userId && request.status === 'Pending');
  }

  hasIncomingRequest(userId: number): boolean {
    return this.incomingRequests.some((request) => request.senderId === userId && request.status === 'Pending');
  }

  getFriendActionLabel(userId: number): string {
    if (this.isFriend(userId)) return 'Friend';
    if (this.hasOutgoingRequest(userId)) return 'Requested';
    if (this.hasIncomingRequest(userId)) return 'Respond in Requests';
    return 'Add Friend';
  }

  selectFriend(friendId: number): void {
    this.activePanel = 'messages';
    this.selectedChatType = 'friend';
    this.selectedFriendId = friendId;
    this.selectedGroupId = null;
    this.chatStatus = '';
    this.refreshDerivedState();
    this.markSelectedConversationAsRead();
    this.safeDetectChanges();
  }

  selectGroup(groupId: number): void {
    this.activePanel = 'messages';
    this.selectedChatType = 'group';
    this.selectedGroupId = groupId;
    this.selectedFriendId = null;
    this.chatStatus = '';
    this.markSelectedGroupAsSeen(groupId);
    this.refreshDerivedState();
    this.safeDetectChanges();
  }

  getSelectedFriend(): any | null {
    return this.selectedFriend;
  }

  getConversationMessages(): any[] {
    return this.conversationMessages;
  }

  getUnreadCount(friendId: number): number {
    return this.unreadCounts[friendId] ?? 0;
  }

  isOwnMessage(message: any): boolean {
    return message.senderId === this.auth.getUserId();
  }

  getFriendName(friendId: number): string {
    return this.friends.find((friend) => friend.userId === friendId)?.username ?? `User ${friendId}`;
  }

  getPostImageUrl(post: Post | any): string | null {
    const path = post?.postImage;
    if (!path) {
      return null;
    }

    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path;
    }

    return `http://localhost:5072${path}`;
  }

  getProfileImageUrl(path?: string | null): string | null {
    if (!path) {
      return null;
    }

    return path.startsWith('http://') || path.startsWith('https://')
      ? path
      : `${environment.apiBaseUrl}${path}`;
  }

  getAvatarInitial(value?: string | null): string {
    const normalized = `${value ?? ''}`.trim();
    return normalized ? normalized.charAt(0).toUpperCase() : '?';
  }

  getGroupName(groupId: number): string {
    return this.groups.find((group) => group.groupId === groupId)?.groupName ?? `Group ${groupId}`;
  }

  getSenderName(message: any): string {
    return this.users.find((user) => user.userId === message.senderId)?.username
      ?? this.friends.find((friend) => friend.userId === message.senderId)?.username
      ?? `User ${message.senderId}`;
  }

  getGroupsList(): any[] {
    return this.groups;
  }

  getGroupUnreadCount(groupId: number): number {
    return this.groupUnreadCounts[groupId] ?? 0;
  }

  getUnreadNotificationCount(): number {
    return this.userNotifications.filter((notification) => !notification.isRead).length;
  }

  getTotalUnreadMessages(): number {
    const friendUnread = Object.values(this.unreadCounts).reduce((sum, count) => sum + count, 0);
    const groupUnread = Object.values(this.groupUnreadCounts).reduce((sum, count) => sum + count, 0);
    return friendUnread + groupUnread;
  }

  getPendingFriendRequestCount(): number {
    return this.incomingRequests.filter((request) => request.status === 'Pending').length;
  }

  getFilteredFriends(): any[] {
    const term = this.messageSearchTerm.trim().toLowerCase();
    if (!term) {
      return this.friends;
    }

    return this.friends.filter((friend) =>
      `${friend?.username ?? ''}`.toLowerCase().includes(term)
    );
  }

  getFilteredUsers(): any[] {
    const term = this.userSearchTerm.trim().toLowerCase();
    if (!term) {
      return this.visibleUsers;
    }

    return this.visibleUsers.filter((user) =>
      `${user?.username ?? ''}`.toLowerCase().includes(term) ||
      `${user?.email ?? ''}`.toLowerCase().includes(term)
    );
  }

  getFilteredShareFriends(): any[] {
    const term = this.shareSearchTerm.trim().toLowerCase();
    if (!term) {
      return this.friends;
    }

    return this.friends.filter((friend) =>
      `${friend?.username ?? ''}`.toLowerCase().includes(term)
    );
  }

  togglePostLike(post: Post): void {
    this.api.togglePostLike(post.postId).subscribe({
      next: (res) => {
        this.updatePostInFeed(post.postId, (existingPost) => ({
          ...existingPost,
          likedByCurrentUser: !!res?.liked,
          likeCount: Number(res?.likeCount ?? existingPost.likeCount)
        }));
      },
      error: (err) => {
        this.toast.error(err?.error?.message ?? err?.error ?? 'Unable to update like right now');
      }
    });
  }

  isPostLiked(postId: number): boolean {
    return !!this.posts.find((post) => post.postId === postId)?.likedByCurrentUser;
  }

  toggleComments(postId: number): void {
    this.expandedCommentsPostId = this.expandedCommentsPostId === postId ? null : postId;
  }

  isCommentsOpen(postId: number): boolean {
    return this.expandedCommentsPostId === postId;
  }

  submitComment(post: Post): void {
    const commentText = `${this.commentDrafts[post.postId] ?? ''}`.trim();
    if (!commentText) {
      this.toast.error('Please enter a comment');
      return;
    }

    this.api.addPostComment(post.postId, commentText).subscribe({
      next: (comment) => {
        this.commentDrafts[post.postId] = '';
        this.expandedCommentsPostId = post.postId;
        this.updatePostInFeed(post.postId, (existingPost) => ({
          ...existingPost,
          commentCount: Number(comment?.commentCount ?? existingPost.commentCount + 1),
          comments: [
            ...existingPost.comments,
            {
              postCommentId: comment.postCommentId,
              postId: comment.postId,
              userId: comment.userId,
              username: comment.username,
              commentText: comment.commentText,
              createdAt: comment.createdAt
            }
          ]
        }));
        this.toast.success('Comment added');
      },
      error: (err) => {
        this.toast.error(err?.error?.message ?? err?.error ?? 'Unable to add comment right now');
      }
    });
  }

  toggleShareMenu(postId: number): void {
    if (this.friends.length === 0) {
      this.toast.info('Add at least one friend before sharing posts.');
      return;
    }

    this.shareMenuPostId = this.shareMenuPostId === postId ? null : postId;
    this.shareSearchTerm = '';
  }

  sharePostWithFriend(post: Post, friendId: number): void {
    const friend = this.friends.find((item) => item.userId === friendId);
    if (!friend) {
      this.toast.error('Friend not found');
      return;
    }

    this.api.sharePost(post.postId, friendId).subscribe({
      next: (res) => {
        this.shareMenuPostId = null;
        this.shareSearchTerm = '';
        this.updatePostInFeed(post.postId, (existingPost) => ({
          ...existingPost,
          shareCount: Number(res?.shareCount ?? existingPost.shareCount)
        }));
        this.toast.success(`Post shared with ${friend.username}`);
      },
      error: (err) => {
        this.toast.error(err?.error?.message ?? err?.error ?? 'Unable to share this post right now');
      }
    });
  }

  canSendCurrentMessage(): boolean {
    const hasContent = `${this.newMessageContent ?? ''}`.trim().length > 0;
    if (!hasContent) {
      return false;
    }

    return this.selectedChatType === 'group'
      ? !!(this.selectedGroupId ?? this.selectedGroup?.groupId)
      : !!(this.selectedFriendId ?? this.selectedFriend?.userId);
  }

  isTypingForSelectedFriend(): boolean {
    return this.selectedFriendId !== null && this.typingFriendId === this.selectedFriendId;
  }

  requestNotificationPermission(): void {
    if (typeof window === 'undefined' || !('Notification' in window)) {
      this.notificationPermission = 'unsupported';
      return;
    }

    if (Notification.permission === 'granted') {
      this.notificationPermission = 'granted';
      return;
    }

    Notification.requestPermission().then((permission) => {
      this.notificationPermission = permission;
      this.safeDetectChanges();
    });
  }

  getLastOwnMessageReadState(): string {
    return this.lastOwnMessageReadState;
  }

  reportUser(userId: number): void {
    const reason = prompt('Enter reason for report:')?.trim();
    if (!reason) {
      return;
    }

    const reportingUserId = this.auth.getUserId();
    if (!reportingUserId) {
      this.toast.error('User not logged in');
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

  private pushChatNotification(text: string, isEmergency = false): void {
    const notification = {
      id: Date.now() + Math.floor(Math.random() * 1000),
      text: isEmergency ? `Emergency alert: ${text}` : text
    };
    this.chatNotifications = [...this.chatNotifications, notification];

    window.setTimeout(() => {
      this.chatNotifications = this.chatNotifications.filter((item) => item.id !== notification.id);
      this.safeDetectChanges();
    }, 3500);
  }

  private markSelectedConversationAsRead(): void {
    if (!this.selectedFriendId || this.readConversationRequestInFlight) {
      return;
    }

    const currentUserId = this.auth.getUserId();
    if (!currentUserId) {
      return;
    }

    const hasUnreadMessages = this.messages.some((message) =>
      message.senderId === this.selectedFriendId &&
      message.receiverId === currentUserId &&
      !message.isRead
    );

    if (!hasUnreadMessages) {
      return;
    }

    const selectedFriendId = this.selectedFriendId;
    const readAt = new Date().toISOString();

    this.messages = this.messages.map((message) =>
      message.senderId === selectedFriendId &&
      message.receiverId === currentUserId
        ? { ...message, isRead: true, readAt }
        : message
    );
    this.refreshDerivedState();
    this.safeDetectChanges();

    this.readConversationRequestInFlight = true;

    this.api.markConversationAsRead(selectedFriendId).subscribe({
      next: () => {
        this.readConversationRequestInFlight = false;
        this.refreshDerivedState();
        this.safeDetectChanges();
      },
      error: () => {
        this.readConversationRequestInFlight = false;
      }
    });
  }

  private getNotificationPermission(): 'default' | 'denied' | 'granted' | 'unsupported' {
    if (typeof window === 'undefined' || !('Notification' in window)) {
      return 'unsupported';
    }

    return Notification.permission;
  }

  private showBrowserNotification(senderName: string, messageText: string): void {
    if (typeof document === 'undefined' || document.visibilityState === 'visible') {
      return;
    }

    if (typeof window === 'undefined' || !('Notification' in window)) {
      return;
    }

    if (Notification.permission !== 'granted') {
      return;
    }

    const notification = new Notification(senderName, {
      body: messageText,
      tag: `chat-${senderName}`
    });

    window.setTimeout(() => notification.close(), 5000);
  }

  private isEmergencyAlert(notification: any): boolean {
    return `${notification?.type ?? ''}`.toLowerCase() === 'alert';
  }

  private playEmergencyAlertSound(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const AudioContextCtor = window.AudioContext || (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!AudioContextCtor) {
      return;
    }

    const audioContext = new AudioContextCtor();
    const gainNode = audioContext.createGain();
    const firstTone = audioContext.createOscillator();
    const secondTone = audioContext.createOscillator();

    gainNode.gain.setValueAtTime(0.0001, audioContext.currentTime);
    gainNode.gain.exponentialRampToValueAtTime(0.14, audioContext.currentTime + 0.02);
    gainNode.gain.exponentialRampToValueAtTime(0.0001, audioContext.currentTime + 0.85);

    firstTone.type = 'triangle';
    firstTone.frequency.setValueAtTime(880, audioContext.currentTime);
    firstTone.frequency.setValueAtTime(660, audioContext.currentTime + 0.22);

    secondTone.type = 'square';
    secondTone.frequency.setValueAtTime(660, audioContext.currentTime + 0.24);
    secondTone.frequency.setValueAtTime(990, audioContext.currentTime + 0.42);

    firstTone.connect(gainNode);
    secondTone.connect(gainNode);
    gainNode.connect(audioContext.destination);

    firstTone.start(audioContext.currentTime);
    secondTone.start(audioContext.currentTime + 0.24);
    firstTone.stop(audioContext.currentTime + 0.45);
    secondTone.stop(audioContext.currentTime + 0.78);

    window.setTimeout(() => {
      void audioContext.close();
    }, 1000);
  }

  private normalizeCurrentUserProfile(source: any): any | null {
    if (!source) {
      return null;
    }

    return {
      userId: source.userId ?? source.UserId ?? null,
      username: `${source.username ?? source.Username ?? ''}`,
      email: `${source.email ?? source.Email ?? ''}`,
      mobileNumber: `${source.mobileNumber ?? source.MobileNumber ?? ''}`,
      role: `${source.role ?? source.Role ?? ''}`,
      profileImage: source.profileImage ?? source.ProfileImage ?? null
    };
  }

  private refreshDerivedState(): void {
    const currentUserId = this.auth.getUserId();

    this.visibleUsers = this.users.filter((user) => user.userId !== currentUserId && user.role === 'User');
    this.selectedFriend = this.friends.find((friend) => friend.userId === this.selectedFriendId) ?? null;
    this.selectedGroup = this.groups.find((group) => group.groupId === this.selectedGroupId) ?? null;

    if (!currentUserId) {
      this.conversationMessages = [];
      this.unreadCounts = {};
      this.groupUnreadCounts = {};
      this.lastOwnMessageReadState = '';
      return;
    }

    if (this.selectedChatType === 'group' && this.selectedGroupId) {
      this.conversationMessages = this.messages
        .filter((message) => message.groupId === this.selectedGroupId)
        .sort((a, b) => new Date(a.timeStamp).getTime() - new Date(b.timeStamp).getTime());
    } else if (this.selectedFriendId) {
      this.conversationMessages = this.messages
        .filter((message) =>
          !message.groupId &&
          ((message.senderId === currentUserId && message.receiverId === this.selectedFriendId) ||
          (message.senderId === this.selectedFriendId && message.receiverId === currentUserId)))
        .sort((a, b) => new Date(a.timeStamp).getTime() - new Date(b.timeStamp).getTime());
    } else {
      this.conversationMessages = [];
    }

    this.unreadCounts = this.friends.reduce((acc: Record<number, number>, friend) => {
      acc[friend.userId] = this.messages.filter((message) =>
        !message.groupId &&
        message.senderId === friend.userId &&
        message.receiverId === currentUserId &&
        !message.isRead
      ).length;
      return acc;
    }, {});

    this.groupUnreadCounts = this.groups.reduce((acc: Record<number, number>, group) => {
      const lastSeenAt = this.groupLastSeenAt[group.groupId];
      acc[group.groupId] = this.messages.filter((message) =>
        message.groupId === group.groupId &&
        message.senderId !== currentUserId &&
        (!lastSeenAt || new Date(message.timeStamp).getTime() > new Date(lastSeenAt).getTime())
      ).length;
      return acc;
    }, {});

    if (this.selectedChatType === 'friend') {
      const ownMessages = [...this.conversationMessages].reverse().find((message) => message.senderId === currentUserId);
      this.lastOwnMessageReadState = ownMessages ? (ownMessages.isRead ? 'Seen' : 'Delivered') : '';
    } else {
      this.lastOwnMessageReadState = '';
    }
  }

  private markSelectedGroupAsSeen(groupId?: number): void {
    const targetGroupId = groupId ?? this.selectedGroupId;
    const currentUserId = this.auth.getUserId();
    if (!targetGroupId || !currentUserId) {
      return;
    }

    const latestIncomingMessage = this.messages
      .filter((message) => message.groupId === targetGroupId && message.senderId !== currentUserId)
      .sort((a, b) => new Date(b.timeStamp).getTime() - new Date(a.timeStamp).getTime())[0];

    if (!latestIncomingMessage) {
      return;
    }

    this.groupLastSeenAt = {
      ...this.groupLastSeenAt,
      [targetGroupId]: latestIncomingMessage.timeStamp
    };
    this.writeGroupSeenState();
  }

  private readGroupSeenState(): Record<number, string> {
    if (typeof window === 'undefined') {
      return {};
    }

    try {
      const raw = window.sessionStorage.getItem('profilebook_group_seen_at');
      if (!raw) {
        return {};
      }

      const parsed = JSON.parse(raw);
      return parsed && typeof parsed === 'object' ? parsed : {};
    } catch {
      return {};
    }
  }

  private writeGroupSeenState(): void {
    if (typeof window === 'undefined') {
      return;
    }

    window.sessionStorage.setItem('profilebook_group_seen_at', JSON.stringify(this.groupLastSeenAt));
  }

  private resetPostComposer(): void {
    this.newPost = { content: '' };
    this.selectedPostImageFile = null;
    this.revokePostImagePreview();
    this.postImagePreviewUrl = null;

    if (this.postImageInput?.nativeElement) {
      this.postImageInput.nativeElement.value = '';
    }
  }

  private revokePostImagePreview(): void {
    if (this.postImagePreviewUrl?.startsWith('blob:')) {
      URL.revokeObjectURL(this.postImagePreviewUrl);
    }
  }

  private safeDetectChanges(): void {
    try {
      this.cdr.detectChanges();
    } catch {
      // no-op
    }
  }

  private updatePostInFeed(postId: number, updater: (post: Post) => Post): void {
    this.posts = this.posts.map((post) => post.postId === postId ? updater(post) : post);
    this.userState.setPosts(this.posts);
    this.safeDetectChanges();
  }

  private normalizePosts(posts: any): Post[] {
    if (!Array.isArray(posts)) {
      return [];
    }

    return posts.map((post) => ({
      ...post,
      likeCount: Number(post?.likeCount ?? 0),
      commentCount: Number(post?.commentCount ?? 0),
      shareCount: Number(post?.shareCount ?? 0),
      likedByCurrentUser: !!post?.likedByCurrentUser,
      comments: Array.isArray(post?.comments) ? post.comments : []
    }));
  }

  logout(): void {
    this.auth.logout();
  }
}
