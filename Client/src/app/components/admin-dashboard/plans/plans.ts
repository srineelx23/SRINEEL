import { Component, input, output, model, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-plans',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './plans.html',
  styleUrl: './plans.css',
})
export class PlansComponent {
  orderedPlans = input.required<any[]>();
  isCreatingPlan = input.required<boolean>();
  planForm = model.required<any>();
  
  vehicleCategories = input.required<string[]>();
  policyCategories = input.required<string[]>();

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
}
