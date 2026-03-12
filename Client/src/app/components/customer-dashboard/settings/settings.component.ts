import { Component, Input, Output, EventEmitter, Signal, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css',
})
export class SettingsComponent {
  @Input() customerName!: Signal<string>;
  @Input() changePasswordForm!: any;
  @Input() changePwdLoading!: Signal<boolean>;

  @Output() onChangePassword = new EventEmitter<void>();

  showCurrentPwd = signal(false);
  showNewPwd = signal(false);
  showConfirmPwd = signal(false);
}
