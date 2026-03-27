import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ClaimsOfficerService } from '../../services/claims-officer.service';
import { extractErrorMessage } from '../../utils/error-handler';
import { jwtDecode } from 'jwt-decode';

import { NavbarComponent } from './navbar/navbar';
import { OverviewComponent } from './overview/overview';
import { ClaimsComponent } from './claims/claims';
import { HistoryComponent } from './history/history';
import { SettingsComponent } from './settings/settings';
import { NotificationsComponent } from '../notifications/notifications';

@Component({
    selector: 'app-claims-officer-dashboard',
    standalone: true,
    imports: [CommonModule, FormsModule, NavbarComponent, OverviewComponent, ClaimsComponent, HistoryComponent, SettingsComponent, NotificationsComponent],

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
    officerName = signal('Officer');
    userRole = signal('VIMS Officer');
    pendingClaims = signal<any[]>([]);
    reviewedClaims = signal<any[]>([]);

    // Sorting State
    claimsSortOption = signal('dateDesc');

    // Computed Totals
    totalPending = computed(() => this.pendingClaims().length);
    totalReviewed = computed(() => this.reviewedClaims().length);

    sortedPendingClaims = computed(() => {
        const data = [...this.pendingClaims()];
        const option = this.claimsSortOption();
        return data.sort((a, b) => {
            const dateA = new Date(a.createdAt || 0).getTime();
            const dateB = new Date(b.createdAt || 0).getTime();
            if (option === 'dateDesc') return dateB - dateA;
            if (option === 'dateAsc') return dateA - dateB;
            return 0;
        });
    });

    sortedReviewedClaims = computed(() => {
        const data = [...this.reviewedClaims()];
        const option = this.claimsSortOption();
        return data.sort((a, b) => {
            const dateA = new Date(a.createdAt || 0).getTime();
            const dateB = new Date(b.createdAt || 0).getTime();
            if (option === 'dateDesc') return dateB - dateA;
            if (option === 'dateAsc') return dateA - dateB;
            if (option === 'amountDesc') return (b.approvedAmount || 0) - (a.approvedAmount || 0);
            if (option === 'amountAsc') return (a.approvedAmount || 0) - (b.approvedAmount || 0);
            return 0;
        });
    });

    // UI State
    selectedClaim = signal<any>(null);
    showUserDropdown = signal(false);
    showSortDropdown = signal(false);

    // DTO logic for form
    decisionForm = {
        approved: true,
        repairCost: null as number | null,
        engineCost: null as number | null,
        invoiceAmount: null as number | null,
        manufactureYear: null as number | null,
        rejectionReason: ''
    };

    payoutBreakdown = signal<any>(null);
    payoutLoading = signal(false);
    detailsLoading = signal(false);


    // Settings - Change Password
    changePasswordForm = {
        currentPassword: '',
        newPassword: '',
        confirmPassword: ''
    };
    changePwdLoading = signal(false);
    showCurrentPwd = false;
    showNewPwd = false;
    showConfirmPwd = false;

    errorMessage = signal('');
    successMessage = signal('');

    ngOnInit() {
        this.extractName();
        this.loadDashboardData();
        this.syncChatbotContext();
    }

    private extractName() {
        const token = sessionStorage.getItem('token');
        if (token) {
            try {
                const decodedToken: any = jwtDecode(token);
                const name =
                    decodedToken['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] ||
                    decodedToken.name ||
                    decodedToken.Name ||
                    'Officer';
                this.officerName.set(name);

                const role = this.authService.getRoleFromStoredToken();
                if (role === 'ClaimsOfficer') this.userRole.set('VIMS Officer');
                else if (role === 'Admin') this.userRole.set('Executive Admin');
                else this.userRole.set(role || 'VIMS Officer');
            } catch (error) {
                console.error('Failed to parse token for name', error);
            }
        }
    }

    loadDashboardData() {
        this.claimsService.getMyAssignedClaims().subscribe({
            next: (res) => {
                // Controller now returns explicit camelCase strings
                const mapped = res.map((c: any) => ({
                    ...c,
                    status: this.mapStatus(c.status),
                    createdAt: c.createdAt,
                    claimType: c.claimType
                }));

                // Filter out pending vs history
                this.pendingClaims.set(mapped.filter((c: any) => c.status === 'Submitted'));
                this.reviewedClaims.set(mapped.filter((c: any) => c.status === 'Approved' || c.status === 'Rejected'));
            },
            error: (err) => console.error('Error loading claims:', err)
        });
    }

    mapStatus(status: any): string {
        const s = status?.toString();
        if (s === '0' || s === 'Submitted' || s === 'UnderReview') return 'Submitted';
        if (s === '1' || s === 'Approved') return 'Approved';
        if (s === '2' || s === 'Rejected') return 'Rejected';
        return status;
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

    // Navigation
    switchTab(tabId: string) {
        this.activeTab.set(tabId);
        this.selectedClaim.set(null);
        this.syncChatbotContext();
    }

    private syncChatbotContext() {
        localStorage.setItem('vims.claimsOfficer.activeTab', this.activeTab());
        window.dispatchEvent(new CustomEvent('vims-chatbot-context-changed'));
    }

    goHome() {
        this.router.navigate(['/']);
    }

    logout() {
        this.authService.logout();
    }

    downloadPolicyContract(policyId: number) {
        this.claimsService.downloadPolicyContract(policyId).subscribe({
            next: (blob) => {
                const url = window.URL.createObjectURL(blob);
                window.open(url, '_blank');
                this.successMessage.set("Policy contract opened in new tab.");
                setTimeout(() => this.successMessage.set(''), 3000);
            },
            error: (err) => {
                console.error("Download failed:", err);
                this.errorMessage.set("Failed to download policy contract.");
                this.autoHideToast();
            }
        });
    }

    downloadSettlementReport(claimId: number) {
        this.claimsService.downloadSettlementReport(claimId).subscribe({
            next: (blob) => {
                const url = window.URL.createObjectURL(blob);
                window.open(url, '_blank');
                this.successMessage.set("Settlement report opened in new tab.");
                setTimeout(() => this.successMessage.set(''), 3000);
            },
            error: (err) => {
                console.error("Download failed:", err);
                this.errorMessage.set("Failed to download settlement report.");
                this.autoHideToast();
            }
        });
    }

    // Claim Review
    openClaimReview(claim: any) {
        // Open the panel immediately with the data we already have from the table
        this.selectedClaim.set({ ...claim, fraudRiskScore: null, summary: null, riskReasons: [] });
        this.detailsLoading.set(true);
        
        // Reset assessment defaults
        this.decisionForm.approved = true;
        this.decisionForm.repairCost = null;
        this.decisionForm.engineCost = null;
        this.decisionForm.invoiceAmount = null;
        this.decisionForm.manufactureYear = null;
        this.decisionForm.rejectionReason = '';

        // Fetch full details (including AI analysis) asynchronously
        this.claimsService.getClaimDetails(claim.claimId).subscribe({
            next: (fullClaim) => {
                this.selectedClaim.set(fullClaim);
                this.detailsLoading.set(false);
                
                // Prefill suggested repair cost if found by AI
                if (fullClaim.suggestedRepairCost) {
                    this.decisionForm.repairCost = fullClaim.suggestedRepairCost;
                }

                // Prefill suggested manufacture year if found by AI
                if (fullClaim.suggestedManufactureYear) {
                    this.decisionForm.manufactureYear = fullClaim.suggestedManufactureYear;
                }

                // AI Safeguard: Default to Reject if risk is high (>= 80%)
                if (fullClaim.fraudRiskScore >= 80) {
                    this.decisionForm.approved = false;
                } else {
                    this.decisionForm.approved = true;
                }
                
                this.updateBreakdown();
            },
            error: (err) => {
                console.error("Error fetching claim details:", err);
                this.detailsLoading.set(false);
            }
        });
    }

    updateBreakdown() {
        const claim = this.selectedClaim();
        if (!claim || !this.decisionForm.approved) {
            this.payoutBreakdown.set(null);
            return;
        }

        this.payoutLoading.set(true);
        this.claimsService.getPayoutBreakdown(claim.claimId, this.decisionForm).subscribe({
            next: (res) => {
                this.payoutBreakdown.set(res);
                this.payoutLoading.set(false);
            },
            error: (err) => {
                console.error("Breakdown error:", err);
                this.payoutBreakdown.set(null);
                this.payoutLoading.set(false);
            }
        });
    }

    getEstimatedPayout(): number | null {
        return this.payoutBreakdown()?.finalPayout ?? null;
    }

    getPayoutBreakdown(): any {
        return this.payoutBreakdown();
    }

    getPayoutWarning(): string | null {
        const bd = this.payoutBreakdown();
        if (bd && bd.warningMessage) {
            return bd.warningMessage;
        }

        const claim = this.selectedClaim();
        const currentYear = new Date().getFullYear();
        const tpYear = this.decisionForm.manufactureYear;
        
        if (claim?.claimType === 'ThirdParty' && tpYear !== null && (tpYear > currentYear || tpYear < 1900)) {
            return 'Please enter a valid manufacturing year.';
        }

        return null;
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
                const currentYear = new Date().getFullYear();
                
                if (!this.decisionForm.repairCost || !this.decisionForm.manufactureYear) {
                    this.errorMessage.set('Repair Cost and Manufacture Year are strictly required for Third Party claims.');
                    this.autoHideToast();
                    return;
                }
                
                if (this.decisionForm.manufactureYear > currentYear || this.decisionForm.manufactureYear < 1900) {
                    this.errorMessage.set('Please enter a valid manufacturing year.');
                    this.autoHideToast();
                    return;
                }
            }
        } else {
            if (!this.decisionForm.rejectionReason || this.decisionForm.rejectionReason.trim().length === 0) {
                this.errorMessage.set('A reason is mandatory when rejecting a claim.');
                this.autoHideToast();
                return;
            }
        }

        const payload = {
            repairCost: this.decisionForm.repairCost,
            engineCost: cType === 'ThirdParty' ? null : this.decisionForm.engineCost,
            invoiceAmount: this.decisionForm.invoiceAmount,
            manufactureYear: this.decisionForm.manufactureYear,
            rejectionReason: this.decisionForm.rejectionReason
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

    changePasswordWithData(data: any) {
        this.changePasswordForm = data;
        this.changePassword();
    }

    private autoHideToast() {
        setTimeout(() => {
            this.errorMessage.set('');
        }, 4000);
    }

    changePassword() {
        const { currentPassword, newPassword, confirmPassword } = this.changePasswordForm;

        if (!currentPassword || !newPassword || !confirmPassword) {
            this.errorMessage.set('All password fields are required.');
            this.autoHideToast();
            return;
        }
        if (newPassword.length < 6) {
            this.errorMessage.set('New password must be at least 6 characters.');
            this.autoHideToast();
            return;
        }
        if (newPassword !== confirmPassword) {
            this.errorMessage.set('New password and confirm password do not match.');
            this.autoHideToast();
            return;
        }

        this.changePwdLoading.set(true);
        this.authService.changePassword({ currentPassword, newPassword }).subscribe({
            next: () => {
                this.successMessage.set('Password changed successfully!');
                this.changePasswordForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
                this.changePwdLoading.set(false);
                setTimeout(() => this.successMessage.set(''), 4000);
            },
            error: (err: any) => {
                this.changePwdLoading.set(false);
                this.errorMessage.set(err.error || 'Failed to change password');
                this.autoHideToast();
            }
        });
    }
}
