import { Component, input, model } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-audit-logs',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './audit-logs.html',
  styleUrl: './audit-logs.css',
})
export class AuditLogsComponent {
  sortedAuditLogs = input.required<any[]>();
  auditSortOption = model<string>('dateDesc');
  isAuditSortOpen = model<boolean>(false);

  getSortLabel(option: string): string {
    switch (option) {
      case 'dateDesc': return 'Newest First';
      case 'dateAsc': return 'Oldest First';
      default: return 'Newest First';
    }
  }
}
