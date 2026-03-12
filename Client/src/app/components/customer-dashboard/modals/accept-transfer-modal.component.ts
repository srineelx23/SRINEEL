import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-accept-transfer-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './accept-transfer-modal.component.html',
  styleUrl: './accept-transfer-modal.component.css'
})
export class AcceptTransferModalComponent {
  transfer = input.required<any>();
  rcFile = signal<File | null>(null);

  onClose = output<void>();
  onAccept = output<File>();

  onFileChange(event: any) {
    const file = event.target.files[0] || null;
    this.rcFile.set(file);
  }

  handleAccept() {
    const file = this.rcFile();
    if (file) {
      this.onAccept.emit(file);
    }
  }

  handleClose() {
    this.onClose.emit();
  }
}
