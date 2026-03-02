import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CustomerService } from '../../services/customer.service';
import { AuthService } from '../../services/auth.service';

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
  policies = signal<any[]>([]);
  claims = signal<any[]>([]);
  applications = signal<any[]>([]);
  payments = signal<any[]>([]);
  plans = signal<any[]>([]); // needed for renewing policies

  // Categorization
  policyFilter = signal('Active');

  activePolicies = computed(() =>
    this.policies().filter(p => p.status === 'Active')
  );

  pendingPolicies = computed(() =>
    this.policies().filter(p => !['Active', 'Cancelled', 'Expired'].includes(p.status))
  );

  inactivePolicies = computed(() =>
    this.policies().filter(p => p.status === 'Cancelled' || p.status === 'Expired')
  );

  selectedPolicy = signal<any>(null);

  // Claims State
  approvedClaims = computed(() => this.claims().filter(c => c.status === 'Approved' || c.status === 1));
  rejectedClaims = computed(() => this.claims().filter(c => c.status === 'Rejected' || c.status === 2));
  pendingClaimsList = computed(() => this.claims().filter(c => c.status === 'Submitted' || c.status === 'UnderReview' || c.status === 0));

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

  private customerService = inject(CustomerService);
  private authService = inject(AuthService);
  private router = inject(Router);

  ngOnInit() {
    this.loadOverviewData();
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
  }

  loadOverviewData() {
    this.loadPolicies();
    this.loadClaims();
    this.loadApplications();
    this.loadPayments();
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
}
