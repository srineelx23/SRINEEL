import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { AgentService } from '../../services/agent.service';
import { extractErrorMessage } from '../../utils/error-handler';
import { jwtDecode } from 'jwt-decode';
import { NavbarComponent } from './navbar/navbar';
import { OverviewComponent } from './overview/overview';
import { ApplicationsComponent } from './applications/applications';
import { HistoryComponent } from './history/history';
import { CustomersComponent } from './customers/customers';
import { SettingsComponent } from './settings/settings';
import { NotificationsComponent } from '../notifications/notifications';


@Component({
  selector: 'app-agent-dashboard',
  standalone: true,
  imports: [
    CommonModule, 
    FormsModule, 
    NavbarComponent, 
    OverviewComponent, 
    ApplicationsComponent, 
    HistoryComponent, 
    CustomersComponent, 
    SettingsComponent,
    NotificationsComponent
  ],

  templateUrl: './agent-dashboard.html',
  styleUrl: './agent-dashboard.css',
})
export class AgentDashboard implements OnInit {
  activeTab = signal('overview');

  // Services
  private authService = inject(AuthService);
  private agentService = inject(AgentService);
  private router = inject(Router);

  // Data
  agentName = signal('Agent');
  userRole = signal('Field Agent');
  pendingApps = signal<any[]>([]);
  reviewedApps = signal<any[]>([]);
  customers = signal<any[]>([]);

  // Sorting State
  appsSortOption = signal('dateDesc');
  customersSortOption = signal('dateDesc');

  showAppsSortDropdown = signal(false);
  showCustomersSortDropdown = signal(false);

  pendingPaymentCount = computed(() => {
    return this.customers().filter(c => c.policyStatus === 'PendingPayment').length;
  });

  sortedPendingApps = computed(() => {
    const data = [...this.pendingApps()];
    const option = this.appsSortOption();
    return data.sort((a, b) => {
      const dateA = new Date(a.createdAt || 0).getTime();
      const dateB = new Date(b.createdAt || 0).getTime();
      if (option === 'dateDesc') return dateB - dateA;
      if (option === 'dateAsc') return dateA - dateB;
      return 0;
    });
  });

  sortedReviewedApps = computed(() => {
    const data = [...this.reviewedApps()];
    const option = this.appsSortOption();
    return data.sort((a, b) => {
      const dateA = new Date(a.createdAt || 0).getTime();
      const dateB = new Date(b.createdAt || 0).getTime();
      if (option === 'dateDesc') return dateB - dateA;
      if (option === 'dateAsc') return dateA - dateB;
      return 0;
    });
  });

  sortedCustomers = computed(() => {
    const data = [...this.customers()];
    const option = this.customersSortOption();
    return data.sort((a, b) => {
      if (option === 'nameAsc') return (a.customerName || '').localeCompare(b.customerName || '');
      if (option === 'nameDesc') return (b.customerName || '').localeCompare(a.customerName || '');
      if (option === 'amountDesc') return (b.premiumAmount || 0) - (a.premiumAmount || 0);
      if (option === 'amountAsc') return (a.premiumAmount || 0) - (b.premiumAmount || 0);
      return 0;
    });
  });

  // UI State
  selectedApp = signal<any>(null);
  selectedCustomerRecord = signal<any>(null);
  showUserDropdown = signal(false);

  reviewAction = {
    approved: true,
    rejectionReason: '',
    invoiceAmount: null as number | null
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
          'Agent';
        this.agentName.set(name);

        const role = this.authService.getRoleFromStoredToken();
        if (role === 'Agent') this.userRole.set('Agent');
        else if (role === 'Admin') this.userRole.set('Executive Admin');
        else this.userRole.set(role || 'Agent');
      } catch (error) {
        console.error('Failed to parse token for name', error);
      }
    }
  }

  mapStatus(statusCode: number): string {
    switch (statusCode) {
      case 0: return 'Pending';
      case 1: return 'Approved';
      case 2: return 'Rejected';
      default: return 'Unknown';
    }
  }

  loadDashboardData() {
    this.agentService.getPendingApplications().subscribe({
      next: (res) => {
        const mapped = res.map((app: any) => ({ ...app, status: this.mapStatus(app.status) }));
        this.pendingApps.set(mapped);
      },
      error: (err) => console.error('Error loading pending apps:', err)
    });

    this.agentService.getApplications().subscribe({
      next: (res) => {
        const mapped = res.map((app: any) => ({ ...app, status: this.mapStatus(app.status) }));
        this.reviewedApps.set(mapped.filter((a: any) => a.status !== 'Pending'));
      },
      error: (err) => console.error('Error loading reviewed apps:', err)
    });

    this.agentService.getCustomers().subscribe({
      next: (res) => {
        const flattenedRecords: any[] = [];
        res.forEach((customer: any) => {
          if (customer.vehicles && customer.vehicles.length > 0) {
            customer.vehicles.forEach((vehicle: any) => {
              if (vehicle.policies && vehicle.policies.length > 0) {
                vehicle.policies.forEach((policy: any) => {
                  flattenedRecords.push({
                    customerName: customer.customerName,
                    email: customer.email,
                    vehicleMake: vehicle.make,
                    vehicleModel: vehicle.model,
                    vehicleYear: vehicle.year,
                    registrationNumber: vehicle.registrationNumber,
                    documents: vehicle.documents,
                    policyNumber: policy.policyNumber,
                    policyStatus: policy.status,
                    premiumAmount: policy.premiumAmount,
                    customerObj: customer
                  });
                });
              } else {
                // If a vehicle has no policy yet (maybe just application approved)
                flattenedRecords.push({
                  customerName: customer.customerName,
                  email: customer.email,
                  vehicleMake: vehicle.make,
                  vehicleModel: vehicle.model,
                  vehicleYear: vehicle.year,
                  registrationNumber: vehicle.registrationNumber,
                  documents: vehicle.documents,
                  policyNumber: 'N/A',
                  policyStatus: 'N/A',
                  premiumAmount: 0,
                  customerObj: customer
                });
              }
            });
          }
        });
        this.customers.set(flattenedRecords);
      },
      error: (err) => console.error('Error loading customers:', err)
    });
  }

  // Navigation
  switchTab(tabId: string) {
    this.activeTab.set(tabId);
    this.selectedApp.set(null);
    this.selectedCustomerRecord.set(null);
    this.syncChatbotContext();
  }

  private syncChatbotContext() {
    localStorage.setItem('vims.agent.activeTab', this.activeTab());
    window.dispatchEvent(new CustomEvent('vims-chatbot-context-changed'));
  }

  goHome() {
    this.router.navigate(['/']);
  }

  logout() {
    this.authService.logout();
  }

  // Application Review
  openAppReview(app: any) {
    this.selectedApp.set(app);
    this.reviewAction.approved = true;
    this.reviewAction.rejectionReason = '';
    this.reviewAction.invoiceAmount = null;
  }

  closeAppReview() {
    this.selectedApp.set(null);
  }

  // Customer Record View
  openCustomerDetails(record: any) {
    this.selectedCustomerRecord.set(record);
  }

  closeCustomerDetails() {
    this.selectedCustomerRecord.set(null);
  }

  submitReviewWithData(data: any) {
    this.reviewAction = data;
    this.submitReview();
  }

  submitReview() {
    this.errorMessage.set('');

    if (this.reviewAction.approved && !this.reviewAction.invoiceAmount) {
      this.errorMessage.set('Please provide an Invoice Amount for approval by inspecting documents.');
      this.autoHideToast();
      return;
    }

    if (!this.reviewAction.approved && !this.reviewAction.rejectionReason) {
      this.errorMessage.set('Please provide a reason for rejection.');
      this.autoHideToast();
      return;
    }

    // ── Invoice Amount Mismatch Check ─────────────────────────────────────
    // Compare the agent's entered amount against the customer's OCR-extracted amount.
    if (this.reviewAction.approved) {
      const submittedByCustomer: number = Number(this.selectedApp()?.invoiceAmount ?? 0);
      const enteredByAgent: number = Number(this.reviewAction.invoiceAmount ?? 0);

      if (submittedByCustomer > 0 && enteredByAgent !== submittedByCustomer) {
        this.errorMessage.set(
          `The invoice amount differs from the invoice document (document shows ₹${submittedByCustomer.toLocaleString('en-IN')}). Please verify before approving.`
        );
        this.autoHideToast();
        return;
      }
    }

    const payload = {
      Approved: this.reviewAction.approved,
      RejectionReason: this.reviewAction.approved ? undefined : this.reviewAction.rejectionReason,
      InvoiceAmount: this.reviewAction.approved ? Number(this.reviewAction.invoiceAmount) : 0
    };

    const appId = this.selectedApp().vehicleApplicationId;

    this.agentService.reviewApplication(appId, payload).subscribe({
      next: () => {
        this.successMessage.set(`Application ${this.reviewAction.approved ? 'Approved' : 'Rejected'} successfully!`);
        this.closeAppReview();
        this.loadDashboardData();
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err) => {
        this.errorMessage.set(err.error || 'Failed to submit review.');
        this.autoHideToast();
      }
    });
  }

  private autoHideToast() {
    setTimeout(() => {
      this.errorMessage.set('');
    }, 5000);
  }

  changePasswordWithData(data: any) {
    this.changePasswordForm = data;
    this.changePassword();
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

  getSortLabel(option: string): string {
    switch (option) {
      case 'dateDesc': return 'Newest First';
      case 'dateAsc': return 'Oldest First';
      case 'nameAsc': return 'Name: A-Z';
      case 'nameDesc': return 'Name: Z-A';
      case 'amountDesc': return 'Premium: High to Low';
      case 'amountAsc': return 'Premium: Low to High';
      default: return 'Newest First';
    }
  }
}
