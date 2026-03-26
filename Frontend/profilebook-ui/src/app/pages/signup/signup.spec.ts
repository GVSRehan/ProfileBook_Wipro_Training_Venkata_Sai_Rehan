import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { SignupComponent } from './signup';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../shared/toast.service';

describe('SignupComponent', () => {
  let component: SignupComponent;
  let fixture: ComponentFixture<SignupComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignupComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            register: () => of({})
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

    fixture = TestBed.createComponent(SignupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
