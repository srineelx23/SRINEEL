import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-applications',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './applications.html',
  styleUrl: './applications.css'
})
export class Applications {
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

  getSortLabel(option: string): string {
    switch (option) {
      case 'dateDesc': return 'Newest First';
      case 'dateAsc': return 'Oldest First';
      default: return 'Newest First';
    }
  }
}
