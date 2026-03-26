import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { UserDashboardComponent } from './user-dashboard';
import { AuthService } from '../../services/auth.service';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { UserStateService } from '../../services/user-state.service';
import { ToastService } from '../../shared/toast.service';

describe('UserDashboardComponent', () => {
  let component: UserDashboardComponent;
  let fixture: ComponentFixture<UserDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserDashboardComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            getUserId: () => 1,
            logout: () => undefined
          }
        },
        {
          provide: ApiService,
          useValue: {
            getPosts: () => of([]),
            getCurrentUserProfile: () => of({ username: 'User', profileImage: null }),
            getMyNotifications: () => of([]),
            getMessages: () => of([]),
            getUsers: () => of([]),
            getFriends: () => of([]),
            getMyGroups: () => of([]),
            getMyFriendRequests: () => of({ incoming: [], outgoing: [] }),
            getAlerts: () => of([])
          }
        },
        {
          provide: SignalRService,
          useValue: {
            startConnection: () => Promise.resolve(),
            stopConnection: () => undefined,
            getMessageReceived: () => of(),
            getTypingReceived: () => of(),
            getMessagesRead: () => of(),
            getAppNotifications: () => of()
          }
        },
        {
          provide: UserStateService,
          useValue: {
            posts$: of([]),
            notifications$: of([]),
            setPosts: () => undefined,
            setNotifications: () => undefined
          }
        },
        {
          provide: ToastService,
          useValue: {
            success: () => undefined,
            error: () => undefined,
            info: () => undefined
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UserDashboardComponent);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
