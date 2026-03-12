import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-initiate-transfer-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './initiate-transfer-modal.component.html',
  styleUrl: './initiate-transfer-modal.component.css'
})
export class InitiateTransferModalComponent {
  policyId = input.required<number | null>();
  error = input<string>('');
  success = input<string>('');
  
  recipientEmail = signal('');

  onClose = output<void>();
  onInitiate = output<string>();

  handleInitiate() {
    this.onInitiate.emit(this.recipientEmail().trim());
  }

  handleClose() {
    this.onClose.emit();
  }
}
