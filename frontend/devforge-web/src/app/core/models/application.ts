import { DeploymentSummary } from './deployment';

export type ApplicationStatus = 'NeverDeployed' | 'Deploying' | 'Running' | 'Failed';

export interface Application {
  id: string;
  name: string;
  repositoryUrl: string;
  repositoryDisplayName: string;
  branch: string;
  runtime: string;
  runtimeDisplayName: string;
  description: string | null;
  status: ApplicationStatus;
  /** Where the running application can be opened, once a deployment has published it. */
  url: string | null;
  lastDeployment: DeploymentSummary | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpdateApplicationRequest {
  name: string;
  repositoryUrl: string;
  branch: string;
  description: string | null;
}

export interface CreateApplicationRequest extends UpdateApplicationRequest {
  runtime: string;
}
