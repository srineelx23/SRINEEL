import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { AdminService } from '../../services/admin.service';
import { extractErrorMessage } from '../../utils/error-handler';
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
  auditLogs = signal<any[]>([]);
  showUserDropdown = signal(false);
  showRoleDropdown = signal(false);

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
    const policyIds = this.vehiclePolicies().map(p => p.policyId);
    // Pull ALL payments for this vehicle's policies (premiums + claim payouts)
    const allVehiclePayments = this.payments().filter((p: any) =>
      policyIds.includes(p.policyId)
    );

    const txns = allVehiclePayments.map((p: any) => {
      const isClaimPayout = this.claimPaymentIds().has(p.paymentId);
      return {
        id: p.paymentId,
        date: p.paymentDate,        // from payments table — always set
        type: isClaimPayout ? 'Claim Payout' : 'Premium Payment',
        amount: p.amount,
        status: p.status,
        isCredit: !isClaimPayout
      };
    });

    // Sort by date descending
    return txns.sort((a: any, b: any) => {
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

  // Per-vehicle premium collected and claims paid out (for the vehicles list table)
  vehiclePremiumsMap = computed(() => {
    const map = new Map<number, number>();
    this.payments().forEach((p: any) => {
      if ((p.status === 'Paid' || p.status === 1) && !this.claimPaymentIds().has(p.paymentId)) {
        const policy = this.policies().find((pol: any) => pol.policyId === p.policyId);
        if (policy?.vehicle?.vehicleId) {
          const vid = policy.vehicle.vehicleId;
          map.set(vid, (map.get(vid) || 0) + (p.amount || 0));
        }
      }
    });
    return map;
  });

  vehicleClaimsMap = computed(() => {
    const map = new Map<number, number>();
    this.claims()
      .filter((c: any) => c.status === 'Approved' || c.status === 1)
      .forEach((c: any) => {
        const policy = this.policies().find((pol: any) => pol.policyId === c.policyId);
        if (policy?.vehicle?.vehicleId && c.approvedAmount) {
          const vid = policy.vehicle.vehicleId;
          map.set(vid, (map.get(vid) || 0) + c.approvedAmount);
        }
      });
    return map;
  });

  // All transactions (for the Payments tab)
  allTransactions = computed(() => {
    let txns: any[] = [];

    // Premium payments + claim payout records from payments table
    this.payments().forEach((p: any) => {
      const isClaimPayout = this.claimPaymentIds().has(p.paymentId);
      const policy = this.policies().find((pol: any) => pol.policyId === p.policyId);
      // For claim payout entries, look up the createdAt from the matching claim
      let date = p.paymentDate;
      if (isClaimPayout && !date) {
        const matchingClaim = this.claims().find((c: any) =>
          c.policyId === p.policyId && c.approvedAmount === p.amount
        );
        date = matchingClaim?.createdAt || null;
      }
      txns.push({
        id: p.paymentId,
        policyId: p.policyId,
        vehicle: policy?.vehicle ? `${policy.vehicle.make || ''} ${policy.vehicle.model || ''} (${policy.vehicle.registrationNumber || ''})` : 'N/A',
        customer: policy?.customer?.fullName || 'N/A',
        date,
        amount: p.amount,
        status: p.status,
        isCredit: !isClaimPayout,
        type: isClaimPayout ? 'Claim Payout' : 'Premium Payment',
        transactionRef: p.transactionReference || ''
      });
    });

    return txns.sort((a, b) => {
      if (!a.date) return 1;
      if (!b.date) return -1;
      return new Date(b.date).getTime() - new Date(a.date).getTime();
    });
  });


  userRoleFilter = signal<string>('All');
  filteredUsers = computed(() => {
    const mappedUsers = this.users().map(u => {
      // Use nullish coalescing (??) because role constant 0 (Admin) is falsy in JS
      const roleVal = u.role ?? u.Role ?? u.roles;
      return { ...u, displayRole: this.getRoleString(roleVal) };
    });
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

  // ── Plan Analytics Charts ────────────────────────────────────────────────
  planAnalyticsOptions = computed<ChartConfiguration['options']>(() => ({
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: 'y' as const,
    scales: {
      x: {
        beginAtZero: true,
        stacked: false,
        grid: { color: this.isDarkMode() ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)' },
        ticks: {
          color: this.isDarkMode() ? '#94A3B8' : '#475569',
          callback: (v: any) => '₹' + (v >= 1_00_000 ? (v / 1_00_000).toFixed(1) + 'L' : v >= 1000 ? (v / 1000).toFixed(0) + 'K' : v)
        },
        border: { display: false }
      },
      y: {
        grid: { display: false },
        ticks: { color: this.isDarkMode() ? '#CBD5E1' : '#1E293B', font: { weight: '600' as const } },
        border: { display: false }
      }
    },
    plugins: {
      legend: {
        position: 'top',
        labels: {
          color: this.isDarkMode() ? '#E2E8F0' : '#1E293B',
          padding: 16,
          usePointStyle: true,
          pointStyle: 'circle',
          font: { size: 12, family: "'Inter', sans-serif" }
        }
      },
      tooltip: {
        backgroundColor: this.isDarkMode() ? 'rgba(15,26,54,0.95)' : 'rgba(255,255,255,0.97)',
        titleColor: this.isDarkMode() ? '#5BC0BE' : '#2563EB',
        bodyColor: this.isDarkMode() ? '#F1F5F9' : '#0F172A',
        borderColor: this.isDarkMode() ? 'rgba(91,192,190,0.3)' : 'rgba(37,99,235,0.2)',
        borderWidth: 1,
        padding: 12,
        callbacks: {
          label: (ctx: any) => {
            const val = ctx.parsed.x;
            const formatted = '₹' + val.toLocaleString('en-IN');
            return ` ${ctx.dataset.label}: ${formatted}`;
          }
        }
      }
    }
  } as any));

  planClaimsCountOptions = computed<ChartConfiguration['options']>(() => ({
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: 'y' as const,
    scales: {
      x: {
        beginAtZero: true,
        stacked: false,
        grid: { color: this.isDarkMode() ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.06)' },
        ticks: {
          color: this.isDarkMode() ? '#94A3B8' : '#475569',
          stepSize: 1,
          callback: (v: any) => Number.isInteger(v) ? v : ''
        },
        border: { display: false }
      },
      y: {
        grid: { display: false },
        ticks: { color: this.isDarkMode() ? '#CBD5E1' : '#1E293B', font: { weight: '600' as const } },
        border: { display: false }
      }
    },
    plugins: {
      legend: {
        position: 'top',
        labels: {
          color: this.isDarkMode() ? '#E2E8F0' : '#1E293B',
          padding: 16,
          usePointStyle: true,
          pointStyle: 'circle',
          font: { size: 12, family: "'Inter', sans-serif" }
        }
      },
      tooltip: {
        backgroundColor: this.isDarkMode() ? 'rgba(15,26,54,0.95)' : 'rgba(255,255,255,0.97)',
        titleColor: this.isDarkMode() ? '#5BC0BE' : '#2563EB',
        bodyColor: this.isDarkMode() ? '#F1F5F9' : '#0F172A',
        borderColor: this.isDarkMode() ? 'rgba(91,192,190,0.3)' : 'rgba(37,99,235,0.2)',
        borderWidth: 1,
        padding: 12,
        callbacks: {
          label: (ctx: any) => ` ${ctx.dataset.label}: ${ctx.parsed.x}`
        }
      }
    }
  } as any));

  planPremiumsChartData: ChartData<'bar'> = { labels: [], datasets: [] };
  planClaimsChartData: ChartData<'bar'> = { labels: [], datasets: [] };
  vehicleTypePremiumsChartData: ChartData<'bar'> = { labels: [], datasets: [] };
  vehicleTypeClaimsChartData: ChartData<'bar'> = { labels: [], datasets: [] };
  planBarChartType: ChartType = 'bar';

  // Modal / Form States
  isCreatingAgent = signal(false);
  isCreatingClaimsOfficer = signal(false);
  isCreatingPlan = signal(false);

  registerForm = { fullName: '', email: '', password: '', role: '' };

  vehicleCategories = ['Car', 'TwoWheeler', 'EVCar', 'EVTwoWheeler', 'HeavyVehicle', 'ThreeWheeler', 'EVThreeWheeler'];
  policyCategories = ['Comprehensive', 'ThirdParty', 'ZeroDepreciation'];

  planForm = {
    planName: '',
    policyType: '',
    basePremium: 0,
    policyDurationMonths: 12,
    deductibleAmount: 0,
    coversThirdParty: true,
    coversOwnDamage: true,
    coversTheft: true,
    zeroDepreciationAvailable: false,
    engineProtectionAvailable: false,
    roadsideAssistanceAvailable: false,
    applicableVehicleType: ''
  };

  errorMessage = signal('');
  successMessage = signal('');

  ngOnInit() {
    this.extractName();
    this.checkTheme();
    this.loadAllData();
  }

  loadAuditLogs() {
    this.adminService.getAuditLogs().subscribe(res => this.auditLogs.set(res));
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
    this.adminService.getAllPolicyPlans().subscribe(res => {
      this.plans.set(res);
      this.rebuildPlanCharts();
    });
    this.adminService.getAllPolicies().subscribe(res => {
      this.policies.set(res);
      this.rebuildPlanCharts();
    });
    this.loadAuditLogs();

    // Load payments and claims together so we can cross-reference claim payouts
    this.adminService.getAllPayments().subscribe(payments => {
      this.payments.set(payments);
      this.rebuildRevenueChart();
      this.rebuildPlanCharts();
    });

    this.adminService.getAllClaims().subscribe(res => {
      this.claims.set(res);
      this.updateClaimsChart(res);
      this.rebuildRevenueChart();
      this.rebuildPlanCharts();
    });
    this.adminService.getAllUsers().subscribe(res => this.users.set(res));
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
    const now = new Date();
    const currentYear = now.getFullYear();

    paymentData.forEach(p => {
      const status = String(p.status || '').toLowerCase();
      if (status === 'paid' || p.status === 1) {
        const date = new Date(p.paymentDate);
        if (date.getFullYear() === currentYear) {
          monthlyTotals[date.getMonth()] += (p.amount || 0);
        }
      }
    });

    this.revenueChartData = {
      labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
      datasets: [
        {
          data: monthlyTotals,
          label: `Monthly Revenue (${currentYear})`,
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

  rebuildPlanCharts() {
    const plans = this.plans() || [];
    const policies = this.policies() || [];
    const payments = this.payments() || [];
    const claims = this.claims() || [];

    if (!plans.length) return;

    // 1. Build Plan Attribute Maps (Normalizing names)
    // Use string keys to ensure consistency regardless of JSON numeric representation
    const planToTypeMap = new Map<string, string>();
    const planToVehicleMap = new Map<string, string>();

    plans.forEach(p => {
      const pId = String(p.planId || p.PlanId || '');
      if (!pId) return;

      const pType = p.policyType?.trim() || p.PolicyType?.trim() || p.planName?.trim() || p.PlanName?.trim() || 'General';
      const vType = p.applicableVehicleType?.trim() || p.ApplicableVehicleType?.trim() || 'General';

      planToTypeMap.set(pId, pType);
      planToVehicleMap.set(pId, vType);
    });

    // 2. Map Policy → Aggregation Keys
    const policyTypeMap = new Map<string, string>();
    const policyVehicleMap = new Map<string, string>();

    policies.forEach((pol: any) => {
      const polId = String(pol.policyId || pol.PolicyId || '');
      const planId = String(pol.planId || pol.PlanId || pol.policyPlan?.planId || '');

      if (!polId) return;

      const type = planToTypeMap.get(planId) || 'General';
      const vType = planToVehicleMap.get(planId) || 'General';

      policyTypeMap.set(polId, type);
      policyVehicleMap.set(polId, vType);
    });

    // 3. Aggregators
    const premByPType = new Map<string, number>();
    const payoutByPType = new Map<string, number>();
    const volByPType = new Map<string, number>();

    const premByVType = new Map<string, number>();
    const payoutByVType = new Map<string, number>();
    const volByVType = new Map<string, number>();

    // 4. Handle Claim Payout Exclusion
    const claimSignatures = new Set<string>();
    claims.filter((c: any) => {
      const status = String(c.status || c.Status || '').toLowerCase();
      return status === 'approved' || c.status === 1 || c.Status === 1;
    }).forEach((c: any) => {
      const polId = String(c.policyId || c.PolicyId || '');
      const amt = c.approvedAmount || c.ApprovedAmount;
      if (amt && polId) claimSignatures.add(`${polId}:${amt}`);
    });

    const claimPayoutIdSet = new Set<string>(); // Used strings for IDs
    payments.forEach((p: any) => {
      const polId = String(p.policyId || p.PolicyId || '');
      const pId = String(p.paymentId || p.PaymentId || '');
      if (claimSignatures.has(`${polId}:${p.amount}`)) claimPayoutIdSet.add(pId);
    });

    // 5. Aggregate Payment Income
    payments.forEach((p: any) => {
      const status = String(p.status || p.Status || '').toLowerCase();
      const pId = String(p.paymentId || p.PaymentId || '');
      const polId = String(p.policyId || p.PolicyId || '');

      if ((status === 'paid' || p.status === 1 || p.Status === 1) && !claimPayoutIdSet.has(pId)) {
        const pType = policyTypeMap.get(polId) || 'General';
        const vType = policyVehicleMap.get(polId) || 'General';

        premByPType.set(pType, (premByPType.get(pType) || 0) + (p.amount || 0));
        premByVType.set(vType, (premByVType.get(vType) || 0) + (p.amount || 0));
      }
    });

    // 6. Aggregate Claims Stats
    claims.forEach((c: any) => {
      const polId = String(c.policyId || c.PolicyId || '');
      const pType = policyTypeMap.get(polId) || 'General';
      const vType = policyVehicleMap.get(polId) || 'General';

      volByPType.set(pType, (volByPType.get(pType) || 0) + 1);
      volByVType.set(vType, (volByVType.get(vType) || 0) + 1);

      const status = String(c.status || c.Status || '').toLowerCase();
      if (status === 'approved' || c.status === 1 || c.Status === 1) {
        const amt = c.approvedAmount || c.ApprovedAmount;
        if (amt) {
          payoutByPType.set(pType, (payoutByPType.get(pType) || 0) + amt);
          payoutByVType.set(vType, (payoutByVType.get(vType) || 0) + amt);
        }
      }
    });

    // 7. Define Dynamic Labels
    const allPolicyTypes = Array.from(new Set([
      ...Array.from(planToTypeMap.values()),
      ...Array.from(premByPType.keys())
    ])).sort();

    const allVehicleTypes = Array.from(new Set([
      ...Array.from(planToVehicleMap.values()),
      ...Array.from(premByVType.keys())
    ])).sort();

    // 8. Final Chart Construction
    this.planPremiumsChartData = {
      labels: allPolicyTypes,
      datasets: [
        { label: 'Premiums Collected', data: allPolicyTypes.map(t => premByPType.get(t) || 0), backgroundColor: 'rgba(91,192,190,0.82)', borderRadius: 5, barThickness: 18 },
        { label: 'Claims Paid Out', data: allPolicyTypes.map(t => payoutByPType.get(t) || 0), backgroundColor: 'rgba(239,68,68,0.75)', borderRadius: 5, barThickness: 18 }
      ]
    };

    this.planClaimsChartData = {
      labels: allPolicyTypes,
      datasets: [
        { label: 'Claim Volume', data: allPolicyTypes.map(t => volByPType.get(t) || 0), backgroundColor: 'rgba(251,191,36,0.85)', borderRadius: 5, barThickness: 18 }
      ]
    };

    this.vehicleTypePremiumsChartData = {
      labels: allVehicleTypes,
      datasets: [
        { label: 'Premiums Collected', data: allVehicleTypes.map(t => premByVType.get(t) || 0), backgroundColor: 'rgba(59,130,246,0.82)', borderRadius: 5, barThickness: 18 },
        { label: 'Claims Paid Out', data: allVehicleTypes.map(t => payoutByVType.get(t) || 0), backgroundColor: 'rgba(239,68,68,0.75)', borderRadius: 5, barThickness: 18 }
      ]
    };

    this.vehicleTypeClaimsChartData = {
      labels: allVehicleTypes,
      datasets: [
        { label: 'Claim Volume', data: allVehicleTypes.map(t => volByVType.get(t) || 0), backgroundColor: 'rgba(168,85,247,0.85)', borderRadius: 5, barThickness: 18 }
      ]
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

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(this.registerForm.email)) {
      this.errorMessage.set('Please provide a valid email address.');
      return;
    }

    if (this.isCreatingAgent()) {
      this.adminService.createAgent(this.registerForm).subscribe({
        next: () => {
          this.successMessage.set('Agent Created Successfully!');
          this.isCreatingAgent.set(false);
          this.loadAllData();
          setTimeout(() => this.successMessage.set(''), 3000);
        },
        error: (err: any) => this.showError(err)
      });
    } else {
      this.adminService.createClaimsOfficer(this.registerForm).subscribe({
        next: () => {
          this.successMessage.set('Claims Officer Created Successfully!');
          this.isCreatingClaimsOfficer.set(false);
          this.loadAllData();
          setTimeout(() => this.successMessage.set(''), 3000);
        },
        error: (err: any) => this.showError(err)
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
      error: (err: any) => this.showError(err)
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
    this.planForm = {
      planName: '',
      policyType: '',
      basePremium: 0,
      policyDurationMonths: 12,
      deductibleAmount: 0,
      coversThirdParty: true,
      coversOwnDamage: true,
      coversTheft: true,
      zeroDepreciationAvailable: false,
      engineProtectionAvailable: false,
      roadsideAssistanceAvailable: false,
      applicableVehicleType: ''
    };
    this.isCreatingPlan.set(true);
  }

  private showError(err: any) {
    this.router.navigate(['/error'], {
      state: {
        status: err.status || 500,
        message: extractErrorMessage(err),
        title: 'Administrative Action Error'
      }
    });
  }
}
