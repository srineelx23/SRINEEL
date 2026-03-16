import { Component, Input, Output, EventEmitter, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-overview',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './overview.component.html',
  styleUrl: './overview.component.css',
})
export class OverviewComponent {
  @Input() customerName!: Signal<string>;
  @Input() policies!: Signal<any[]>;
  @Input() activePolicies!: Signal<any[]>;
  @Input() pendingApplicationsCount!: Signal<number>;
  @Input() pendingPaymentPolicies!: Signal<any[]>;
  @Input() pendingClaimsList!: Signal<any[]>;
  @Input() approvedClaims!: Signal<any[]>;
  @Input() myVehicles!: Signal<any[]>;

  @Output() onSwitchTab = new EventEmitter<string>();
  @Output() onSetPolicyFilter = new EventEmitter<string>();
  @Output() onRoadsideAssistance = new EventEmitter<string>();

  handlePendingPayment() {
    this.onSwitchTab.emit('policies');
    this.onSetPolicyFilter.emit('PendingPayment');
  }

  requestRoadside(vehicleReg: string) {
    this.onRoadsideAssistance.emit(vehicleReg);
  }
}
