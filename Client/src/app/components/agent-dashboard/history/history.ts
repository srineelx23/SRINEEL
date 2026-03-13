import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './history.html',
  styleUrl: './history.css'
})
export class HistoryComponent {
  reviewedApps = input.required<any[]>();
  sortedReviewedApps = input.required<any[]>();
  selectedApp = input.required<any>();
  appsSortOption = input.required<string>();
  showAppsSortDropdown = signal(false);

  onOpenAppReview = output<any>();
  onCloseAppReview = output<void>();
  onAppsSortOptionChange = output<string>();

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

  getSortLabel(option: string): string {
    switch (option) {
      case 'dateDesc': return 'Newest First';
      case 'dateAsc': return 'Oldest First';
      default: return 'Newest First';
    }
  }
}
