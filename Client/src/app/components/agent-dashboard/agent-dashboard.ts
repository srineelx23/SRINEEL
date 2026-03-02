import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { AgentService } from '../../services/agent.service';

@Component({
  selector: 'app-agent-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
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
  pendingApps = signal<any[]>([]);
  reviewedApps = signal<any[]>([]);
  customers = signal<any[]>([]);

  // UI State
  selectedApp = signal<any>(null);
  selectedCustomerRecord = signal<any>(null);

  reviewAction = {
    approved: true,
    rejectionReason: '',
    invoiceAmount: null as number | null
  };

  errorMessage = signal('');
  successMessage = signal('');

  ngOnInit() {
    this.loadDashboardData();
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
}
