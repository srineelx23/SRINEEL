import { Component, input, output, model, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { VimsFormatPipe } from '../../../utils/vims-format.pipe';

@Component({
  selector: 'app-plans',
  standalone: true,
  imports: [CommonModule, FormsModule, VimsFormatPipe],
  templateUrl: './plans.html',
  styleUrl: './plans.css',
})
export class PlansComponent {
  orderedPlans = input.required<any[]>();
  isCreatingPlan = input.required<boolean>();
  planForm = model.required<any>();
  
  vehicleCategories = input.required<string[]>();
  policyCategories = input.required<string[]>();

  planVehicleTypeFilter = model.required<string>();
  planTypeFilter = model.required<string>();
  planStatusFilter = model.required<'Active' | 'Inactive'>();
  plansSortOption = model.required<string>();
  showPlansSortDropdown = model.required<boolean>();
  
  showVehicleTypeDropdown = signal(false);
  showPlanTypeDropdown = signal(false);
  showFormPolicyTypeDropdown = signal(false);
  showFormVehicleTypeDropdown = signal(false);

  onOpenPlanForm = output<void>();
  onSubmitPlanRegistration = output<void>();
  onTogglePlanStatus = output<{ planId: number, status: number }>();
  onSwitchTab = output<string>();

  openPlanForm() {
    this.onOpenPlanForm.emit();
  }

  submitPlanRegistration() {
    this.onSubmitPlanRegistration.emit();
  }

  togglePlanStatus(planId: number, status: number) {
    this.onTogglePlanStatus.emit({ planId, status });
  }

  cancelCreation() {
    this.onSwitchTab.emit('plans');
  }

  getSortLabel(option: string): string {
    switch (option) {
      case 'popularity': return 'Most Popular';
      case 'coverageDesc': return 'Coverage: High to Low';
      case 'coverageAsc': return 'Coverage: Low to High';
      case 'premiumDesc': return 'Premium: High to Low';
      case 'premiumAsc': return 'Premium: Low to High';
      default: return 'Most Popular';
    }
  }
}
