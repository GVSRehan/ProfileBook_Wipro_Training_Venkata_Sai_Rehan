import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: HubConnection | null = null;
  private messageReceived = new Subject<any>();
  private typingReceived = new Subject<any>();
  private messagesRead = new Subject<any>();
  private appNotifications = new Subject<any>();
  private connectionReady: Promise<void> | null = null;

  constructor(private auth: AuthService) {}

  startConnection(): Promise<void> {
    const token = this.auth.getToken();
    if (!token) {
      return Promise.reject(new Error('Missing auth token'));
    }

    if (this.hubConnection?.state === HubConnectionState.Connected) {
      return Promise.resolve();
    }

    if (this.connectionReady) {
      return this.connectionReady;
    }

    if (!this.hubConnection) {
      this.hubConnection = new HubConnectionBuilder()
        .withUrl(`${environment.apiBaseUrl}/chatHub`, {
          accessTokenFactory: () => this.auth.getToken() ?? ''
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Information)
        .build();

      this.hubConnection.on('ReceiveMessage', (message) => {
        this.messageReceived.next(message);
      });

      this.hubConnection.on('ReceiveTyping', (payload) => {
        this.typingReceived.next(payload);
      });

      this.hubConnection.on('MessagesRead', (payload) => {
        this.messagesRead.next(payload);
      });

      this.hubConnection.on('ReceiveAppNotification', (payload) => {
        this.appNotifications.next(payload);
      });

      this.hubConnection.onclose(() => {
        this.connectionReady = null;
      });
    }

    this.connectionReady = this.hubConnection
      .start()
      .then(() => {
        console.log('SignalR connection started');
      })
      .catch(err => {
        this.connectionReady = null;
        console.log('Error while starting connection: ' + err);
        throw err;
      });

    return this.connectionReady;
  }

  stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop();
      this.hubConnection = null;
    }
    this.connectionReady = null;
  }

  async sendMessage(receiverId: number, message: string): Promise<void> {
    await this.startConnection();

    if (!this.hubConnection || this.hubConnection.state !== HubConnectionState.Connected) {
      throw new Error('Chat connection is not ready');
    }

    return this.hubConnection.invoke('SendMessage', receiverId, message);
  }

  async sendTyping(receiverId: number): Promise<void> {
    await this.startConnection();

    if (!this.hubConnection || this.hubConnection.state !== HubConnectionState.Connected) {
      throw new Error('Chat connection is not ready');
    }

    return this.hubConnection.invoke('SendTyping', receiverId);
  }

  async sendGroupMessage(groupId: number, message: string): Promise<void> {
    await this.startConnection();

    if (!this.hubConnection || this.hubConnection.state !== HubConnectionState.Connected) {
      throw new Error('Chat connection is not ready');
    }

    return this.hubConnection.invoke('SendGroupMessage', groupId, message);
  }

  getMessageReceived(): Observable<any> {
    return this.messageReceived.asObservable();
  }

  getTypingReceived(): Observable<any> {
    return this.typingReceived.asObservable();
  }

  getMessagesRead(): Observable<any> {
    return this.messagesRead.asObservable();
  }

  getAppNotifications(): Observable<any> {
    return this.appNotifications.asObservable();
  }
}
