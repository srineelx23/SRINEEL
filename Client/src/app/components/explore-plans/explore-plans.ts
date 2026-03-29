import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { CustomerService } from '../../services/customer.service';
import { AuthService } from '../../services/auth.service';
import { ThemeService } from '../../services/theme.service';
import { VimsFormatPipe } from '../../utils/vims-format.pipe';

@Component({
  selector: 'app-explore-plans',
  standalone: true,
  imports: [CommonModule, FormsModule, VimsFormatPipe],
  templateUrl: './explore-plans.html',
  styleUrl: './explore-plans.css'
})
export class ExplorePlans implements OnInit {
  protected readonly themeService = inject(ThemeService);
  // Data stores
  plans = signal<any[]>([]);
  filteredPlans = signal<any[]>([]);

  // Selection Computed
  selectedPlanDetails = computed(() => {
    const id = this.selectedPlanForQuote();
    return this.plans().find(p => p.planId === id) || null;
  });

  isEVPlan = computed(() => {
    const plan = this.selectedPlanDetails();
    const applicableType = (plan?.applicableVehicleType || '').toString().toLowerCase();
    return applicableType.includes('ev');
  });

  // Filter State
  searchQuery = signal('');
  filterVehicleType = signal('');
  filterMaxPremium = signal<number | null>(null);

  vehicleCategories = ['Car', 'TwoWheeler', 'ThreeWheeler', 'EVCar', 'EVTwoWheeler', 'EVThreeWheeler', 'HeavyVehicle'];

  // Feature filters
  filterThirdParty = signal(false);
  filterOwnDamage = signal(false);
  filterTheft = signal(false);
  filterZeroDep = signal(false);
  filterRoadside = signal(false);

  // Quote State
  selectedPlanForQuote = signal<number | null>(null);
  quoteForm = {
    InvoiceAmount: null as number | null,
    ManufactureYear: new Date().getFullYear(),
    FuelType: 'Petrol',
    VehicleType: 'Private',
    KilometersDriven: null as number | null,
    PolicyYears: 1,
    PlanId: null as number | null
  };
  calculatedQuote = signal<any>(null);
  isLoggedIn = signal(false);
  userName = signal<string | null>(null);
  userRole = signal<string | null>(null);
  showDropdown = false;
  showVehicleDropdown = signal(false);

  // Custom Dropdown States
  showFuelDropdown = signal(false);
  showUsageDropdown = signal(false);
  showDurationDropdown = signal(false);
  showDirectFuelDropdown = signal(false);
  showDirectUsageDropdown = signal(false);
  showDirectDurationDropdown = signal(false);

  // Application State
  currentIntent = signal<'quote' | 'apply' | null>(null);
  isApplying = signal(false);
  applicationForm = {
    RegistrationNumber: '',
    Make: '',
    Model: ''
  };
  currentYear = new Date().getFullYear();
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

    const role = this.authService.getRoleFromStoredToken();
    if (role === 'ClaimsOfficer') this.userRole.set('Claims Officer');
    else if (role === 'Admin') this.userRole.set('Executive Admin');
    else this.userRole.set(role);
  }

  applyFilters() {
    let results = this.plans().filter(p => p.status === 1 || p.status === 'Active');

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
    
    // Get top 3 plans by purchase count (only those with at least 1 purchase)
    const top3Ids = [...this.plans()]
      .filter(p => (p.purchaseCount || 0) > 0)
      .sort((a, b) => (b.purchaseCount || 0) - (a.purchaseCount || 0))
      .slice(0, 3)
      .map(p => p.planId);
    
    results.forEach(p => {
      p.isPopular = top3Ids.includes(p.planId);
    });
  }

  updateFilter(event: any, type: string) {
    const val = event.target.value;
    if (type === 'search') this.searchQuery.set(val);
    if (type === 'vehicle') this.filterVehicleType.set(val);
    if (type === 'premium') this.filterMaxPremium.set(val ? Number(val) : null);
    this.applyFilters();
  }

  setFilterDirect(val: string, type: string) {
    if (type === 'vehicle') {
      this.filterVehicleType.set(val);
      this.showVehicleDropdown.set(false);
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
    this.setupPlanForm(planId);
    this.currentIntent.set('quote');
    this.isApplying.set(false);
    this.calculatedQuote.set(null);
  }

  onBuyNow(planId: number) {
    if (!this.checkAuth(planId)) return;
    this.setupPlanForm(planId);
    this.currentIntent.set('apply');
    this.isApplying.set(true);
    this.calculatedQuote.set(null);
  }

  private setupPlanForm(planId: number) {
    this.selectedPlanForQuote.set(planId);
    this.quoteForm.PlanId = planId;

    if (this.isEVPlan()) {
      this.quoteForm.FuelType = 'EV';
      this.showFuelDropdown.set(false);
      this.showDirectFuelDropdown.set(false);
      return;
    }

    this.quoteForm.FuelType = this.quoteForm.FuelType || 'Petrol';
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
    this.currentIntent.set(null);
    this.calculatedQuote.set(null);
    this.isApplying.set(false);
    this.invoiceFile = null;
    this.rcFile = null;

    this.quoteForm = {
      InvoiceAmount: null as number | null,
      ManufactureYear: new Date().getFullYear(),
      FuelType: 'Petrol',
      VehicleType: 'Private',
      KilometersDriven: null as number | null,
      PolicyYears: 1,
      PlanId: null as number | null
    };

    this.applicationForm = {
      RegistrationNumber: '',
      Make: '',
      Model: ''
    };
  }

  onQuoteChange() {
    if (this.currentIntent() === 'quote' || this.currentIntent() === 'apply') {
        const p = this.quoteForm;
        if (!p.InvoiceAmount || !p.PlanId || p.KilometersDriven === null || p.KilometersDriven === undefined || p.KilometersDriven < 0 || p.KilometersDriven > 999999) {
            this.calculatedQuote.set(null);
            return;
        }
        const currentYear = new Date().getFullYear();
        if (currentYear - Number(p.ManufactureYear) > 15 || Number(p.ManufactureYear) < 0 || Number(p.ManufactureYear) > currentYear) {
            this.calculatedQuote.set(null);
            return;
        }

        const payload = {
          InvoiceAmount: Number(p.InvoiceAmount),
          ManufactureYear: Number(p.ManufactureYear),
          FuelType: p.FuelType,
          VehicleType: p.VehicleType,
          KilometersDriven: Number(p.KilometersDriven),
          PolicyYears: Number(p.PolicyYears),
          PlanId: Number(p.PlanId)
        };

        this.customerService.calculateQuote(payload).subscribe({
          next: (res) => {
            this.calculatedQuote.set(res);
          },
          error: (err: any) => {
             this.calculatedQuote.set(null);
          }
        });
    }
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

    const currentYear = new Date().getFullYear();
    if (currentYear - payload.ManufactureYear > 15) {
      this.errorMessage.set("Cannot buy insurance for vehicles aged greater than 15 years");
      this.autoHideToast();
      return;
    }

    if (payload.InvoiceAmount < 0 || payload.KilometersDriven < 0 || payload.KilometersDriven > 999999 || payload.ManufactureYear < 0 || payload.ManufactureYear > currentYear) {
      this.router.navigate(['/error'], {
        state: { status: 400, message: "Please enter valid field values.", title: 'Validation Error' }
      });
      return;
    }

    this.customerService.calculateQuote(payload).subscribe({
      next: (res) => {
        this.calculatedQuote.set(res);
      },
      error: (err: any) => {
        this.router.navigate(['/error'], {
          state: { status: err.status, message: err.error?.message || "Failed to calculate quote.", title: 'Calculation Error' }
        });
      }
    });
  }

  startApplication() {
    this.currentIntent.set('apply');
    this.isApplying.set(true);
  }

  backToQuote() {
    this.currentIntent.set('quote');
    this.isApplying.set(false);
    this.onQuoteChange();
  }

  onFileChange(event: any, field: 'invoice' | 'rc') {
    const file = event.target.files[0];
    if (field === 'invoice') this.invoiceFile = file;
    if (field === 'rc') this.rcFile = file;
  }

  viewUploadedDocument(field: 'invoice' | 'rc') {
    const file = field === 'invoice' ? this.invoiceFile : this.rcFile;
    if (!file) {
      this.errorMessage.set(field === 'invoice' ? 'Please upload an invoice document first.' : 'Please upload an RC document first.');
      this.autoHideToast();
      return;
    }

    const objectUrl = URL.createObjectURL(file);

    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.target = '_blank';
    anchor.rel = 'noopener noreferrer';
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);

    setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
  }

  submitApplication() {
    this.errorMessage.set('');
    this.successMessage.set('');

    const validationErrors = this.getApplicationValidationErrors();
    if (validationErrors.length > 0) {
      this.errorMessage.set(validationErrors.join(' | '));
      this.autoHideToast();
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

    formData.append('InvoiceDocument', this.invoiceFile!);
    formData.append('RcDocument', this.rcFile!);

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
        const apiMessage = this.extractApiErrorMessage(err);
        if (err?.status === 400 && apiMessage) {
          this.errorMessage.set(apiMessage.replace(/\s*\|\s*/g, ' | '));
          this.autoHideToast();
          return;
        }

        this.router.navigate(['/error'], {
          state: {
            status: err.status,
            message: apiMessage || 'Submission failed.',
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

  private extractApiErrorMessage(err: any): string {
    if (!err) {
      return '';
    }

    if (typeof err.error === 'string') {
      const raw = err.error.trim();
      if (raw.startsWith('{') && raw.endsWith('}')) {
        try {
          const parsed = JSON.parse(raw);
          return parsed?.message || raw;
        } catch {
          return raw;
        }
      }
      return raw;
    }

    if (typeof err.error === 'object' && err.error?.message) {
      return err.error.message;
    }

    return err.message || '';
  }

  private getApplicationValidationErrors(): string[] {
    const errors: string[] = [];
    const currentYear = new Date().getFullYear();

    const registration = (this.applicationForm.RegistrationNumber || '').trim();
    const make = (this.applicationForm.Make || '').trim();
    const model = (this.applicationForm.Model || '').trim();

    const planId = Number(this.quoteForm.PlanId);
    const invoiceAmount = Number(this.quoteForm.InvoiceAmount);
    const kilometersDriven = Number(this.quoteForm.KilometersDriven);
    const manufactureYear = Number(this.quoteForm.ManufactureYear);
    const policyYears = Number(this.quoteForm.PolicyYears);
    const fuelType = (this.quoteForm.FuelType || '').trim().toLowerCase();
    const vehicleType = (this.quoteForm.VehicleType || '').trim().toLowerCase();

    if (!planId || planId <= 0) {
      errors.push('Please select a valid plan.');
    }

    if (!registration) {
      errors.push('Registration number is required.');
    } else {
      const normalizedReg = registration.toUpperCase().replace(/\s|-/g, '');
      const regRegex = /^[A-Z]{2}[A-Z0-9]{1,3}[A-Z]{1,3}\d{1,4}$/;
      if (!regRegex.test(normalizedReg)) {
        errors.push('Invalid vehicle registration number format.');
      }
    }

    if (!make || make.length < 2 || make.length > 60) {
      errors.push('Vehicle make must be between 2 and 60 characters.');
    }

    if (!model || model.length > 80) {
      errors.push('Vehicle model is required and must not exceed 80 characters.');
    }

    if (!Number.isFinite(manufactureYear) || manufactureYear < 1980 || manufactureYear > currentYear) {
      errors.push(`Manufacture year must be between 1980 and ${currentYear}.`);
    }

    if (Number.isFinite(manufactureYear) && currentYear - manufactureYear > 15) {
      errors.push('Cannot buy insurance for vehicles aged greater than 15 years.');
    }

    if (!Number.isFinite(invoiceAmount) || invoiceAmount <= 0) {
      errors.push('Invoice amount must be greater than 0.');
    }

    if (!Number.isFinite(kilometersDriven) || kilometersDriven < 0 || kilometersDriven > 999999) {
      errors.push('Kilometers driven must be between 0 and 999999.');
    }

    if (!Number.isFinite(policyYears) || policyYears < 1 || policyYears > 5) {
      errors.push('Policy duration must be between 1 and 5 years.');
    }

    const validFuelTypes = ['petrol', 'diesel', 'hybrid', 'ev', 'cng'];
    if (!validFuelTypes.includes(fuelType)) {
      errors.push('Fuel type must be one of: Petrol, Diesel, Hybrid, EV, CNG.');
    }

    if (!(vehicleType === 'private' || vehicleType === 'commercial')) {
      errors.push('Vehicle usage type must be Private or Commercial.');
    }

    if (this.isEVPlan() && fuelType !== 'ev') {
      errors.push('Selected EV plan requires EV fuel type.');
    }

    if (!this.invoiceFile || !this.rcFile) {
      errors.push('Please upload both Invoice and RC documents.');
    } else {
      this.validatePdfFile(this.invoiceFile, 'Invoice document', errors);
      this.validatePdfFile(this.rcFile, 'RC document', errors);
    }

    return errors;
  }

  private validatePdfFile(file: File, label: string, errors: string[]): void {
    if (!file) {
      errors.push(`${label} is required.`);
      return;
    }

    const fileName = (file.name || '').toLowerCase();
    const fileType = (file.type || '').toLowerCase();
    const isPdf = fileName.endsWith('.pdf') && (fileType === 'application/pdf' || fileType === 'application/x-pdf' || fileType === '');
    if (!isPdf) {
      errors.push(`${label} must be a PDF file only.`);
    }

    const maxSize = 10 * 1024 * 1024;
    if (file.size <= 0) {
      errors.push(`${label} is empty.`);
    } else if (file.size > maxSize) {
      errors.push(`${label} exceeds maximum size of 10 MB.`);
    }
  }

  logout() {
    this.authService.logout();
    this.isLoggedIn.set(false);
    this.userName.set(null);
  }

}

