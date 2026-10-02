import { TestBed } from '@angular/core/testing';
import { PipelineStep } from '../../core/models/deployment';
import { PipelineSteps } from './pipeline-steps';

describe('PipelineSteps', () => {
  it('renders each step with a class for its state and marks the active one', async () => {
    const steps: PipelineStep[] = [
      { name: 'Queued', state: 'Completed' },
      { name: 'Building', state: 'Active' },
      { name: 'Testing', state: 'Failed' },
      { name: 'Deploying', state: 'Pending' },
    ];

    const fixture = TestBed.createComponent(PipelineSteps);
    fixture.componentRef.setInput('steps', steps);
    await fixture.whenStable();

    const items = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('li'));

    expect(items.map((item) => item.querySelector('.name')?.textContent)).toEqual(steps.map((step) => step.name));
    expect(items.map((item) => item.className)).toEqual(['completed', 'active', 'failed', 'pending']);
    expect(items.map((item) => item.getAttribute('aria-current'))).toEqual([null, 'step', null, null]);
    expect(items[0].querySelector('.marker')?.textContent?.trim()).toBe('✓');
    expect(items[2].querySelector('.marker')?.textContent?.trim()).toBe('✕');
  });
});
