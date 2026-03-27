import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-rules',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './rules.html',
  styleUrls: ['./rules.css']
})
export class RulesComponent {
  activeCategory = signal<string>('policy');

  categories = [
    { id: 'policy', label: 'Policy & Coverage', icon: 'fa-shield-halved' },
    { id: 'claims', label: 'Claims & Settlement', icon: 'fa-file-invoice-dollar' },
    { id: 'fraud', label: 'AI & Fraud Governance', icon: 'fa-microchip' },
    { id: 'process', label: 'Process & Compliance', icon: 'fa-clipboard-check' }
  ];

  setCategory(id: string) {
    this.activeCategory.set(id);
  }

  goBack() {
    window.history.back();
  }
}
