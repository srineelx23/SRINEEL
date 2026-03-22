import { Component, Input, Output, EventEmitter, Signal, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { VimsFormatPipe } from '../../../utils/vims-format.pipe';

@Component({
  selector: 'app-policies',
  standalone: true,
  imports: [CommonModule, FormsModule, VimsFormatPipe],
  templateUrl: './policies.component.html',
  styleUrl: './policies.component.css',
})
export class PoliciesComponent {
  @Input() policyFilter!: Signal<string>;
  @Input() activePolicies!: Signal<any[]>;
  @Input() renewablePolicies!: Signal<any[]>;
  @Input() pendingPolicies!: Signal<any[]>;
  @Input() pendingPaymentPolicies!: Signal<any[]>;
  @Input() inactivePolicies!: Signal<any[]>;
  @Input() selectedPolicy!: Signal<any>;
  @Input() renewingPolicyId!: Signal<number | null>;
  @Input() renewForm!: any;
  @Input() filteredRenewPlans!: Signal<any[]>;

  // Dropdown States
  showPlanDropdown = signal(false);
  showYearsDropdown = signal(false);

  @Output() onSetPolicyFilter = new EventEmitter<string>();
  @Output() onViewPolicyDetails = new EventEmitter<number>();
  @Output() onPayPremium = new EventEmitter<number>();
  @Output() onStartRenew = new EventEmitter<number>();
  @Output() onClosePolicyDetails = new EventEmitter<void>();
  @Output() onOpenTransferModal = new EventEmitter<number>();
  @Output() onCancelPolicy = new EventEmitter<number>();
  @Output() onSubmitRenew = new EventEmitter<void>();
  @Output() onCancelRenew = new EventEmitter<void>();
  @Output() onDownloadPolicyContract = new EventEmitter<number>();

  selectPlan(planId: number) {
    this.renewForm.NewPlanId = planId;
    this.showPlanDropdown.set(false);
  }

  selectYears(years: number) {
    this.renewForm.SelectedYears = years;
    this.showYearsDropdown.set(false);
  }

  getPlanName(planId: any) {
    if (!planId) return 'Upgrade / Change Plan';
    const plan = this.filteredRenewPlans().find(p => p.planId == planId || p.id == planId);
    return plan ? plan.planName : 'Upgrade / Change Plan';
  }
}
