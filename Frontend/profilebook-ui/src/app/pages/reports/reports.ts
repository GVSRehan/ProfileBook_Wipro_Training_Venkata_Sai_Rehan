import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../shared/toast.service';
import { AdminStateService } from '../../services/admin-state.service';
import { finalize, take } from 'rxjs';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './reports.html',
  styleUrl: './reports.css',
})
export class ReportsComponent implements OnInit {
  reports: any[] = [];
  actionByReportId: Record<number, 'dismiss' | 'warn' | 'deactivate'> = {};
  notesByReportId: Record<number, string> = {};
  isLoading = false;
  errorMessage = '';

  constructor(
    private api: ApiService,
    private router: Router,
    private toast: ToastService,
    private adminState: AdminStateService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    this.loadReports();
  }

  loadReports() {
    this.isLoading = true;
    this.errorMessage = '';
    this.reports = [];

    this.api.getReports()
      .pipe(
        take(1),
        finalize(() => {
          this.isLoading = false;
          this.safeDetectChanges();
        })
      )
      .subscribe({
        next: (res) => {
          this.reports = Array.isArray(res) ? res : [];
          this.adminState.setReports(this.reports);
          this.errorMessage = '';
          this.safeDetectChanges();
        },
        error: (err) => {
          console.error('Error loading reports', err);
          this.errorMessage = extractServerMessage(err) ?? 'Failed to load reports';
          this.toast.error(this.errorMessage);
          this.safeDetectChanges();
        }
      });
  }

  takeAction(report: any) {
    const action = this.actionByReportId[report.reportId] ?? 'warn';
    const adminNotes = this.notesByReportId[report.reportId]?.trim() || undefined;

    this.api.takeReportAction(report.reportId, { action, adminNotes }).subscribe({
      next: (res: any) => {
        this.toast.success(res?.message ?? 'Report action saved');
        this.loadReports();
      },
      error: (err: any) => {
        this.toast.error(err?.error?.message ?? 'Failed to update report');
      }
    });
  }

  backToDashboard() {
    this.router.navigate(['/admin/dashboard']);
  }

  private safeDetectChanges(): void {
    try {
      this.cdr.detectChanges();
    } catch {
      // no-op
    }
  }
}

function extractServerMessage(err: any): string | null {
  if (!err) {
    return null;
  }

  if (typeof err.error === 'string' && err.error.trim()) {
    return err.error.trim();
  }

  if (typeof err.error?.message === 'string' && err.error.message.trim()) {
    return err.error.message.trim();
  }

  if (typeof err.message === 'string' && err.message.trim()) {
    return err.message.trim();
  }

  return null;
}
