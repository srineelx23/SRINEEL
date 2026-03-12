import { Component, Input, Output, EventEmitter, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-applications',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.css',
})
export class ApplicationsComponent {
  @Input() applications!: Signal<any[]>;
  @Input() sortedApplications!: Signal<any[]>;
  @Input() showApplicationsSortDropdown!: Signal<boolean>;
  @Input() applicationsSortOption!: Signal<string>;

  @Output() onToggleSortDropdown = new EventEmitter<boolean>();
  @Output() onSetSortOption = new EventEmitter<string>();

  getSortLabel(option: string): string {
    switch (option) {
      case 'dateDesc': return 'Latest Applied';
      case 'dateAsc': return 'Oldest Applied';
      default: return 'Latest Applied';
    }
  }
}
