import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { AdminService } from '../../services/admin.service';
import { jwtDecode } from 'jwt-decode';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, BaseChartDirective],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.css',
})
export class AdminDashboard implements OnInit, OnDestroy {
  activeTab = signal('overview');
  currentYear = new Date().getFullYear();

  private authService = inject(AuthService);
  private adminService = inject(AdminService);
  private router = inject(Router);

  // Data Stores
  adminName = signal('Admin');
  users = signal<any[]>([]);
  policies = signal<any[]>([]);
  claims = signal<any[]>([]);
  payments = signal<any[]>([]);
  plans = signal<any[]>([]);
  showUserDropdown = signal(false);

  // Aggregate Computations

  // Identify payment records that are actually claim payouts (not premiums).
  // When a claim is approved, a payment record is created with the same amount
  // and policyId. We exclude these from revenue and premium ledgers.
  claimPaymentIds = computed(() => {
    const claimSignatures = new Set<string>();
    this.claims()
      .filter((c: any) => c.status === 'Approved' || c.status === 1)
      .forEach((c: any) => {
        if (c.approvedAmount && c.policyId) {
          claimSignatures.add(`${c.policyId}:${c.approvedAmount}`);
        }
      });

    const payoutIds = new Set<number>();
    this.payments().forEach((p: any) => {
      const key = `${p.policyId}:${p.amount}`;
      if (claimSignatures.has(key)) {
        payoutIds.add(p.paymentId);
      }
    });
    return payoutIds;
  });

  // Only count actual premium payments (exclude claim payout records)
  premiumPayments = computed(() =>
    this.payments().filter((p: any) =>
      (p.status === 'Paid' || p.status === 1) &&
      !this.claimPaymentIds().has(p.paymentId)
    )
  );

  totalRevenue = computed(() =>
    this.premiumPayments().reduce((sum: number, p: any) => sum + (p.amount || 0), 0)
  );

  totalClaimsApproved = computed(() => {
    return this.claims().filter((c: any) => c.status === 'Approved' || c.status === 1).length;
  });

  totalPayoutAmount = computed(() => {
    return this.claims()
      .filter((c: any) => c.status === 'Approved' || c.status === 1)
      .reduce((sum, c) => sum + (c.approvedAmount || 0), 0);
  });

  netProfit = computed(() => {
    return this.totalRevenue() - this.totalPayoutAmount();
  });

  totalActivePolicies = computed(() => {
    return this.policies().filter((p: any) => p.status === 'Active' || p.status === 0).length;
  });

  vehicles = computed(() => {
    // Extract unique vehicles from policies
    const vMap = new Map();
    this.policies().forEach(p => {
      if (p.vehicle && !vMap.has(p.vehicle.vehicleId)) {
        vMap.set(p.vehicle.vehicleId, { ...p.vehicle, currentIdv: p.idv || p.invoiceAmount, customerName: p.customer?.fullName || 'Anonymous' });
      }
    });
    return Array.from(vMap.values());
  });

  selectedVehicle = signal<any | null>(null);

  vehiclePolicies = computed(() => {
    const v = this.selectedVehicle();
    if (!v) return [];
    return this.policies().filter(p => p.vehicle?.vehicleId === v.vehicleId);
  });

  vehicleClaims = computed(() => {
    const policyIds = this.vehiclePolicies().map(p => p.policyId);
    return this.claims().filter(c => policyIds.includes(c.policyId));
  });

  vehiclePayments = computed(() => {
    const policyIds = this.vehiclePolicies().map(p => p.policyId);
    // Exclude payment records that are actually claim payouts
    return this.payments().filter((p: any) =>
      policyIds.includes(p.policyId) &&
      !this.claimPaymentIds().has(p.paymentId)
    );
  });

  vehicleTransactions = computed(() => {
    let txns: any[] = [];

    // Premium Payments (Credits to company)
    this.vehiclePayments().forEach(p => {
      txns.push({
        id: p.paymentId || p.transactionId,
        date: p.paymentDate,
        type: 'Premium Payment',
        amount: p.amount,
        status: p.status,
        isCredit: true
      });
    });

    // Claim Payouts (Debits — losses to company)
    this.vehicleClaims()
      .filter((c: any) => c.status === 'Approved' || c.status === 1)
      .forEach((c: any) => {
        if (c.approvedAmount) {
          txns.push({
            id: c.claimNumber || c.claimId,
            date: c.resolvedDate || c.incidentDate,
            type: 'Claim Payout',
            amount: c.approvedAmount,
            status: 'Settled',
            isCredit: false
          });
        }
      });

    // Sort by date descending where possible
    return txns.sort((a, b) => {
      if (!a.date) return 1;
      if (!b.date) return -1;
      return new Date(b.date).getTime() - new Date(a.date).getTime();
    });
  });

  vehicleDocuments = computed(() => {
    let docs: any[] = [];
    const v = this.selectedVehicle();
    if (!v) return docs;

    // Vehicle Documents
    if (v.documents && Array.isArray(v.documents)) {
      v.documents.forEach((d: any) => {
        if (d.filePath) docs.push({ name: d.documentType || 'Vehicle Document', url: 'https://localhost:7257/' + d.filePath });
      });
    }

    // Claim Documents
    this.vehicleClaims().forEach(c => {
      if (c.documents && Array.isArray(c.documents)) {
        c.documents.forEach((d: any, index: number) => {
          if (d.document1) docs.push({ name: `Claim ${c.claimNumber} - Primary Evidence`, url: 'https://localhost:7257/' + d.document1 });
          if (d.document2) docs.push({ name: `Claim ${c.claimNumber} - Secondary Evidence`, url: 'https://localhost:7257/' + d.document2 });
        });
      }
    });

    return docs;
  });

  // Ordered Plans
  orderedPlans = computed(() => {
    const pCounts = new Map();
    this.policies().forEach(p => {
      const pId = p.planId || p.policyPlanId || (p.policyPlan ? p.policyPlan.planId : 0);
      if (pId) pCounts.set(pId, (pCounts.get(pId) || 0) + 1);
    });

    const arr = [...this.plans()];
    arr.sort((a, b) => {
      const countA = pCounts.get(a.planId) || 0;
      const countB = pCounts.get(b.planId) || 0;
      return countB - countA;
    });
    return arr;
  });

  // Filters
  vehicleIdvFilter = signal<number>(0);
  filteredVehicles = computed(() => {
    return this.vehicles().filter(v => (v.currentIdv || v.year) >= this.vehicleIdvFilter());
  });

  userRoleFilter = signal<string>('All');
  filteredUsers = computed(() => {
    const mappedUsers = this.users().map(u => ({ ...u, displayRole: this.getRoleString(u.role || u.roles) }));
    if (this.userRoleFilter() === 'All') return mappedUsers;
    return mappedUsers.filter(u => u.displayRole === this.userRoleFilter() || (u.roles && u.roles.includes(this.userRoleFilter())));
  });

  getRoleString(roleVal: any): string {
    if (typeof roleVal === 'string') return roleVal;
    if (Array.isArray(roleVal) && roleVal.length > 0) return roleVal[0];
    switch (roleVal) {
      case 0: return 'Admin';
      case 1: return 'Agent';
      case 2: return 'ClaimsOfficer';
      case 3: return 'Customer';
      default: return 'Unknown';
    }
  }

  rolesList = computed(() => ['All', 'Admin', 'Customer', 'Agent', 'ClaimsOfficer']);

  // Theme tracker (must be declared before computed chart options that use it)
  isDarkMode = signal(true);

  // Chart Properties
  revenueChartOptions = computed<ChartConfiguration['options']>(() => ({
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      y: {
        beginAtZero: true,
        grid: { color: this.isDarkMode() ? 'rgba(255,255,255,0.07)' : 'rgba(0,0,0,0.07)' },
        ticks: {
          color: this.isDarkMode() ? '#94A3B8' : '#475569',
          callback: (v: any) => '₹' + (v >= 1000 ? (v / 1000).toFixed(0) + 'K' : v)
        },
        border: { display: false }
      },
      x: {
        grid: { display: false },
        ticks: { color: this.isDarkMode() ? '#94A3B8' : '#475569' },
        border: { display: false }
      }
    },
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: this.isDarkMode() ? 'rgba(15,26,54,0.95)' : 'rgba(255,255,255,0.97)',
        titleColor: this.isDarkMode() ? '#5BC0BE' : '#2563EB',
        bodyColor: this.isDarkMode() ? '#F1F5F9' : '#0F172A',
        borderColor: this.isDarkMode() ? 'rgba(91,192,190,0.3)' : 'rgba(37,99,235,0.2)',
        borderWidth: 1,
        padding: 12,
        callbacks: {
          label: (ctx: any) => ' ₹' + ctx.parsed.y.toLocaleString('en-IN')
        }
      }
    }
  }));
  revenueChartData: ChartData<'bar'> = {
    labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
    datasets: [{
      data: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], label: 'Monthly Revenue',
      backgroundColor: 'rgba(91,192,190,0.8)', hoverBackgroundColor: '#5BC0BE',
      borderRadius: 6, borderSkipped: false
    }]
  };
  revenueChartType: ChartType = 'bar';

  claimsChartOptions = computed<ChartConfiguration['options']>(() => ({
    responsive: true,
    maintainAspectRatio: false,
    cutout: '68%',
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          color: this.isDarkMode() ? '#E2E8F0' : '#1E293B',
          padding: 20,
          font: { size: 13, family: "'Inter', sans-serif", weight: '500' },
          usePointStyle: true,
          pointStyle: 'circle'
        }
      },
      tooltip: {
        backgroundColor: this.isDarkMode() ? 'rgba(15,26,54,0.95)' : 'rgba(255,255,255,0.97)',
        titleColor: this.isDarkMode() ? '#F1F5F9' : '#0F172A',
        bodyColor: this.isDarkMode() ? '#94A3B8' : '#475569',
        borderColor: this.isDarkMode() ? 'rgba(255,255,255,0.1)' : 'rgba(0,0,0,0.1)',
        borderWidth: 1,
        padding: 12,
        callbacks: {
          label: (ctx: any) => ` ${ctx.label}: ${ctx.parsed} claims`
        }
      }
    }
  } as any));
  claimsChartData: ChartData<'doughnut'> = {
    labels: ['✅ Approved', '❌ Rejected', '⏳ Under Review'],
    datasets: [{
      data: [0, 0, 0],
      backgroundColor: ['rgba(34, 197, 94, 0.85)', 'rgba(239, 68, 68, 0.85)', 'rgba(251, 191, 36, 0.85)'],
      hoverBackgroundColor: ['#22C55E', '#EF4444', '#FBB724'],
      borderColor: this.isDarkMode() ? '#0F1A36' : '#F8FAFC',
      borderWidth: 3,
      hoverOffset: 8
    }]
  };
  claimsChartType: ChartType = 'doughnut';

  // Modal / Form States
  isCreatingAgent = signal(false);
  isCreatingClaimsOfficer = signal(false);
  isCreatingPlan = signal(false);

  registerForm = { fullName: '', email: '', password: '', role: '' };
  planForm = { planName: '', description: '', coveragePercent: 80, isZeroDepreciationAvailable: false, coversThirdParty: true, coversTheft: true, coversOwnDamage: true, maxCoverageAmount: null as number | null, deductibleAmount: 0 };

  errorMessage = signal('');
  successMessage = signal('');

  ngOnInit() {
    this.extractName();
    this.checkTheme();
    this.loadAllData();
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
          'Admin';
        this.adminName.set(name);
      } catch (error) {
        console.error('Failed to parse token for name', error);
      }
    }
  }

  private themeObserver?: MutationObserver;

  ngOnDestroy() {
    this.themeObserver?.disconnect();
  }

  checkTheme() {
    // Read the current theme from the <html data-theme="..."> attribute,
    // which is the single source of truth (set by the theme toggle button).
    const readTheme = () => {
      const attr = document.documentElement.getAttribute('data-theme');
      this.isDarkMode.set(attr !== 'light');
    };

    readTheme(); // set immediately on init

    // Watch for attribute changes so the chart updates reactively
    // when the user clicks the theme toggle — even within the same tab.
    this.themeObserver = new MutationObserver(readTheme);
    this.themeObserver.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['data-theme']
    });
  }

  loadAllData() {
    this.adminService.getAllUsers().subscribe(res => this.users.set(res));
    this.adminService.getAllPolicyPlans().subscribe(res => this.plans.set(res));
    this.adminService.getAllPolicies().subscribe(res => this.policies.set(res));

    // Load payments and claims together so we can cross-reference claim payouts
    this.adminService.getAllPayments().subscribe(payments => {
      this.payments.set(payments);
      // Wait for claims to arrive first if possible, otherwise rebuild after claims load
      if (this.claims().length > 0) {
        this.rebuildRevenueChart();
      }
    });

    this.adminService.getAllClaims().subscribe(res => {
      this.claims.set(res);
      this.updateClaimsChart(res);
      // Now that claims are loaded, rebuild revenue chart with correct exclusions
      if (this.payments().length > 0) {
        this.rebuildRevenueChart();
      }
    });
  }

  rebuildRevenueChart() {
    // Build the set of claim payout payment amounts to exclude
    const claimSignatures = new Set<string>();
    this.claims()
      .filter((c: any) => c.status === 'Approved' || c.status === 1)
      .forEach((c: any) => {
        if (c.approvedAmount && c.policyId) {
          claimSignatures.add(`${c.policyId}:${c.approvedAmount}`);
        }
      });

    const premiumOnly = this.payments().filter((p: any) => {
      const key = `${p.policyId}:${p.amount}`;
      return !claimSignatures.has(key);
    });

    this.updateRevenueChart(premiumOnly);
  }

  updateRevenueChart(paymentData: any[]) {
    const monthlyTotals = new Array(12).fill(0);
    paymentData.forEach(p => {
      if (p.status === 'Paid' || p.status === 1) {
        const date = new Date(p.paymentDate);
        // Only count current year if needed, or group all years. 
        // For simplicity we will group by month
        if (date.getFullYear() === new Date().getFullYear()) {
          monthlyTotals[date.getMonth()] += p.amount;
        }
      }
    });

    this.revenueChartData = {
      labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
      datasets: [
        {
          data: monthlyTotals,
          label: 'Monthly Revenue (Current Year)',
          backgroundColor: 'rgba(91,192,190,0.8)',
          hoverBackgroundColor: '#5BC0BE',
          borderRadius: 6,
          borderSkipped: false
        }
      ]
    };
  }

  updateClaimsChart(claimData: any[]) {
    let approved = 0;
    let rejected = 0;
    let pending = 0;
    claimData.forEach(c => {
      const status = c.status === 1 || c.status === 'Approved' ? 'Approved' :
        c.status === 2 || c.status === 'Rejected' ? 'Rejected' : 'Pending';
      if (status === 'Approved') approved++;
      else if (status === 'Rejected') rejected++;
      else pending++;
    });

    this.claimsChartData = {
      labels: ['✅ Approved', '❌ Rejected', '⏳ Under Review'],
      datasets: [{
        data: [approved, rejected, pending],
        backgroundColor: ['rgba(34, 197, 94, 0.85)', 'rgba(239, 68, 68, 0.85)', 'rgba(251, 191, 36, 0.85)'],
        hoverBackgroundColor: ['#22C55E', '#EF4444', '#FBB724'],
        borderColor: this.isDarkMode() ? '#0F1A36' : '#F8FAFC',
        borderWidth: 3,
        hoverOffset: 8
      }]
    };
  }

  switchTab(tabId: string) {
    this.activeTab.set(tabId);
    this.isCreatingAgent.set(false);
    this.isCreatingClaimsOfficer.set(false);
    this.isCreatingPlan.set(false);
    this.selectedVehicle.set(null); // Return to list view
  }

  goHome() {
    this.router.navigate(['/']);
  }

  logout() {
    this.authService.logout();
  }

  // --- Actions ---

  togglePlanStatus(planId: number, status: number) {
    if (status === 1) { // Current status is active, so deactivate
      this.adminService.deactivatePlan(planId).subscribe({
        next: () => {
          this.successMessage.set('Plan Deactivated!');
          this.loadAllData();
          setTimeout(() => this.successMessage.set(''), 3000);
        }
      });
    } else { // Current status is inactive (0), so activate
      this.adminService.activatePlan(planId).subscribe({
        next: () => {
          this.successMessage.set('Plan Activated!');
          this.loadAllData();
          setTimeout(() => this.successMessage.set(''), 3000);
        }
      });
    }
  }

  viewVehicleDetails(vehicle: any) {
    this.selectedVehicle.set(vehicle);
  }

  backToVehicles() {
    this.selectedVehicle.set(null);
  }

  submitUserRegistration() {
    this.errorMessage.set('');
    if (this.isCreatingAgent()) {
      this.adminService.createAgent(this.registerForm).subscribe({
        next: () => {
          this.successMessage.set('Agent Created Successfully!');
          this.isCreatingAgent.set(false);
          this.loadAllData();
          setTimeout(() => this.successMessage.set(''), 3000);
        },
        error: (err) => this.showError(err)
      });
    } else {
      this.adminService.createClaimsOfficer(this.registerForm).subscribe({
        next: () => {
          this.successMessage.set('Claims Officer Created Successfully!');
          this.isCreatingClaimsOfficer.set(false);
          this.loadAllData();
          setTimeout(() => this.successMessage.set(''), 3000);
        },
        error: (err) => this.showError(err)
      });
    }
  }

  submitPlanRegistration() {
    this.errorMessage.set('');
    this.adminService.createPolicyPlan(this.planForm).subscribe({
      next: () => {
        this.successMessage.set('New Policy Plan Generated Successfully!');
        this.isCreatingPlan.set(false);
        this.loadAllData();
        setTimeout(() => this.successMessage.set(''), 3000);
      },
      error: (err) => this.showError(err)
    });
  }

  openUserForm(role: string) {
    this.registerForm = { fullName: '', email: '', password: '', role: role };
    if (role === 'Agent') {
      this.isCreatingAgent.set(true);
      this.isCreatingClaimsOfficer.set(false);
    } else {
      this.isCreatingAgent.set(false);
      this.isCreatingClaimsOfficer.set(true);
    }
  }

  openPlanForm() {
    this.planForm = { planName: '', description: '', coveragePercent: 80, isZeroDepreciationAvailable: false, coversThirdParty: true, coversTheft: true, coversOwnDamage: true, maxCoverageAmount: null, deductibleAmount: 0 };
    this.isCreatingPlan.set(true);
  }

  private showError(err: any) {
    this.errorMessage.set(err.error?.message || err.error || 'Operation Failed.');
    setTimeout(() => this.errorMessage.set(''), 4000);
  }
}
