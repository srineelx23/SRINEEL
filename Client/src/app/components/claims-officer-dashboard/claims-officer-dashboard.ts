import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ClaimsOfficerService } from '../../services/claims-officer.service';

@Component({
    selector: 'app-claims-officer-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule],
    templateUrl: './claims-officer-dashboard.html',
    styleUrl: './claims-officer-dashboard.css',
})
export class ClaimsOfficerDashboard implements OnInit {
    activeTab = signal('overview');

    // Services
    private authService = inject(AuthService);
    private claimsService = inject(ClaimsOfficerService);
    private router = inject(Router);

    // Data
    pendingClaims = signal<any[]>([]);
    reviewedClaims = signal<any[]>([]);

    // Computed Totals
    totalPending = computed(() => this.pendingClaims().length);
    totalReviewed = computed(() => this.reviewedClaims().length);

    // UI State
    selectedClaim = signal<any>(null);

    // DTO logic for form
    decisionForm = {
        approved: true,
        repairCost: null as number | null,
        engineCost: null as number | null,
        invoiceAmount: null as number | null,
        manufactureYear: null as number | null
    };

    errorMessage = signal('');
    successMessage = signal('');

    ngOnInit() {
        this.loadDashboardData();
    }

    loadDashboardData() {
        this.claimsService.getMyAssignedClaims().subscribe({
            next: (res) => {
                // Map Status Enums to Strings just like Agent Dashboard if received as ints/mapped string
                const mapped = res.map((c: any) => ({ ...c, status: this.mapStatus(c.status) }));

                // Filter out pending vs history
                this.pendingClaims.set(mapped.filter((c: any) => c.status === 'Submitted' || c.status === 'UnderReview' || c.status === '0'));
                this.reviewedClaims.set(mapped.filter((c: any) => c.status === 'Approved' || c.status === 'Rejected' || c.status === '1' || c.status === '2'));
            },
            error: (err) => console.error('Error loading claims:', err)
        });
    }

    mapStatus(status: any): string {
        if (status === 0 || status === 'Submitted' || status === 'UnderReview') return 'Submitted';
        if (status === 1 || status === 'Approved') return 'Approved';
        if (status === 2 || status === 'Rejected') return 'Rejected';
        return status;
    }

    // Navigation
    switchTab(tabId: string) {
        this.activeTab.set(tabId);
        this.selectedClaim.set(null);
    }

    goHome() {
        this.router.navigate(['/']);
    }

    logout() {
        this.authService.logout();
    }

    // Claim Review
    openClaimReview(claim: any) {
        this.selectedClaim.set(claim);
        // Reset defaults
        this.decisionForm.approved = true;
        this.decisionForm.repairCost = null;
        this.decisionForm.engineCost = null;
        this.decisionForm.invoiceAmount = null;
        this.decisionForm.manufactureYear = null;
    }

    closeClaimReview() {
        this.selectedClaim.set(null);
    }

    submitDecision() {
        this.errorMessage.set('');

        // Type Validation based on ClaimType
        const cType = this.selectedClaim().claimType;
        if (this.decisionForm.approved) {
            if (cType === 'Damage') {
                if (!this.decisionForm.repairCost) {
                    this.errorMessage.set('Repair cost is required for Damage claims.');
                    this.autoHideToast();
                    return;
                }
            } else if (cType === 'ThirdParty') {
                if (!this.decisionForm.repairCost || !this.decisionForm.invoiceAmount || !this.decisionForm.manufactureYear) {
                    this.errorMessage.set('Repair Cost, Invoice Amount, and Manufacture Year are strictly required for Third Party claims.');
                    this.autoHideToast();
                    return;
                }
            }
        }

        const payload = {
            repairCost: this.decisionForm.repairCost,
            engineCost: this.decisionForm.engineCost,
            invoiceAmount: this.decisionForm.invoiceAmount,
            manufactureYear: this.decisionForm.manufactureYear
        };

        const claimId = this.selectedClaim().claimId;

        this.claimsService.decideClaim(claimId, payload, this.decisionForm.approved).subscribe({
            next: () => {
                this.successMessage.set(`Claim has been ${this.decisionForm.approved ? 'Approved' : 'Rejected'} successfully!`);
                this.closeClaimReview();
                this.loadDashboardData();
                setTimeout(() => this.successMessage.set(''), 3000);
            },
            error: (err) => {
                this.errorMessage.set(err.error?.message || err.error || 'Failed to submit decision.');
                this.autoHideToast();
            }
        });
    }

    private autoHideToast() {
        setTimeout(() => {
            this.errorMessage.set('');
        }, 4000);
    }
}
