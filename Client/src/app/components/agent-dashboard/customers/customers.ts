import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './customers.html',
  styleUrl: './customers.css'
})
export class CustomersComponent {
  customers = input.required<any[]>();
  sortedCustomers = input.required<any[]>();
  selectedCustomerRecord = input.required<any>();
  customersSortOption = input.required<string>();
  showCustomersSortDropdown = signal(false);

  onOpenCustomerDetails = output<any>();
  onCloseCustomerDetails = output<void>();
  onCustomersSortOptionChange = output<string>();

  openCustomerDetails(customer: any) {
    this.onOpenCustomerDetails.emit(customer);
  }

  closeCustomerDetails() {
    this.onCloseCustomerDetails.emit();
  }

  setCustomersSortOption(option: string) {
    this.onCustomersSortOptionChange.emit(option);
    this.showCustomersSortDropdown.set(false);
  }

  getSortLabel(option: string): string {
    switch (option) {
      case 'nameAsc': return 'Name A-Z';
      case 'nameDesc': return 'Name Z-A';
      case 'amountDesc': return 'Premium: High to Low';
      case 'amountAsc': return 'Premium: Low to High';
      default: return 'Name A-Z';
    }
  }
}
