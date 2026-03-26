import { ChangeDetectorRef, Component, NgZone, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { ToastService } from '../../shared/toast.service';
import { environment } from '../../../environments/environment';

type ConversationType = 'friend' | 'group';

interface ConversationItem {
  type: ConversationType;
  id: number;
  name: string;
  profileImage: string | null;
  preview: string;
  lastMessageAt: string | null;
  unreadCount: number;
}

@Component({
  selector: 'app-user-messages',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './user-messages.html',
  styleUrl: './user-messages.css'
})
export class UserMessagesComponent implements OnInit, OnDestroy {
  searchTerm = '';
  currentUserProfile: any | null = null;
  messages: any[] = [];
  users: any[] = [];
  friends: any[] = [];
  groups: any[] = [];
  allConversationItems: ConversationItem[] = [];
  conversationMessages: any[] = [];
  newMessageContent = '';
  selectedChatType: ConversationType = 'friend';
  selectedFriendId: number | null = null;
  selectedGroupId: number | null = null;
  selectedConversation: ConversationItem | null = null;
  unreadCounts: Record<number, number> = {};
  groupUnreadCounts: Record<number, number> = {};
  groupLastSeenAt: Record<number, string> = {};
  lastOwnMessageReadState = '';
  chatStatus = 'Loading conversations...';
  typingFriendId: number | null = null;
  typingUsername = '';

  private typingResetHandle: number | null = null;
  private lastTypingSentAt = 0;
  private readConversationRequestInFlight = false;
  private messageSubscription: Subscription | null = null;
  private typingSubscription: Subscription | null = null;
  private messagesReadSubscription: Subscription | null = null;

  constructor(
    private auth: AuthService,
    private api: ApiService,
    private signalR: SignalRService,
    private toast: ToastService,
    private cdr: ChangeDetectorRef,
    private ngZone: NgZone
  ) {}

  ngOnInit(): void {
    this.groupLastSeenAt = this.readGroupSeenState();
    this.loadCurrentUserProfile();
    this.loadUsers();
    this.loadFriends();
    this.loadGroups();
    this.loadMessages();

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
          this.toast.info(`New message from ${senderName}`);

          if (message.groupId && this.selectedChatType === 'group' && this.selectedGroupId === message.groupId) {
            this.markSelectedGroupAsSeen();
          } else if (!message.groupId && this.selectedChatType === 'friend' && this.selectedFriendId === message.senderId) {
            this.markSelectedConversationAsRead();
          }
        }

        this.refreshConversationState();
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

        this.refreshConversationState();
        this.safeDetectChanges();
      });
    });
  }

  ngOnDestroy(): void {
    this.signalR.stopConnection();
    this.messageSubscription?.unsubscribe();
    this.typingSubscription?.unsubscribe();
    this.messagesReadSubscription?.unsubscribe();

    if (this.typingResetHandle) {
      window.clearTimeout(this.typingResetHandle);
    }
  }

  loadUsers(): void {
    this.api.getUsers().subscribe({
      next: (res) => {
        this.users = Array.isArray(res) ? res : [];
        this.refreshConversationState();
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading users', err);
      }
    });
  }

  loadCurrentUserProfile(): void {
    this.api.getCurrentUserProfile().subscribe({
      next: (res) => {
        this.currentUserProfile = res ?? null;
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading current user profile', err);
      }
    });
  }

  loadFriends(): void {
    this.api.getFriends().subscribe({
      next: (res) => {
        this.friends = Array.isArray(res) ? res : [];
        this.refreshConversationState();
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
        this.groups = Array.isArray(res) ? res : [];
        this.refreshConversationState();
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading groups', err);
      }
    });
  }

  loadMessages(): void {
    this.api.getMessages().subscribe({
      next: (res) => {
        this.messages = Array.isArray(res) ? res : [];
        this.refreshConversationState();
        this.safeDetectChanges();
      },
      error: (err) => {
        console.error('Error loading messages', err);
      }
    });
  }

  getVisibleConversationItems(): ConversationItem[] {
    const term = this.searchTerm.trim().toLowerCase();
    if (!term) {
      return this.allConversationItems;
    }

    return this.allConversationItems.filter((item) =>
      item.name.toLowerCase().includes(term)
    );
  }

  selectConversation(item: ConversationItem): void {
    if (item.type === 'group') {
      this.selectGroup(item.id);
      return;
    }

    this.selectFriend(item.id);
  }

  selectFriend(friendId: number): void {
    this.selectedChatType = 'friend';
    this.selectedFriendId = friendId;
    this.selectedGroupId = null;
    this.chatStatus = '';
    this.refreshConversationState();
    this.markSelectedConversationAsRead();
    this.safeDetectChanges();
  }

  selectGroup(groupId: number): void {
    this.selectedChatType = 'group';
    this.selectedGroupId = groupId;
    this.selectedFriendId = null;
    this.chatStatus = '';
    this.markSelectedGroupAsSeen(groupId);
    this.refreshConversationState();
    this.safeDetectChanges();
  }

  isConversationSelected(item: ConversationItem): boolean {
    return item.type === this.selectedChatType &&
      (item.type === 'group' ? this.selectedGroupId === item.id : this.selectedFriendId === item.id);
  }

  sendMessage(): void {
    const content = `${this.newMessageContent ?? ''}`.trim();
    if (!content) {
      this.toast.error('Please enter a message');
      return;
    }

    if (this.selectedChatType === 'group' && this.selectedGroupId) {
      this.api.sendMessage({ groupId: this.selectedGroupId, messageContent: content }).subscribe({
        next: (message) => {
          if (!this.messages.some((existing) => existing.messageId === message.messageId)) {
            this.messages = [...this.messages, message];
          }
          this.newMessageContent = '';
          this.refreshConversationState();
          this.safeDetectChanges();
        },
        error: (err) => {
          this.toast.error(err?.error?.message ?? err?.error ?? 'Unable to send group message right now');
        }
      });
      return;
    }

    if (this.selectedChatType === 'friend' && this.selectedFriendId) {
      this.signalR.sendMessage(this.selectedFriendId, content)
        .then(() => {
          this.newMessageContent = '';
        })
        .catch((error) => {
          this.toast.error(error?.message || 'Unable to send message right now');
        });
      return;
    }

    this.toast.info('Please choose a conversation first');
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

  canSendCurrentMessage(): boolean {
    const hasContent = `${this.newMessageContent ?? ''}`.trim().length > 0;
    if (!hasContent) {
      return false;
    }

    return this.selectedChatType === 'group'
      ? this.selectedGroupId !== null
      : this.selectedFriendId !== null;
  }

  isOwnMessage(message: any): boolean {
    return message.senderId === this.auth.getUserId();
  }

  getSenderName(message: any): string {
    return this.users.find((user) => user.userId === message.senderId)?.username
      ?? this.friends.find((friend) => friend.userId === message.senderId)?.username
      ?? `User ${message.senderId}`;
  }

  getSenderProfileImageUrl(message: any): string | null {
    if (this.isOwnMessage(message)) {
      return this.getProfileImageUrl(this.currentUserProfile?.profileImage);
    }

    const senderProfileImage = this.users.find((user) => user.userId === message.senderId)?.profileImage
      ?? this.friends.find((friend) => friend.userId === message.senderId)?.profileImage
      ?? null;

    return this.getProfileImageUrl(senderProfileImage);
  }

  getSelectedConversationName(): string {
    if (this.selectedConversation) {
      return this.selectedConversation.name;
    }

    return 'Select a conversation';
  }

  getSelectedConversationImageUrl(): string | null {
    if (!this.selectedConversation || this.selectedConversation.type === 'group') {
      return null;
    }

    return this.getProfileImageUrl(this.selectedConversation.profileImage);
  }

  getConversationImageUrl(item: ConversationItem): string | null {
    return item.type === 'friend'
      ? this.getProfileImageUrl(item.profileImage)
      : null;
  }

  getAvatarInitial(value?: string | null): string {
    const normalized = `${value ?? ''}`.trim();
    return normalized ? normalized.charAt(0).toUpperCase() : '?';
  }

  getLastOwnMessageReadState(): string {
    return this.lastOwnMessageReadState;
  }

  isTypingForSelectedFriend(): boolean {
    return this.selectedChatType === 'friend' &&
      this.selectedFriendId !== null &&
      this.typingFriendId === this.selectedFriendId;
  }

  private refreshConversationState(): void {
    const currentUserId = this.auth.getUserId();
    if (!currentUserId) {
      this.allConversationItems = [];
      this.conversationMessages = [];
      this.selectedConversation = null;
      this.unreadCounts = {};
      this.groupUnreadCounts = {};
      this.lastOwnMessageReadState = '';
      this.chatStatus = 'User not logged in.';
      return;
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

    const friendItems = this.friends.map((friend) => {
      const relatedMessages = this.messages.filter((message) =>
        !message.groupId &&
        ((message.senderId === currentUserId && message.receiverId === friend.userId) ||
        (message.senderId === friend.userId && message.receiverId === currentUserId))
      );
      const lastMessage = this.getLatestMessage(relatedMessages);

      return {
        type: 'friend' as const,
        id: friend.userId,
        name: friend.username,
        profileImage: friend.profileImage ?? null,
        preview: this.getConversationPreview(lastMessage, 'friend'),
        lastMessageAt: lastMessage?.timeStamp ?? null,
        unreadCount: this.unreadCounts[friend.userId] ?? 0
      };
    });

    const groupItems = this.groups.map((group) => {
      const relatedMessages = this.messages.filter((message) => message.groupId === group.groupId);
      const lastMessage = this.getLatestMessage(relatedMessages);

      return {
        type: 'group' as const,
        id: group.groupId,
        name: group.groupName,
        profileImage: null,
        preview: this.getConversationPreview(lastMessage, 'group'),
        lastMessageAt: lastMessage?.timeStamp ?? null,
        unreadCount: this.groupUnreadCounts[group.groupId] ?? 0
      };
    });

    this.allConversationItems = [...friendItems, ...groupItems].sort((a, b) => {
      if (a.lastMessageAt && b.lastMessageAt) {
        return new Date(b.lastMessageAt).getTime() - new Date(a.lastMessageAt).getTime();
      }

      if (a.lastMessageAt) return -1;
      if (b.lastMessageAt) return 1;
      return a.name.localeCompare(b.name);
    });

    const activeConversationExists = this.allConversationItems.some((item) =>
      item.type === this.selectedChatType &&
      (item.type === 'group' ? item.id === this.selectedGroupId : item.id === this.selectedFriendId)
    );

    if (!activeConversationExists && this.allConversationItems.length > 0) {
      const first = this.allConversationItems[0];
      this.selectedChatType = first.type;
      this.selectedFriendId = first.type === 'friend' ? first.id : null;
      this.selectedGroupId = first.type === 'group' ? first.id : null;
      if (first.type === 'group') {
        this.markSelectedGroupAsSeen(first.id);
      } else {
        this.markSelectedConversationAsRead();
      }
    }

    this.selectedConversation = this.allConversationItems.find((item) =>
      item.type === this.selectedChatType &&
      (item.type === 'group' ? item.id === this.selectedGroupId : item.id === this.selectedFriendId)
    ) ?? null;

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

    if (this.selectedChatType === 'friend') {
      const ownMessages = [...this.conversationMessages].reverse().find((message) => message.senderId === currentUserId);
      this.lastOwnMessageReadState = ownMessages ? (ownMessages.isRead ? 'Seen' : 'Delivered') : '';
    } else {
      this.lastOwnMessageReadState = '';
    }

    this.chatStatus = this.allConversationItems.length === 0
      ? 'No conversations yet. Add a friend or wait to be added to a group.'
      : '';
  }

  private getLatestMessage(messages: any[]): any | null {
    if (messages.length === 0) {
      return null;
    }

    return [...messages].sort((a, b) => new Date(b.timeStamp).getTime() - new Date(a.timeStamp).getTime())[0];
  }

  private getConversationPreview(message: any | null, type: ConversationType): string {
    if (!message) {
      return type === 'group' ? 'No group messages yet.' : 'No messages yet.';
    }

    const author = this.isOwnMessage(message) ? 'You' : this.getSenderName(message);
    const prefix = type === 'group' ? `${author}: ` : '';
    const fullText = `${prefix}${message.messageContent ?? ''}`.trim();
    return fullText.length > 60 ? `${fullText.slice(0, 57)}...` : fullText;
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
    this.refreshConversationState();
    this.safeDetectChanges();

    this.readConversationRequestInFlight = true;

    this.api.markConversationAsRead(selectedFriendId).subscribe({
      next: () => {
        this.readConversationRequestInFlight = false;
        this.refreshConversationState();
        this.safeDetectChanges();
      },
      error: () => {
        this.readConversationRequestInFlight = false;
      }
    });
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

  private getProfileImageUrl(path?: string | null): string | null {
    if (!path) {
      return null;
    }

    return path.startsWith('http://') || path.startsWith('https://')
      ? path
      : `${environment.apiBaseUrl}${path}`;
  }

  private safeDetectChanges(): void {
    try {
      this.cdr.detectChanges();
    } catch {
      // no-op
    }
  }
}
