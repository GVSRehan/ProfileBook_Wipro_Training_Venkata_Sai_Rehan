import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReviewPosts } from './review-posts';

describe('ReviewPosts', () => {
  let component: ReviewPosts;
  let fixture: ComponentFixture<ReviewPosts>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReviewPosts],
    }).compileComponents();

    fixture = TestBed.createComponent(ReviewPosts);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
