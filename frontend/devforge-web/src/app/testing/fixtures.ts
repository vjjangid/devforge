import { Application } from '../core/models/application';
import { Deployment, DeploymentLog, DeploymentStatus } from '../core/models/deployment';

export function anApplication(overrides: Partial<Application> = {}): Application {
  return {
    id: 'app-1',
    name: 'Todo API',
    repositoryUrl: 'https://github.com/vijay/todo-api',
    repositoryDisplayName: 'github.com/vijay/todo-api',
    branch: 'main',
    runtime: 'dotnet-10',
    runtimeDisplayName: '.NET 10',
    description: null,
    status: 'NeverDeployed',
    url: null,
    lastDeployment: null,
    createdAt: '2026-10-02T14:00:00Z',
    updatedAt: '2026-10-02T14:00:00Z',
    ...overrides,
  };
}

export function aDeployment(status: DeploymentStatus, overrides: Partial<Deployment> = {}): Deployment {
  return {
    id: 'dep-1',
    applicationId: 'app-1',
    applicationName: 'Todo API',
    number: 1,
    version: 'v1',
    commitSha: null,
    imageReference: null,
    url: null,
    status,
    currentStage: null,
    simulateFailure: false,
    errorMessage: null,
    createdAt: '2026-10-02T14:00:00Z',
    startedAt: null,
    completedAt: null,
    pipeline: [],
    ...overrides,
  };
}

export function aLog(id: number, message: string): DeploymentLog {
  return { id, timestamp: '2026-10-02T14:00:00Z', level: 'Info', stage: null, message };
}
