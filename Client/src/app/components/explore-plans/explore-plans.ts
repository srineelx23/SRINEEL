import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { CustomerService } from '../../services/customer.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-explore-plans',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './explore-plans.html',
  styleUrl: './explore-plans.css'
})
export class ExplorePlans implements OnInit {
  // Data stores
  plans = signal<any[]>([]);
  filteredPlans = signal<any[]>([]);

  // Selection Computed
  selectedPlanDetails = computed(() => {
    const id = this.selectedPlanForQuote();
    return this.plans().find(p => p.planId === id) || null;
  });

  // Filter State
  searchQuery = signal('');
  filterVehicleType = signal('');
  filterPolicyType = signal('');
  filterMaxPremium = signal<number | null>(null);

  // Feature filters
  filterThirdParty = signal(false);
  filterOwnDamage = signal(false);
  filterTheft = signal(false);
  filterZeroDep = signal(false);
  filterRoadside = signal(false);

  // Quote State
  selectedPlanForQuote = signal<number | null>(null);
  quoteForm = {
    InvoiceAmount: null,
    ManufactureYear: new Date().getFullYear(),
    FuelType: 'Petrol',
    VehicleType: 'Private',
    KilometersDriven: null,
    PolicyYears: 1,
    PlanId: null as number | null
  };
  calculatedQuote = signal<any>(null);
  isLoggedIn = signal(false);
  userName = signal<string | null>(null);
  showDropdown = false;
  showVehicleDropdown = signal(false);
  showPolicyDropdown = signal(false);

  // Application State
  isApplying = signal(false);
  applicationForm = {
    RegistrationNumber: '',
    Make: '',
    Model: ''
  };
  invoiceFile: File | null = null;
  rcFile: File | null = null;

  errorMessage = signal('');
  successMessage = signal('');

  private customerService = inject(CustomerService);
  public authService = inject(AuthService);
  public router = inject(Router);
  public route = inject(ActivatedRoute);

  ngOnInit() {
    this.loadPlans();

    // Check for stored intent after login via query param
    this.route.queryParamMap.subscribe(params => {
      const savedPlanId = params.get('open_quote');
      if (savedPlanId) {
        this.onGetQuote(Number(savedPlanId));
      }
    });
  }

  loadPlans() {
    this.customerService.getAllPolicyPlans().subscribe({
      next: (res) => {
        this.plans.set(res);
        this.applyFilters();
      },
      error: (err) => console.error(err)
    });
    this.isLoggedIn.set(this.authService.isLoggedIn());
    this.userName.set(this.authService.getUserName());
  }

  applyFilters() {
    let results = this.plans();

    // Text search
    if (this.searchQuery()) {
      results = results.filter(p =>
        p.planName.toLowerCase().includes(this.searchQuery().toLowerCase()) ||
        (p.description && p.description.toLowerCase().includes(this.searchQuery().toLowerCase()))
      );
    }

    // Vehicle Type
    if (this.filterVehicleType()) {
      results = results.filter(p => p.applicableVehicleType === this.filterVehicleType());
    }

    // Policy Type
    if (this.filterPolicyType()) {
      results = results.filter(p => p.policyType === this.filterPolicyType());
    }

    // Premium range
    if (this.filterMaxPremium()) {
      results = results.filter(p => p.basePremium <= (this.filterMaxPremium() || 0));
    }

    // Booleans
    if (this.filterThirdParty()) results = results.filter(p => p.coversThirdParty);
    if (this.filterOwnDamage()) results = results.filter(p => p.coversOwnDamage);
    if (this.filterTheft()) results = results.filter(p => p.coversTheft);
    if (this.filterZeroDep()) results = results.filter(p => p.zeroDepreciationAvailable);
    if (this.filterRoadside()) results = results.filter(p => p.roadsideAssistanceAvailable);

    this.filteredPlans.set(results);
  }

  updateFilter(event: any, type: string) {
    const val = event.target.value;
    if (type === 'search') this.searchQuery.set(val);
    if (type === 'vehicle') this.filterVehicleType.set(val);
    if (type === 'policy') this.filterPolicyType.set(val);
    if (type === 'premium') this.filterMaxPremium.set(val ? Number(val) : null);
    this.applyFilters();
  }

  setFilterDirect(val: string, type: string) {
    if (type === 'vehicle') {
      this.filterVehicleType.set(val);
      this.showVehicleDropdown.set(false);
    }
    if (type === 'policy') {
      this.filterPolicyType.set(val);
      this.showPolicyDropdown.set(false);
    }
    this.applyFilters();
  }

  toggleFeatureFilter(feature: string) {
    if (feature === 'thirdParty') this.filterThirdParty.set(!this.filterThirdParty());
    if (feature === 'ownDamage') this.filterOwnDamage.set(!this.filterOwnDamage());
    if (feature === 'theft') this.filterTheft.set(!this.filterTheft());
    if (feature === 'zeroDep') this.filterZeroDep.set(!this.filterZeroDep());
    if (feature === 'roadside') this.filterRoadside.set(!this.filterRoadside());
    this.applyFilters();
  }

  resetFilters() {
    this.searchQuery.set('');
    this.filterVehicleType.set('');
    this.filterPolicyType.set('');
    this.filterMaxPremium.set(null);
    this.filterThirdParty.set(false);
    this.filterOwnDamage.set(false);
    this.filterTheft.set(false);
    this.filterZeroDep.set(false);
    this.filterRoadside.set(false);
    this.applyFilters();
  }

  goHome() {
    this.router.navigate(['/']);
  }

  goToDashboard() {
    const role = this.authService.getRoleFromStoredToken();
    if (role === 'Customer') this.router.navigate(['/customer-dashboard']);
    else if (role === 'Admin') this.router.navigate(['/admin-dashboard']);
    else if (role === 'Agent') this.router.navigate(['/agent-dashboard']);
    else if (role === 'ClaimsOfficer' || role === 'Claims') this.router.navigate(['/claims-dashboard']);
  }

  // --- Quotes & Applications ---
  onGetQuote(planId: number) {
    if (!this.checkAuth(planId)) return;
    this.selectedPlanForQuote.set(planId);
    this.quoteForm.PlanId = planId;
    this.isApplying.set(false);
    this.calculatedQuote.set(null);
  }

  onBuyNow(planId: number) {
    if (!this.checkAuth(planId)) return;
    this.selectedPlanForQuote.set(planId);
    this.quoteForm.PlanId = planId;
    this.isApplying.set(true);
    this.calculatedQuote.set(null);
  }

  private checkAuth(planId: number): boolean {
    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login'], { queryParams: { quote_intent: planId } });
      return false;
    }

    const role = this.authService.getRoleFromStoredToken();
    const restrictedRoles = ['Admin', 'Agent', 'ClaimsOfficer', 'Claims'];

    if (restrictedRoles.includes(role || '')) {
      this.router.navigate(['/error'], {
        state: {
          status: 403,
          message: "Staff members (Admin/Agent/Claims) cannot purchase policies. Please register as a customer.",
          title: 'Access Restricted'
        }
      });
      return false;
    }
    return true;
  }

  cancelQuoteProcess() {
    this.selectedPlanForQuote.set(null);
    this.calculatedQuote.set(null);
    this.isApplying.set(false);
    this.invoiceFile = null;
    this.rcFile = null;
  }

  calculateQuote() {
    this.errorMessage.set('');

    const payload = {
      InvoiceAmount: Number(this.quoteForm.InvoiceAmount),
      ManufactureYear: Number(this.quoteForm.ManufactureYear),
      FuelType: this.quoteForm.FuelType,
      VehicleType: this.quoteForm.VehicleType,
      KilometersDriven: Number(this.quoteForm.KilometersDriven),
      PolicyYears: Number(this.quoteForm.PolicyYears),
      PlanId: Number(this.quoteForm.PlanId)
    };

    if (!payload.InvoiceAmount || !payload.PlanId) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: "Please enter the Invoice Amount and select a Plan.", title: 'Quotation Error' }
      });
      return;
    }

    this.customerService.calculateQuote(payload).subscribe({
      next: (res) => {
        this.calculatedQuote.set(res);
      },
      error: (err: any) => {
        // Interceptor will handle the redirect, but we can set a specific title here if we want
        this.router.navigate(['/error'], {
          state: { status: err.status, message: err.error?.message || "Failed to calculate quote.", title: 'Calculation Error' }
        });
      }
    });
  }

  startApplication() {
    this.isApplying.set(true);
  }

  onFileChange(event: any, field: 'invoice' | 'rc') {
    const file = event.target.files[0];
    if (field === 'invoice') this.invoiceFile = file;
    if (field === 'rc') this.rcFile = file;
  }

  submitApplication() {
    this.errorMessage.set('');
    this.successMessage.set('');

    if (!this.applicationForm.RegistrationNumber || !this.applicationForm.Make || !this.applicationForm.Model) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: "Please fill in all vehicle details.", title: 'Application Error' }
      });
      return;
    }

    if (!this.invoiceFile || !this.rcFile) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: "Please upload both the Invoice and RC documents.", title: 'Missing Documents' }
      });
      return;
    }

    const formData = new FormData();
    formData.append('PlanId', String(this.quoteForm.PlanId));
    formData.append('RegistrationNumber', this.applicationForm.RegistrationNumber);
    formData.append('Make', this.applicationForm.Make);
    formData.append('Model', this.applicationForm.Model);
    formData.append('Year', String(this.quoteForm.ManufactureYear));
    formData.append('FuelType', this.quoteForm.FuelType);
    formData.append('VehicleType', this.quoteForm.VehicleType);
    formData.append('KilometersDriven', String(this.quoteForm.KilometersDriven));
    formData.append('PolicyYears', String(this.quoteForm.PolicyYears));
    formData.append('InvoiceAmount', String(this.quoteForm.InvoiceAmount));

    formData.append('InvoiceDocument', this.invoiceFile);
    formData.append('RcDocument', this.rcFile);

    this.customerService.createVehicleApplication(formData).subscribe({
      next: (res) => {
        this.successMessage.set("Application submitted successfully! Redirecting...");
        this.isApplying.set(false);
        this.calculatedQuote.set(null);
        setTimeout(() => {
          this.successMessage.set('');
          this.router.navigate(['/customer-dashboard']);
        }, 3000);
      },
      error: (err: any) => {
        this.router.navigate(['/error'], {
          state: {
            status: err.status,
            message: err.error?.message || typeof err.error === 'string' ? err.error : "Submission failed.",
            title: 'Submission Error'
          }
        });
      }
    });
  }

  private autoHideToast() {
    setTimeout(() => {
      this.errorMessage.set('');
    }, 5000);
  }

  logout() {
    this.authService.logout();
    this.isLoggedIn.set(false);
    this.userName.set(null);
  }
}
