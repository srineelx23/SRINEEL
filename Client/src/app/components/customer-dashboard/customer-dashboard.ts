import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CustomerService } from '../../services/customer.service';
import { AuthService } from '../../services/auth.service';
import { jwtDecode } from 'jwt-decode';

@Component({
  selector: 'app-customer-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './customer-dashboard.html',
  styleUrl: './customer-dashboard.css',
})
export class CustomerDashboard implements OnInit {
  activeTab = signal('overview');

  // Data stores
  customerName = signal('Customer');
  policies = signal<any[]>([]);
  claims = signal<any[]>([]);
  applications = signal<any[]>([]);
  payments = signal<any[]>([]);
  plans = signal<any[]>([]); // needed for renewing policies
  showUserDropdown = signal(false);

  // Categorization
  policyFilter = signal('Active');

  activePolicies = computed(() =>
    this.policies().filter(p => p.status === 'Active')
  );

  pendingPolicies = computed(() =>
    this.policies().filter(p => !['Active', 'Cancelled', 'Expired', 'PendingPayment'].includes(p.status))
  );

  pendingPaymentPolicies = computed(() =>
    this.policies().filter(p => p.status === 'PendingPayment')
  );

  pendingApplicationsCount = computed(() =>
    this.applications().filter(a => a.status === 'UnderReview' || a.status === 0).length
  );

  inactivePolicies = computed(() =>
    this.policies().filter(p => p.status === 'Cancelled' || p.status === 'Expired')
  );

  selectedPolicy = signal<any>(null);

  // Claims State
  approvedClaims = computed(() => this.claims().filter(c => c.status === 'Approved' || c.status === 1));
  rejectedClaims = computed(() => this.claims().filter(c => c.status === 'Rejected' || c.status === 2));
  pendingClaimsList = computed(() => this.claims().filter(c => c.status === 'Submitted' || c.status === 'UnderReview' || c.status === 0));
  // Payments & Claims
  premiumPayments = computed(() =>
    this.payments().filter((p: any) =>
      !p.transactionReference ||
      (!p.transactionReference.toLowerCase().includes('claim') && !p.transactionReference.toLowerCase().includes('transfer'))
    )
  );

  claimPayments = computed(() =>
    this.payments().filter((p: any) => p.transactionReference && p.transactionReference.toLowerCase().includes('claim'))
  );

  transferPayments = computed(() =>
    this.payments().filter((p: any) => p.transactionReference && p.transactionReference.toLowerCase().includes('transfer'))
  );

  totalPremiumPaid = computed(() =>
    this.premiumPayments()
      .filter((p: any) => p.status === 'Paid' || p.status === 1)
      .reduce((sum: number, p: any) => sum + (Math.abs(p.amount) || 0), 0)
  );

  totalClaimPayouts = computed(() =>
    this.claimPayments()
      .filter((p: any) => p.status === 'Paid' || p.status === 1)
      .reduce((sum: number, p: any) => sum + (Math.abs(p.amount) || 0), 0)
  );

  // Unique vehicles derived from policies — for the overview vehicles section
  myVehicles = computed(() => {
    const map = new Map<string, any>();
    this.policies().forEach((p: any) => {
      const reg = p.vehicleRegistrationNumber || p.registrationNumber;
      if (reg && !map.has(reg)) {
        map.set(reg, {
          reg,
          vehicleName: p.vehicleMake ? `${p.vehicleMake} ${p.vehicleModel}` : (p.vehicleName || reg),
          idv: p.idv || p.invoiceAmount || 0,
          planName: p.planName || 'Standard',
          status: p.status
        });
      }
    });
    return Array.from(map.values());
  });

  selectedClaim = signal<any>(null);
  isFilingClaim = signal(false);
  claimForm = {
    PolicyId: null,
    ClaimType: 'Damage'
  };
  claimDoc1: File | null = null;
  claimDoc2: File | null = null;

  // Renew State
  renewingPolicyId = signal<number | null>(null);
  renewForm = {
    NewPlanId: null,
    SelectedYears: 1
  };

  errorMessage = signal('');
  successMessage = signal('');

  // Transfer state
  incomingTransfers = signal<any[]>([]);
  outgoingTransfers = signal<any[]>([]);
  showTransferModal = signal(false);
  showAcceptModal = signal(false);
  transferPolicyId = signal<number | null>(null);
  transferRecipientEmail = signal('');
  transferError = signal('');
  transferSuccess = signal('');
  pendingAcceptTransfer = signal<any>(null);
  rcFile: File | null = null;

  private customerService = inject(CustomerService);
  private authService = inject(AuthService);
  private router = inject(Router);

  ngOnInit() {
    this.extractName();
    this.loadOverviewData();
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
          'Customer';
        this.customerName.set(name);
      } catch (error) {
        console.error('Failed to parse token for name', error);
      }
    }
  }

  switchTab(tab: string) {
    this.activeTab.set(tab);
    this.renewingPolicyId.set(null); // Reset renew state on tab switch
    if (tab === 'overview') this.loadOverviewData();
    if (tab === 'policies') {
      this.loadPolicies();
      this.policyFilter.set('Active'); // Default to active on tab switch
      this.selectedPolicy.set(null);
    }
    if (tab === 'claims') this.loadClaims();
    if (tab === 'applied') this.loadApplications();
    if (tab === 'payments') this.loadPayments();
    if (tab === 'transfers') { this.loadIncomingTransfers(); this.loadOutgoingTransfers(); }
  }

  loadOverviewData() {
    this.loadPolicies();
    this.loadClaims();
    this.loadApplications();
    this.loadPayments();
    this.loadIncomingTransfers();
    this.loadOutgoingTransfers();
  }

  loadPayments() {
    this.customerService.getMyPayments().subscribe({
      next: (res) => this.payments.set(res),
      error: (err) => console.error(err)
    });
  }

  loadPolicies() {
    this.customerService.getMyPolicies().subscribe({
      next: (res) => this.policies.set(res),
      error: (err) => console.error(err)
    });
  }

  loadClaims() {
    this.customerService.getMyClaims().subscribe({
      next: (res) => this.claims.set(res),
      error: (err) => console.error(err)
    });
  }

  loadApplications() {
    this.customerService.getMyApplications().subscribe({
      next: (res) => this.applications.set(res),
      error: (err) => console.error(err)
    });
  }

  // --- Claims ---
  startClaim() {
    this.isFilingClaim.set(true);
  }

  onClaimFileChange(event: any, field: 'doc1' | 'doc2') {
    const file = event.target.files[0];
    if (field === 'doc1') this.claimDoc1 = file;
    if (field === 'doc2') this.claimDoc2 = file;
  }

  submitClaim() {
    this.errorMessage.set('');
    this.successMessage.set('');

    if (!this.claimForm.PolicyId || !this.claimForm.ClaimType) {
      this.errorMessage.set("Please fill in policy and claim type.");
      this.autoHideToast();
      return;
    }

    const formData = new FormData();
    formData.append('PolicyId', String(this.claimForm.PolicyId));
    formData.append('ClaimType', this.claimForm.ClaimType);

    if (this.claimDoc1) formData.append('Document1', this.claimDoc1);
    if (this.claimDoc2) formData.append('Document2', this.claimDoc2);

    this.customerService.submitClaim(formData).subscribe({
      next: (res) => {
        this.successMessage.set("Claim submitted successfully!");
        this.isFilingClaim.set(false);
        this.loadClaims();
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || (typeof err.error === 'string' ? err.error : "Submit claim failed."));
        this.autoHideToast();
      }
    });
  }

  viewClaimDetails(claim: any) {
    this.selectedClaim.set(claim);
  }

  closeClaimDetails() {
    this.selectedClaim.set(null);
  }

  // --- Policy Actions ---
  viewPolicyDetails(policyId: number) {
    this.customerService.getPolicy(policyId).subscribe({
      next: (res) => this.selectedPolicy.set(res),
      error: (err) => console.error(err)
    });
  }

  closePolicyDetails() {
    this.selectedPolicy.set(null);
  }

  payPremium(policyId: number) {
    this.customerService.payAnnualPremium(policyId).subscribe({
      next: () => {
        this.successMessage.set("Premium paid successfully!");
        this.loadPolicies();
        if (this.selectedPolicy()?.policyId === policyId) {
          this.viewPolicyDetails(policyId);
        }
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || "Payment failed.");
        this.autoHideToast();
      }
    });
  }

  cancelPolicy(policyId: number) {
    if (confirm("Are you sure you want to cancel this policy?")) {
      this.customerService.cancelPolicy(policyId).subscribe({
        next: () => {
          this.successMessage.set("Policy cancelled.");
          this.loadPolicies();
          if (this.selectedPolicy()?.policyId === policyId) {
            this.viewPolicyDetails(policyId);
          }
          this.renewingPolicyId.set(null);
          setTimeout(() => this.successMessage.set(''), 3000);
        },
        error: (err) => {
          this.errorMessage.set(err.error?.message || "Cancellation failed.");
          this.autoHideToast();
        }
      });
    }
  }

  startRenew(policyId: number) {
    if (!this.plans() || this.plans().length === 0) {
      this.customerService.getAllPolicyPlans().subscribe(res => this.plans.set(res));
    }
    this.renewingPolicyId.set(policyId);
  }

  submitRenew() {
    if (!this.renewForm.NewPlanId) return;

    const payload = {
      NewPlanId: Number(this.renewForm.NewPlanId),
      SelectedYears: Number(this.renewForm.SelectedYears)
    };

    this.customerService.renewPolicy(this.renewingPolicyId()!, payload).subscribe({
      next: () => {
        this.successMessage.set("Policy renewed!");
        const pId = this.renewingPolicyId();
        this.renewingPolicyId.set(null);
        this.loadPolicies();
        if (pId && this.selectedPolicy()?.policyId === pId) {
          this.viewPolicyDetails(pId);
        }
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || (typeof err.error === 'string' ? err.error : "Renewal failed."));
        this.autoHideToast();
      }
    });
  }

  private autoHideToast() {
    setTimeout(() => {
      this.errorMessage.set('');
    }, 5000);
  }

  goHome() {
    this.router.navigate(['/']);
  }

  logout() {
    this.authService.logout();
  }

  // ===================== POLICY TRANSFER =====================

  openTransferModal(policyId: number) {
    this.transferPolicyId.set(policyId);
    this.transferRecipientEmail.set('');
    this.transferError.set('');
    this.transferSuccess.set('');
    this.showTransferModal.set(true);
  }

  closeTransferModal() {
    this.showTransferModal.set(false);
    this.transferPolicyId.set(null);
  }

  initiateTransfer() {
    const email = this.transferRecipientEmail().trim();
    const policyId = this.transferPolicyId();
    if (!email || !policyId) return;

    this.transferError.set('');
    this.customerService.initiateTransfer(policyId, email).subscribe({
      next: () => {
        this.transferSuccess.set('Transfer request sent successfully.');
        this.loadOutgoingTransfers();
        setTimeout(() => this.closeTransferModal(), 2000);
      },
      error: (err) => {
        if (err.status === 404)
          this.transferError.set('No customer found with that email address.');
        else
          this.transferError.set(err.error?.message || 'Transfer failed. Try again.');
      }
    });
  }

  loadIncomingTransfers() {
    this.customerService.getIncomingTransfers().subscribe({
      next: (res) => this.incomingTransfers.set(res),
      error: () => { }
    });
  }

  loadOutgoingTransfers() {
    this.customerService.getOutgoingTransfers().subscribe({
      next: (res) => this.outgoingTransfers.set(res),
      error: () => { }
    });
  }

  openAcceptModal(transfer: any) {
    this.pendingAcceptTransfer.set(transfer);
    this.rcFile = null;
    this.showAcceptModal.set(true);
  }

  closeAcceptModal() {
    this.showAcceptModal.set(false);
    this.pendingAcceptTransfer.set(null);
    this.rcFile = null;
  }

  onRcFileChange(event: any) {
    this.rcFile = event.target.files[0] || null;
  }

  acceptTransfer() {
    const transfer = this.pendingAcceptTransfer();
    if (!transfer || !this.rcFile) {
      this.errorMessage.set('Please upload your RC document.');
      this.autoHideToast();
      return;
    }

    this.customerService.acceptTransfer(transfer.policyTransferId, this.rcFile).subscribe({
      next: () => {
        this.successMessage.set('Transfer accepted! The application is now pending agent approval.');
        this.closeAcceptModal();
        this.loadIncomingTransfers();
        this.loadPolicies();
        setTimeout(() => this.successMessage.set(''), 5000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Accept failed.');
        this.autoHideToast();
      }
    });
  }

  rejectTransfer(transferId: number) {
    if (!confirm('Are you sure you want to reject this policy transfer?')) return;

    this.customerService.rejectTransfer(transferId).subscribe({
      next: () => {
        this.successMessage.set('Transfer request rejected.');
        this.loadIncomingTransfers();
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.message || 'Reject failed.');
        this.autoHideToast();
      }
    });
  }
}
