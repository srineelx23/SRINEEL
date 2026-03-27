import { Component, Input, Output, EventEmitter, signal, inject } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { VimsFormatPipe } from '../../../utils/vims-format.pipe';

@Component({
  selector: 'app-claims',
  standalone: true,
  imports: [CommonModule, FormsModule, VimsFormatPipe],
  templateUrl: './claims.html',
  styleUrl: './claims.css'
})
export class ClaimsComponent {
  @Input({ required: true }) pendingClaims!: any[];
  @Input({ required: true }) sortedPendingClaims!: any[];
  @Input({ required: true }) selectedClaim!: any;
  @Input({ required: true }) claimsSortOption!: string;
  @Input({ required: true }) payoutLoading!: boolean;
  @Input({ required: true }) decisionForm!: any;
  @Input({ required: true }) payoutBreakdown!: any;
  @Input({ required: true }) payoutWarning!: string | null;
  @Input({ required: true }) detailsLoading!: boolean;
  
  @Output() onOpenClaimReview = new EventEmitter<any>();
  @Output() onCloseClaimReview = new EventEmitter<void>();
  @Output() onClaimsSortOptionChange = new EventEmitter<string>();
  @Output() onUpdateBreakdown = new EventEmitter<void>();
  @Output() onSubmitDecision = new EventEmitter<void>();
  @Output() onDownloadPolicyContract = new EventEmitter<number>();
  @Output() onDownloadSettlementReport = new EventEmitter<number>();

  private sanitizer = inject(DomSanitizer);

  showSortDropdown = signal(false);
  currentYear = new Date().getFullYear();

  openClaimReview(claim: any) {
    this.onOpenClaimReview.emit(claim);
  }

  closeClaimReview() {
    this.onCloseClaimReview.emit();
  }

  setSortOption(option: string) {
    this.onClaimsSortOptionChange.emit(option);
    this.showSortDropdown.set(false);
  }

  updateBreakdown() {
    this.onUpdateBreakdown.emit();
  }

  submitDecision() {
    this.onSubmitDecision.emit();
  }

  getSortLabel(option: string): string {
    switch (option) {
      case 'dateDesc': return 'Newest First';
      case 'dateAsc': return 'Oldest First';
      case 'amountDesc': return 'Amount: High to Low';
      case 'amountAsc': return 'Amount: Low to High';
      default: return 'Newest First';
    }
  }

  getEstimatedPayout(): number | null {
    return this.payoutBreakdown?.finalPayout ?? null;
  }

  getPayoutBreakdown(): any {
    return this.payoutBreakdown;
  }

  getPayoutWarning(): string | null {
    return this.payoutWarning;
  }

  formatSummary(summary: string): SafeHtml {
    if (!summary) return '';
    
    // 1. Remove intro if it exists (extra safety). Keep the bold markers for Incident Details.
    // This finds the first occurrence of "Incident Details" and removes everything before it, 
    // but checks if there were bold markers right before it.
    let index = summary.indexOf('Incident Details');
    let cleaned = summary;
    if (index > 0) {
      // Check if there are ** before "Incident Details"
      const preceeding = summary.substring(Math.max(0, index - 2), index);
      if (preceeding === '**') {
        cleaned = summary.substring(index - 2);
      } else {
        cleaned = summary.substring(index);
      }
    }
    
    // 2. Wrap bold text **word** -> <strong>word</strong> - use global with multi-line
    let html = cleaned.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
    
    // 3. Handle bullet points and format
    let lines = html.split('\n');
    let finalHtmlList: string[] = [];
    
    lines.forEach(line => {
      let l = line.trim();
      if (!l) return;
      
      // Clean up stray markdown bullets (* or -) but don't break our HTML
      if (l.startsWith('*') || l.startsWith('-')) {
        l = l.substring(1).trim();
      }
      
      // Safety: remove any remaining single or double asterisks that weren't caught
      l = l.replace(/\*/g, '');

      finalHtmlList.push(`<div class="summary-bullet" style="margin-bottom: 8px;">${l}</div>`);
    });

    return this.sanitizer.bypassSecurityTrustHtml(finalHtmlList.join(''));
  }

  getOriginalInvoice(): string | null {
    // Standardizing on lowercase 'vehicle' as it comes from JSON
    const docs = this.selectedClaim?.vehicle?.vehicleApplication?.documents;
    if (!docs || !Array.isArray(docs)) return null;
    
    // Case-insensitive search for 'Invoice'
    const inv = docs.find((d: any) => 
      d.documentType?.toLowerCase() === 'invoice' || 
      d.documentType?.toLowerCase()?.includes('invoice')
    );
    return inv ? inv.filePath : null;
  }
}
