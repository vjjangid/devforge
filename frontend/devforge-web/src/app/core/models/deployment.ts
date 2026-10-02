export type DeploymentStatus = 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';
export type DeploymentStage = 'Preparing' | 'Building' | 'Testing' | 'Deploying';
export type PipelineStepState = 'Pending' | 'Active' | 'Completed' | 'Failed';
export type DeploymentLogLevel = 'Info' | 'Warning' | 'Error';

export interface PipelineStep {
  name: string;
  state: PipelineStepState;
}

export interface Deployment {
  id: string;
  applicationId: string;
  applicationName: string;
  number: number;
  version: string;
  commitSha: string | null;
  /** The container image this deployment built, once the build stage has finished. */
  imageReference: string | null;
  /** Where the application can be opened. Only set while this deployment is the one being served. */
  url: string | null;
  status: DeploymentStatus;
  currentStage: DeploymentStage | null;
  simulateFailure: boolean;
  errorMessage: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  pipeline: PipelineStep[];
}

export interface DeploymentSummary {
  id: string;
  number: number;
  version: string;
  status: DeploymentStatus;
  createdAt: string;
  completedAt: string | null;
}

export interface DeploymentLog {
  id: number;
  timestamp: string;
  level: DeploymentLogLevel;
  stage: DeploymentStage | null;
  message: string;
}

export interface CreateDeploymentRequest {
  simulateFailure: boolean;
}

const ACTIVE_STATUSES: readonly DeploymentStatus[] = ['Queued', 'Running'];

/** A deployment that a worker has not finished yet; its state will still change. */
export function isDeploymentActive(status: DeploymentStatus): boolean {
  return ACTIVE_STATUSES.includes(status);
}
