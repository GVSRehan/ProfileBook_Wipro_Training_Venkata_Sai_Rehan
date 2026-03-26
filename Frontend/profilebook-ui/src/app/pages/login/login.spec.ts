import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LoginComponent } from './login';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            login: () => of({ token: 'token', role: 'User', isMainAdmin: false }),
            saveToken: () => undefined,
            saveSessionMeta: () => undefined
          }
        },
        {
          provide: ToastService,
          useValue: {
            success: () => undefined,
            error: () => undefined
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
