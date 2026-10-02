import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, input, viewChild } from '@angular/core';
import { DeploymentLog } from '../../core/models/deployment';

/** How close to the bottom (px) the viewer must be for new lines to keep it pinned there. */
const STICK_TO_BOTTOM_THRESHOLD = 40;

@Component({
  selector: 'app-deployment-logs',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div #viewport class="terminal mono" role="log" aria-label="Deployment logs" tabindex="0" (scroll)="onScroll()">
      @for (entry of logs(); track entry.id) {
        <div class="line" [class.error]="entry.level === 'Error'" [class.warning]="entry.level === 'Warning'">
          <time class="time">{{ entry.timestamp | date: 'HH:mm:ss' }}</time>
          <span class="message">{{ entry.message }}</span>
        </div>
      } @empty {
        <div class="line placeholder">No log output yet.</div>
      }
      @if (live()) {
        <div class="line placeholder">
          <span class="cursor" aria-hidden="true">▍</span>
        </div>
      }
    </div>
  `,
  styles: `
    .terminal {
      max-height: 420px;
      overflow-y: auto;
      padding: var(--space-3) var(--space-4);
      border-radius: 0 0 var(--radius) var(--radius);
      background: var(--terminal-bg);
      color: var(--terminal-text);
      line-height: 1.7;
    }

    .line {
      display: flex;
      gap: var(--space-3);
    }

    .time,
    .placeholder {
      color: var(--terminal-muted);
    }

    .time {
      flex: none;
    }

    .message {
      overflow-wrap: anywhere;
      white-space: pre-wrap;
    }

    .error .message {
      color: var(--terminal-error);
    }

    .warning .message {
      color: #f5c26b;
    }

    .cursor {
      animation: blink 1s steps(2) infinite;
    }

    @keyframes blink {
      50% { opacity: 0; }
    }
  `,
})
export class DeploymentLogs {
  readonly logs = input.required<DeploymentLog[]>();

  /** Whether more output is expected. */
  readonly live = input(false);

  private readonly viewport = viewChild.required<ElementRef<HTMLElement>>('viewport');
  private pinnedToBottom = true;

  constructor() {
    // Follow new output, unless the reader has scrolled up to look at something.
    afterRenderEffect(() => {
      this.logs();
      const element = this.viewport().nativeElement;
      if (this.pinnedToBottom) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }

  protected onScroll(): void {
    const element = this.viewport().nativeElement;
    this.pinnedToBottom =
      element.scrollHeight - element.scrollTop - element.clientHeight < STICK_TO_BOTTOM_THRESHOLD;
  }
}
