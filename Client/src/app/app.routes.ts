import { Routes } from '@angular/router';
import { Landing } from './components/landing/landing';
import { CustomerLogin } from './components/customer-login/customer-login';
import { CustomerRegister } from './components/customer-register/customer-register';
import { AdminLogin } from './components/admin-login/admin-login';
import { AgentLogin } from './components/agent-login/agent-login';
import { ClaimsLogin } from './components/claims-login/claims-login';
import { AdminDashboard } from './components/admin-dashboard/admin-dashboard';
import { AgentDashboard } from './components/agent-dashboard/agent-dashboard';
import { ClaimsOfficerDashboard } from './components/claims-officer-dashboard/claims-officer-dashboard';
import { CustomerDashboard } from './components/customer-dashboard/customer-dashboard';
import { ExplorePlans } from './components/explore-plans/explore-plans';

export const routes: Routes = [
  { path: '', component: Landing },
  { path: 'login', component: CustomerLogin },
  { path: 'register', component: CustomerRegister },
  { path: 'admin-login', component: AdminLogin },
  { path: 'agent-login', component: AgentLogin },
  { path: 'claims-login', component: ClaimsLogin },
  { path: 'customer-dashboard', component: CustomerDashboard },
  { path: 'admin-dashboard', component: AdminDashboard },
  { path: 'agent-dashboard', component: AgentDashboard },
  { path: 'claims-dashboard', component: ClaimsOfficerDashboard },
  { path: 'explore-plans', component: ExplorePlans },
  { path: '**', redirectTo: '' }
];

