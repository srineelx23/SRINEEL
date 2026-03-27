import { Component, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AgentService } from '../../../services/agent.service';

@Component({
  selector: 'app-applications',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './applications.html',
  styleUrl: './applications.css'
})
export class ApplicationsComponent {
  private agentService = inject(AgentService);

  pendingApps = input.required<any[]>();
  sortedPendingApps = input.required<any[]>();
  selectedApp = input.required<any>();
  appsSortOption = input.required<string>();
  showAppsSortDropdown = signal(false);
  reviewAction = {
    approved: true,
    rejectionReason: '',
    invoiceAmount: null as number | null
  };

  onOpenAppReview = output<any>();
  onCloseAppReview = output<void>();
  onAppsSortOptionChange = output<string>();
  onSubmitReview = output<any>();

  validationLoading = signal(false);
  validationRiskScore = signal(0);
  validationErrors = signal<string[]>([]);

  constructor() {
    effect(() => {
      const app = this.selectedApp();
      if (!app || !app.vehicleApplicationId) {
        this.validationLoading.set(false);
        this.validationRiskScore.set(0);
        this.validationErrors.set([]);
        return;
      }

      this.loadValidation(app.vehicleApplicationId);
    });
  }

  openAppReview(app: any) {
    this.onOpenAppReview.emit(app);
  }

  closeAppReview() {
    this.onCloseAppReview.emit();
  }

  setAppsSortOption(option: string) {
    this.onAppsSortOptionChange.emit(option);
    this.showAppsSortDropdown.set(false);
  }

  submitReview() {
    this.onSubmitReview.emit(this.reviewAction);
  }

  private loadValidation(applicationId: number) {
    this.validationLoading.set(true);
    this.validationRiskScore.set(0);
    this.validationErrors.set([]);

    this.agentService.validateApplicationDocuments(applicationId).subscribe({
      next: (res) => {
        this.validationRiskScore.set(Number(res?.riskScore || 0));
        this.validationErrors.set(Array.isArray(res?.errors) ? res.errors : []);
        this.validationLoading.set(false);
      },
      error: () => {
        this.validationErrors.set(['Unable to load document validations right now.']);
        this.validationRiskScore.set(0);
        this.validationLoading.set(false);
      }
    });
  }

  getSortLabel(option: string): string {
    switch (option) {
      case 'dateDesc': return 'Newest First';
      case 'dateAsc': return 'Oldest First';
      default: return 'Newest First';
    }
  }

  getRiskTone(): 'low' | 'medium' | 'high' {
    const score = this.validationRiskScore();
    if (score >= 70) return 'high';
    if (score >= 35) return 'medium';
    return 'low';
  }

  getValidationSummary(): string {
    const issues = this.validationErrors().length;
    if (issues === 0) {
      return 'No mismatches detected';
    }

    return `${issues} validation issue${issues > 1 ? 's' : ''} found`;
  }
}
