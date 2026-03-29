import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CustomerService } from '../../services/customer.service';
import { AuthService } from '../../services/auth.service';
import { OverviewComponent } from './overview/overview.component';
import { PoliciesComponent } from './policies/policies.component';
import { ClaimsComponent } from './claims/claims.component';
import { PaymentsComponent } from './payments/payments.component';
import { TransfersComponent } from './transfers/transfers.component';
import { NavbarComponent } from './navbar/navbar.component';
import { ApplicationsComponent } from './applications/applications.component';
import { SettingsComponent } from './settings/settings.component';
import { jwtDecode } from 'jwt-decode';
import { extractErrorMessage } from '../../utils/error-handler';
import { InitiateTransferModalComponent } from './modals/initiate-transfer-modal.component';
import { AcceptTransferModalComponent } from './modals/accept-transfer-modal.component';
import {
  CreateOrderResponse,
  RazorpayService,
  RazorpayVerifyPayload
} from '../../services/razorpay.service';

import { NotificationsComponent } from '../notifications/notifications';

@Component({
  selector: 'app-customer-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, OverviewComponent, PoliciesComponent, ClaimsComponent, PaymentsComponent, TransfersComponent, SettingsComponent, ApplicationsComponent, NavbarComponent, InitiateTransferModalComponent, AcceptTransferModalComponent, NotificationsComponent],

  templateUrl: './customer-dashboard.html',
  styleUrl: './customer-dashboard.css',
})
export class CustomerDashboard implements OnInit {
  activeTab = signal('overview');

  // Data stores
  customerName = signal('Customer');
  referralCode = signal('');
  walletBalance = signal(0);
  referralHistory = signal<any[]>([]);
  userRole = signal('Customer');
  policies = signal<any[]>([]);
  claims = signal<any[]>([]);
  applications = signal<any[]>([]);
  payments = signal<any[]>([]);
  plans = signal<any[]>([]); // needed for renewing policies
  showUserDropdown = signal(false);

  // Sorting State
  claimsSortOption = signal('dateDesc');
  paymentsSortOption = signal('dateDesc');
  applicationsSortOption = signal('dateDesc');

  showClaimsSortDropdown = signal(false);
  showPaymentsSortDropdown = signal(false);
  showApplicationsSortDropdown = signal(false);

  // Categorization
  policyFilter = signal('Active');

  activePolicies = computed(() => {
    const now = new Date();
    now.setHours(0, 0, 0, 0);

    return this.policies().filter(p => {
      if (p.status !== 'Active') return false;
      const endDate = new Date(p.endDate);
      endDate.setHours(0, 0, 0, 0);

      const diffTime = endDate.getTime() - now.getTime();
      const diffDays = Math.round(diffTime / (1000 * 3600 * 24));

      // If within 30 days of expiry AND not already renewed, it goes to Renewable
      if (diffDays <= 30 && diffDays >= 0 && !p.isRenewed) return false;
      return true;
    });
  });

  pendingPolicies = computed(() =>
    this.policies().filter(p => !['Active', 'Cancelled', 'Expired', 'PendingPayment', 'Claimed'].includes(p.status))
  );

  pendingPaymentPolicies = computed(() =>
    this.policies().filter(p => p.status === 'PendingPayment')
  );

  pendingApplicationsCount = computed(() =>
    this.applications().filter(a => a.status === 'UnderReview' || a.status === 0).length
  );

  claimablePolicies = computed(() =>
    this.policies().filter(p => p.status === 'Active')
  );

  inactivePolicies = computed(() => {
    const now = new Date();
    now.setHours(0, 0, 0, 0);

    return this.policies().filter(p => {
      // Inactive policies include Cancelled, Claimed, and Expired
      if (p.status === 'Cancelled' || p.status === 'Claimed' || p.status === 'Expired') return true;
      return false;
    });
  });

  renewablePolicies = computed(() => {
    const now = new Date();
    now.setHours(0, 0, 0, 0);

    return this.policies().filter(p => {
      if (p.isRenewed || p.status !== 'Active') return false; // Only Active policies can be renewed/upgraded before they expire

      const endDate = new Date(p.endDate);
      endDate.setHours(0, 0, 0, 0);

      const diffTime = endDate.getTime() - now.getTime();
      const diffDays = Math.round(diffTime / (1000 * 3600 * 24));

      return diffDays >= -30 && diffDays <= 30;
    });
  });

  selectedPolicy = signal<any>(null);

  // Claims State
  // Payments & Claims
  sortedClaims = computed(() => {
    const data = [...this.claims()];
    const option = this.claimsSortOption();
    return data.sort((a, b) => {
      if (option === 'dateDesc') return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
      if (option === 'dateAsc') return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      if (option === 'amountDesc') return (b.approvedAmount || 0) - (a.approvedAmount || 0);
      if (option === 'amountAsc') return (a.approvedAmount || 0) - (b.approvedAmount || 0);
      return 0;
    });
  });

  approvedClaims = computed(() => this.sortedClaims().filter(c => c.status === 'Approved' || c.status === 1));
  rejectedClaims = computed(() => this.sortedClaims().filter(c => c.status === 'Rejected' || c.status === 2));
  pendingClaimsList = computed(() => this.sortedClaims().filter(c => c.status === 'Submitted' || c.status === 'UnderReview' || c.status === 0));

  sortedPayments = computed(() => {
    const data = [...this.payments()];
    const option = this.paymentsSortOption();
    return data.sort((a, b) => {
      const amountA = Math.abs(a.amount || 0);
      const amountB = Math.abs(b.amount || 0);
      if (option === 'dateDesc') return new Date(b.paymentDate).getTime() - new Date(a.paymentDate).getTime();
      if (option === 'dateAsc') return new Date(a.paymentDate).getTime() - new Date(b.paymentDate).getTime();
      if (option === 'amountDesc') return amountB - amountA;
      if (option === 'amountAsc') return amountA - amountB;
      return 0;
    });
  });

  premiumPayments = computed(() =>
    this.sortedPayments().filter((p: any) =>
      !p.transactionReference ||
      (!p.transactionReference.toLowerCase().includes('claim') && !p.transactionReference.toLowerCase().includes('transfer'))
    )
  );

  claimPayments = computed(() =>
    this.sortedPayments()
      .filter((p: any) => p.transactionReference && p.transactionReference.toLowerCase().includes('claim'))
      .map((p: any) => {
        const ref = p.transactionReference || '';
        // Extract claim number from something like "Claim #CLM12345"
        const match = ref.match(/Claim #(\S+)/);
        const claimNum = match ? match[1] : null;
        const claim = claimNum ? this.claims().find((c: any) => (c.claimNumber === claimNum || c.claimId.toString() === claimNum)) : null;
        
        let displayType = 'Settlement';
        if (claim) {
          const type = (claim.claimType || '').toLowerCase();
          if (type.includes('thirdparty') || type.includes('third-party')) {
            displayType = 'Third-Party';
          } else if (type.includes('damage') || type.includes('theft') || type.includes('accident')) {
            displayType = 'Own Damage';
          } else {
            displayType = claim.claimType || 'Settlement';
          }
        }

        return {
          ...p,
          claimType: displayType
        };
      })
  );

  transferPayments = computed(() =>
    this.sortedPayments().filter((p: any) => p.transactionReference && p.transactionReference.toLowerCase().includes('transfer'))
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

  sortedApplications = computed(() => {
    const data = [...this.applications()];
    const option = this.applicationsSortOption();
    return data.sort((a, b) => {
      if (option === 'dateDesc') return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
      if (option === 'dateAsc') return new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      return 0;
    });
  });

  filteredRenewPlans = computed(() => {
    const policyId = this.renewingPolicyId();
    if (!policyId) return [];
    
    // Try to find the policy in the current list or use selectedPolicy if it matches
    let policy = this.policies().find(p => p.policyId == policyId);
    if (!policy && this.selectedPolicy()?.policyId == policyId) {
      policy = this.selectedPolicy();
    }
    
    if (!policy) return this.plans().filter(p => p.status === 1 || p.status === 'Active');

    // Find the current plan in the loaded plans list to determine its ApplicableVehicleType
    let currentPlan = undefined;
    const planIdToMatch = policy.planId || policy.plan?.planId || policy.policyPlan?.planId || policy.PlanId;
    
    if (planIdToMatch) {
      currentPlan = this.plans().find(p => p.planId == planIdToMatch || p.id == planIdToMatch);
    }
    
    if (!currentPlan && policy.planName) {
      currentPlan = this.plans().find(p => p.planName === policy.planName);
    }

    const vType = currentPlan ? (currentPlan.applicableVehicleType || currentPlan.ApplicableVehicleType)?.toString().trim() : null;

    if (!vType || vType === "N/A") {
      // Fallback if current plan not found, but this should rarely happen
      return this.plans().filter(p => p.status === 1 || p.status === 'Active');
    }

    return this.plans().filter(p => {
      const planVType = (p.applicableVehicleType || p.ApplicableVehicleType)?.toString().trim();
      const isActive = p.status === 1 || p.status === 'Active';
      return isActive && planVType && planVType.toLowerCase() === vType.toLowerCase();
    });
  });

  // Unique vehicles derived from policies — for the overview vehicles section
  myVehicles = computed(() => {
    const map = new Map<string, any>();
    this.policies().forEach((p: any) => {
      if (p.status !== 'Active') return;
      const reg = p.vehicleRegistrationNumber || p.registrationNumber;
      if (reg && !map.has(reg)) {
        map.set(reg, {
          reg,
          vehicleName: p.vehicleMake ? `${p.vehicleMake} ${p.vehicleModel}` : (p.vehicleName || reg),
          idv: p.idv || p.invoiceAmount || 0,
          planName: p.planName || 'Standard',
          status: p.status,
          roadsideAssistanceAvailable: p.roadsideAssistanceAvailable
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

  // Settings - Change Password
  changePasswordForm = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  };
  changePwdLoading = signal(false);

  getStatusString(status: any): string {
    const s = status?.toString();
    if (s === '0' || s === 'Submitted') return 'Submitted';
    if (s === '1' || s === 'Approved') return 'Approved';
    if (s === '2' || s === 'Rejected') return 'Rejected';
    return 'Under Review';
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

  showCurrentPwd = false;
  showNewPwd = false;
  showConfirmPwd = false;

  // Transfer state
  incomingTransfers = signal<any[]>([]);
  outgoingTransfers = signal<any[]>([]);
  showTransferModal = signal(false);
  showAcceptModal = signal(false);
  transferPolicyId = signal<number | null>(null);
  transferError = signal('');
  transferSuccess = signal('');
  pendingAcceptTransfer = signal<any>(null);
  showCancelPolicyModal = signal(false);
  cancelPolicyTargetId = signal<number | null>(null);
  cancelPolicyTargetNumber = signal<string | null>(null);
  cancelPolicyInput = '';

  private customerService = inject(CustomerService);
  private razorpayService = inject(RazorpayService);
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

        const code =
          decodedToken.ReferralCode ||
          decodedToken.referralCode ||
          '';
        this.referralCode.set(code);

        const role = this.authService.getRoleFromStoredToken();
        if (role === 'Admin') this.userRole.set('Executive Admin');
        else if (role === 'Agent') this.userRole.set('Agent');
        else if (role === 'ClaimsOfficer') this.userRole.set('Claims Officer');
        else this.userRole.set(role || 'Customer');
      } catch (error) {
        this.customerName.set('Customer');
        this.referralCode.set('');
      }
    }
  }

  switchTab(tab: string) {
    if (tab === 'explore-plans') {
      this.router.navigate(['/explore-plans']);
      return;
    }

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
    if (tab === 'settings') this.loadReferralAndWalletData();
  }

  loadOverviewData() {
    this.loadPolicies();
    this.loadClaims();
    this.loadApplications();
    this.loadPayments();
    this.loadIncomingTransfers();
    this.loadOutgoingTransfers();
    this.loadReferralAndWalletData();
  }

  loadReferralAndWalletData() {
    this.customerService.getWalletBalance().subscribe({
      next: (res) => this.walletBalance.set(Number(res?.balance || 0)),
      error: () => this.walletBalance.set(0)
    });

    this.customerService.getReferralHistory().subscribe({
      next: (res) => this.referralHistory.set(Array.isArray(res) ? res : []),
      error: () => this.referralHistory.set([])
    });
  }

  applyReferralCode(code: string) {
    this.customerService.applyReferralCode(code).subscribe({
      next: () => {
        this.successMessage.set('Referral code applied successfully. Discount will apply on your first premium payment.');
        this.loadReferralAndWalletData();
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err: any) => {
        this.errorMessage.set(extractErrorMessage(err));
        this.autoHideToast();
      }
    });
  }

  loadPayments() {
    this.customerService.getMyPayments().subscribe({
      next: (res) => this.payments.set(res),
      error: () => {}
    });
  }

  downloadInvoice(paymentId: number) {
    this.customerService.downloadInvoice(paymentId).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        window.open(url, '_blank');
        this.successMessage.set("Document opened in new tab.");
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err: any) => {
        this.router.navigate(['/error'], {
          state: { status: err.status, message: "Failed to download invoice.", title: 'Download Error' }
        });
      }
    });
  }

  downloadClaimReport(claimId: number) {
    this.customerService.downloadClaimReport(claimId).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        window.open(url, '_blank');
        this.successMessage.set("Report opened in new tab.");
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err: any) => {
        this.errorMessage.set("Failed to download settlement report.");
        this.autoHideToast();
      }
    });
  }

  downloadPolicyContract(policyId: number) {
    this.customerService.downloadPolicyContract(policyId).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        window.open(url, '_blank');
        this.successMessage.set("Policy contract opened in new tab.");
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err: any) => {
        this.errorMessage.set("Failed to download policy contract.");
        this.autoHideToast();
      }
    });
  }

  loadPolicies() {

    this.customerService.getMyPolicies().subscribe({
      next: (res) => this.policies.set(res),
      error: () => {}
    });
  }

  loadClaims() {
    this.customerService.getMyClaims().subscribe({
      next: (res) => this.claims.set(res),
      error: () => {}
    });
  }

  loadApplications() {
    this.customerService.getMyApplications().subscribe({
      next: (res) => this.applications.set(res),
      error: () => {}
    });
  }

  availableClaimTypes = signal<{value: string, label: string}[]>([
    { value: 'Damage', label: 'Vehicle Damage / Accident' },
    { value: 'Theft', label: 'Total Theft' },
    { value: 'ThirdParty', label: 'Third-Party Liability' }
  ]);

  onClaimPolicyChange() {
    const policyId = this.claimForm.PolicyId;
    let types = [
      { value: 'Damage', label: 'Vehicle Damage / Accident' },
      { value: 'Theft', label: 'Total Theft' },
      { value: 'ThirdParty', label: 'Third-Party Liability' }
    ];

    if (policyId) {
      let policy = this.policies().find(p => p.policyId == policyId);
      if (policy) {
        let currentPlan = undefined;
        const planId = policy.planId || policy.plan?.planId || policy.policyPlan?.planId || policy.PlanId;
        if (planId) currentPlan = this.plans().find(p => p.planId == planId || p.id == planId);
        if (!currentPlan && policy.planName) currentPlan = this.plans().find(p => p.planName === policy.planName);

        if (currentPlan) {
          const typeArray = [];
          if (currentPlan.coversThirdParty || currentPlan.CoversThirdParty || currentPlan.policyType?.toLowerCase() === 'thirdparty' || currentPlan.PolicyType?.toLowerCase() === 'thirdparty') {
             typeArray.push({ value: 'ThirdParty', label: 'Third-Party Liability' });
          }
          if (currentPlan.coversOwnDamage || currentPlan.CoversOwnDamage || (currentPlan.policyType && currentPlan.policyType.toLowerCase() !== 'thirdparty')) {
             typeArray.push({ value: 'Damage', label: 'Vehicle Damage / Accident' });
          }
          if (currentPlan.coversTheft || currentPlan.CoversTheft || (currentPlan.policyType && currentPlan.policyType.toLowerCase() !== 'thirdparty')) {
             typeArray.push({ value: 'Theft', label: 'Total Theft' });
          }
          if (typeArray.length > 0) types = typeArray;
        }
      }
    }

    this.availableClaimTypes.set(types);

    const isValid = types.some(t => t.value === this.claimForm.ClaimType);
    if (!isValid && types.length > 0) {
      this.claimForm.ClaimType = types[0].value;
    }
  }

  // --- Claims ---
  startClaim() {
    this.isFilingClaim.set(true);
    // Reset to defaults
    this.claimForm.PolicyId = null;
    this.claimForm.ClaimType = 'Damage';
    
    // Ensure plans are loaded so we can find coverage details for filtering claim types
    if (!this.plans() || this.plans().length === 0) {
      this.customerService.getAllPolicyPlans().subscribe({
        next: (res) => {
          this.plans.set(res);
          this.onClaimPolicyChange();
        },
        error: () => {}
      });
    } else {
      this.onClaimPolicyChange();
    }
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
      this.router.navigate(['/error'], {
        state: { status: 400, message: "Please fill in policy and claim type.", title: 'Claim Error' }
      });
      return;
    }

    if (this.claimForm.ClaimType === 'Theft' && !this.claimDoc1) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: "FIR document is required for theft claims.", title: 'Validation Error' }
      });
      return;
    }

    if (this.claimForm.ClaimType === 'ThirdParty' && (!this.claimDoc1 || !this.claimDoc2)) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: "Both repair bill and vehicle invoice are required for third party claims.", title: 'Validation Error' }
      });
      return;
    }

    if (this.claimForm.ClaimType === 'Damage' && !this.claimDoc1) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: "Repair bill is required for own damage claims.", title: 'Validation Error' }
      });
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
      error: (err: any) => {
        this.router.navigate(['/error'], {
          state: {
            status: err.status,
            message: extractErrorMessage(err),
            title: 'Claim Submission Failed'
          }
        });
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
      error: () => {}
    });
  }

  closePolicyDetails() {
    this.selectedPolicy.set(null);
  }

  payPremium(policyId: number) {
    this.razorpayService.createOrder(policyId).subscribe({
      next: (orderData: CreateOrderResponse) => {
        if (!orderData?.orderId || !orderData?.keyId) {
          this.errorMessage.set('Invalid payment order received from server. Please retry.');
          this.autoHideToast();
          return;
        }

        const onSuccess = (response: any) => {
          const verifyData: RazorpayVerifyPayload = {
            policyId: policyId,
            razorpayOrderId: response.razorpay_order_id,
            razorpayPaymentId: response.razorpay_payment_id,
            razorpaySignature: response.razorpay_signature
          };

          this.razorpayService.verifyPayment(verifyData).subscribe({
            next: (res: any) => {
              const discountAmount = Number(res?.discountAmount || orderData.discountAmount || 0);
              const discountMessage = discountAmount > 0
                ? ` Referral discount applied: Rs ${discountAmount.toFixed(2)}.`
                : '';
              this.successMessage.set(`Premium paid successfully via Razorpay!${discountMessage}`);
              this.loadOverviewData();
              if (this.selectedPolicy()?.policyId === policyId) {
                this.viewPolicyDetails(policyId);
              }
              setTimeout(() => this.successMessage.set(''), 3000);
            },
            error: (err: any) => {
              this.errorMessage.set("Payment verification failed. Please contact support.");
              this.autoHideToast();
            }
          });
        };

        this.razorpayService
          .launchCheckout(orderData, onSuccess, (message: string) => {
            this.errorMessage.set(message);
            this.autoHideToast();
          })
          .catch((error: any) => {
            this.errorMessage.set(error?.message || 'Unable to start payment checkout.');
            this.autoHideToast();
          });
      },
      error: (err: any) => {
        this.errorMessage.set(extractErrorMessage(err));
        this.autoHideToast();
      }
    });
  }

  cancelPolicy(policyId: number) {
    const policy = this.policies().find((p: any) => p.policyId === policyId)
      || (this.selectedPolicy() && this.selectedPolicy().policyId === policyId ? this.selectedPolicy() : null);
    const policyNumber = policy?.policyNumber;

    if (!policyNumber) {
      this.errorMessage.set('Unable to resolve policy number for confirmation. Please refresh and try again.');
      this.autoHideToast();
      return;
    }

    this.cancelPolicyTargetId.set(policyId);
    this.cancelPolicyTargetNumber.set(policyNumber);
    this.cancelPolicyInput = '';
    this.showCancelPolicyModal.set(true);
  }

  closeCancelPolicyModal() {
    this.showCancelPolicyModal.set(false);
    this.cancelPolicyTargetId.set(null);
    this.cancelPolicyTargetNumber.set(null);
    this.cancelPolicyInput = '';
  }

  confirmCancelPolicy() {
    const policyId = this.cancelPolicyTargetId();
    const policyNumber = this.cancelPolicyTargetNumber();
    if (!policyId || !policyNumber) {
      this.closeCancelPolicyModal();
      return;
    }

    const confirmationToken = `delete-policy-${policyNumber}`;
    if (this.cancelPolicyInput.trim() !== confirmationToken) {
      this.errorMessage.set(`Confirmation failed. Type exactly: ${confirmationToken}`);
      this.autoHideToast();
      return;
    }

    this.closeCancelPolicyModal();

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
        this.errorMessage.set(extractErrorMessage(err));
        this.autoHideToast();
      }
    });
  }

  startRenew(policyId: number) {
    this.renewingPolicyId.set(policyId);
    this.renewForm.NewPlanId = null;
    this.renewForm.SelectedYears = 1;

    // Load available plans if not already loaded, they'll be filtered by computed

    if (!this.plans() || this.plans().length === 0) {
      this.customerService.getAllPolicyPlans().subscribe({
        next: (res) => this.plans.set(res),
        error: () => {}
      });
    }
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
        this.errorMessage.set(extractErrorMessage(err));
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
    this.transferError.set('');
    this.transferSuccess.set('');
    this.showTransferModal.set(true);
  }

  closeTransferModal() {
    this.showTransferModal.set(false);
    this.transferPolicyId.set(null);
  }

  initiateTransfer(recipientEmail: string) {
    const policyId = this.transferPolicyId();
    if (!recipientEmail || !policyId) return;

    this.transferError.set('');
    this.customerService.initiateTransfer(policyId, recipientEmail).subscribe({
      next: () => {
        this.transferSuccess.set('Transfer request sent successfully.');
        this.loadOutgoingTransfers();
        setTimeout(() => this.closeTransferModal(), 2000);
      },
      error: (err: any) => {
        this.transferError.set(extractErrorMessage(err));
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
    this.showAcceptModal.set(true);
  }

  closeAcceptModal() {
    this.showAcceptModal.set(false);
    this.pendingAcceptTransfer.set(null);
  }

  acceptTransfer(rcFile: File) {
    const transfer = this.pendingAcceptTransfer();
    if (!transfer || !rcFile) {
      this.errorMessage.set('Please upload your RC document.');
      this.autoHideToast();
      return;
    }

    this.customerService.acceptTransfer(transfer.policyTransferId, rcFile).subscribe({
      next: () => {
        this.successMessage.set('Transfer accepted! The application is now pending agent approval.');
        this.closeAcceptModal();
        this.loadIncomingTransfers();
        this.loadPolicies();
        setTimeout(() => this.successMessage.set(''), 5000);
      },
      error: (err: any) => {
        this.errorMessage.set(extractErrorMessage(err));
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
        this.errorMessage.set(extractErrorMessage(err));
        this.autoHideToast();
      }
    });
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
      error: (err) => {
        this.changePwdLoading.set(false);
        this.errorMessage.set(extractErrorMessage(err));
        this.autoHideToast();
      }
    });
  }

  handleRoadsideAssistance(vehicleReg: string) {
    this.successMessage.set('Fetching your location...');
    
    if (!navigator.geolocation) {
      this.errorMessage.set('Geolocation is not supported by your browser.');
      this.autoHideToast();
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const data = {
          username: this.customerName(),
          vehicleNumber: vehicleReg,
          longitude: position.coords.longitude,
          latitude: position.coords.latitude
        };

        this.customerService.requestRoadsideAssistance(data).subscribe({
          next: (res: any) => {
            // Display message from response or a default one
            const msg = res?.message || res?.msg || 'Assistance is on the way! We have received your location.';
            this.successMessage.set(msg);
            setTimeout(() => this.successMessage.set(''), 7000);
          },
          error: (err: any) => {
            // Even if n8n returns an error, we might want to tell the user something went wrong
            this.errorMessage.set('Could not contact roadside assistance. Please try again or call support.');
            this.autoHideToast();
          }
        });
      },
      (error) => {
        this.errorMessage.set('Unable to retrieve your location. Please check your browser permissions.');
        this.autoHideToast();
      }
    );
  }
}
