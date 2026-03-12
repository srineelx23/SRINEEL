import { Component, Input, Output, EventEmitter, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-policies',
  standalone: true,
  imports: [CommonModule, FormsModule],
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

  @Output() onSetPolicyFilter = new EventEmitter<string>();
  @Output() onViewPolicyDetails = new EventEmitter<number>();
  @Output() onPayPremium = new EventEmitter<number>();
  @Output() onStartRenew = new EventEmitter<number>();
  @Output() onClosePolicyDetails = new EventEmitter<void>();
  @Output() onOpenTransferModal = new EventEmitter<number>();
  @Output() onCancelPolicy = new EventEmitter<number>();
  @Output() onSubmitRenew = new EventEmitter<void>();
  @Output() onCancelRenew = new EventEmitter<void>();
}
