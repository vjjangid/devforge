import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ApiError } from '../../core/models/api-error';
import { ApplicationsApi } from '../../core/services/applications-api';
import { PlatformApi } from '../../core/services/platform-api';
import { anApplication } from '../../testing/fixtures';
import { ApplicationForm } from './application-form';

describe('ApplicationForm', () => {
  const api = { create: vi.fn(), update: vi.fn(), get: vi.fn() };
  const platform = {
    info: () => of({ runtimes: [{ key: 'dotnet-10', displayName: '.NET 10' }], features: { failureSimulation: true } }),
  };

  let fixture: ComponentFixture<ApplicationForm>;
  let element: HTMLElement;

  async function render(id?: string): Promise<void> {
    fixture = TestBed.createComponent(ApplicationForm);
    if (id) {
      fixture.componentRef.setInput('id', id);
    }
    await fixture.whenStable();
    element = fixture.nativeElement as HTMLElement;
  }

  function type(fieldId: string, value: string): void {
    const input = element.querySelector<HTMLInputElement>(`#${fieldId}`)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
  }

  const errors = () => Array.from(element.querySelectorAll('.field .error')).map((node) => node.textContent?.trim());

  beforeEach(() => {
    vi.resetAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ApplicationsApi, useValue: api },
        { provide: PlatformApi, useValue: platform },
      ],
    });
  });

  it('shows validation messages and does not call the API when the form is invalid', async () => {
    await render();

    type('repositoryUrl', 'not a url');
    type('branch', 'my branch');
    await submit();

    expect(errors()).toEqual([
      'Name is required.',
      'Enter a repository URL such as github.com/owner/repo.',
      'Branch must not contain spaces.',
    ]);
    expect(api.create).not.toHaveBeenCalled();
  });

  it('creates the application and navigates to it', async () => {
    api.create.mockReturnValue(of(anApplication({ id: 'new-id' })));
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await render();

    type('name', '  Todo API ');
    type('repositoryUrl', 'github.com/vijay/todo-api');
    await submit();

    expect(api.create).toHaveBeenCalledWith({
      name: 'Todo API',
      repositoryUrl: 'github.com/vijay/todo-api',
      branch: 'main',
      runtime: 'dotnet-10',
      description: null,
    });
    expect(navigate).toHaveBeenCalledWith(['/applications', 'new-id']);
  });

  it('shows field errors returned by the API under the matching input', async () => {
    api.create.mockReturnValue(
      throwError(() => new ApiError(400, 'Invalid', { repositoryUrl: ['Repository URL must not contain credentials.'] })),
    );
    await render();

    type('name', 'Todo API');
    type('repositoryUrl', 'github.com/vijay/todo-api');
    await submit();

    expect(errors()).toEqual(['Repository URL must not contain credentials.']);
    expect(element.querySelector('app-error-alert')).toBeNull();
  });

  it('shows a conflict from the API as a banner', async () => {
    api.create.mockReturnValue(throwError(() => new ApiError(409, "An application named 'Todo API' already exists.")));
    await render();

    type('name', 'Todo API');
    type('repositoryUrl', 'github.com/vijay/todo-api');
    await submit();

    expect(element.querySelector('app-error-alert')?.textContent).toContain('already exists');
  });

  it('loads the application when editing, locks the runtime, and saves with update', async () => {
    api.get.mockReturnValue(of(anApplication({ description: 'Original' })));
    api.update.mockReturnValue(of(anApplication()));
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    await render('app-1');

    expect(element.querySelector<HTMLInputElement>('#name')!.value).toBe('Todo API');
    expect(element.querySelector<HTMLSelectElement>('#runtime')!.disabled).toBe(true);

    type('branch', 'develop');
    await submit();

    expect(api.update).toHaveBeenCalledWith('app-1', {
      name: 'Todo API',
      repositoryUrl: 'https://github.com/vijay/todo-api',
      branch: 'develop',
      description: 'Original',
    });
    expect(api.create).not.toHaveBeenCalled();
  });
});
