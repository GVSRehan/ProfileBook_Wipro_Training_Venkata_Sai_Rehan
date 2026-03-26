import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AdminDashboardComponent } from './admin-dashboard';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';
import { AdminStateService } from '../../services/admin-state.service';
import { SignalRService } from '../../services/signalr.service';

describe('AdminDashboardComponent', () => {
  let component: AdminDashboardComponent;
  let fixture: ComponentFixture<AdminDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [
        provideRouter([]),
        {
          provide: ApiService,
          useValue: {
            getDashboardStats: () => of({ totalUsers: 0, pendingPosts: 0, reports: 0 }),
            getDurationOptions: () => of([]),
            getExpiringCredentials: () => of([]),
            getAdminProfile: () => of({ username: 'Admin', email: 'admin@example.com', role: 'Admin', isMainAdmin: true, isActive: true })
          }
        },
        {
          provide: AuthService,
          useValue: {
            isMainAdmin: () => true,
            getUserId: () => 1,
            logout: () => undefined
          }
        },
        {
          provide: ToastService,
          useValue: {
            success: () => undefined,
            error: () => undefined
          }
        },
        {
          provide: AdminStateService,
          useValue: {
            dashboardStats$: of({ totalUsers: 0, pendingPosts: 0, reports: 0 }),
            expiringAdmins$: of([]),
            setDashboardStats: () => undefined,
            setExpiringAdmins: () => undefined
          }
        },
        {
          provide: SignalRService,
          useValue: {
            startConnection: () => Promise.resolve(),
            getAppNotifications: () => of()
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
