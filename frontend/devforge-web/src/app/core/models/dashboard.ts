import { Application } from './application';
import { Deployment } from './deployment';

export interface DashboardSummary {
  applications: number;
  running: number;
  deploymentsToday: number;
  failedDeployments: number;
  recentApplications: Application[];
  recentDeployments: Deployment[];
}
