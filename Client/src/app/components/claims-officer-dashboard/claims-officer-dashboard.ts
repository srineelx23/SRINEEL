import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ClaimsOfficerService } from '../../services/claims-officer.service';
import { extractErrorMessage } from '../../utils/error-handler';
import { jwtDecode } from 'jwt-decode';

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
    officerName = signal('Officer');
    userRole = signal('Claims Officer');
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

    // DTO logic for form
    decisionForm = {
        approved: true,
        repairCost: null as number | null,
        engineCost: null as number | null,
        invoiceAmount: null as number | null,
        manufactureYear: null as number | null,
        rejectionReason: ''
    };

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
                if (role === 'ClaimsOfficer') this.userRole.set('Claims Officer');
                else if (role === 'Admin') this.userRole.set('Executive Admin');
                else this.userRole.set(role || 'Claims Officer');
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
        this.decisionForm.rejectionReason = '';
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
        } else {
            if (!this.decisionForm.rejectionReason || this.decisionForm.rejectionReason.trim().length === 0) {
                this.errorMessage.set('A reason is mandatory when rejecting a claim.');
                this.autoHideToast();
                return;
            }
        }

        const payload = {
            repairCost: this.decisionForm.repairCost,
            engineCost: this.decisionForm.engineCost,
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
