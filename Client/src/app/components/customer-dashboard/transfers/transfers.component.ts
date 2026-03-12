import { Component, Input, Output, EventEmitter, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-transfers',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './transfers.component.html',
  styleUrl: './transfers.component.css',
})
export class TransfersComponent {
  @Input() incomingTransfers!: Signal<any[]>;
  @Input() outgoingTransfers!: Signal<any[]>;

  @Output() onAcceptTransfer = new EventEmitter<any>();
  @Output() onRejectTransfer = new EventEmitter<number>();

  trackByTransferId(index: number, t: any): any {
    return t.policyTransferId || index;
  }

  onDeclineTransfer(transfer: any) {
    if (transfer.policyTransferId) {
      this.onRejectTransfer.emit(transfer.policyTransferId);
    }
  }
}
