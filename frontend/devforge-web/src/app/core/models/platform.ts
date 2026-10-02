export interface Runtime {
  key: string;
  displayName: string;
}

export interface PlatformInfo {
  runtimes: Runtime[];
  features: {
    failureSimulation: boolean;
  };
}
