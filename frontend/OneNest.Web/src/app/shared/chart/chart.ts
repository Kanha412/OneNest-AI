import {
  Component,
  ElementRef,
  effect,
  input,
  viewChild,
  OnDestroy
} from '@angular/core';

import {
  Chart,
  ChartConfiguration,
  ChartType,
  registerables
} from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-chart',
  standalone: true,
  template: '<canvas #canvas></canvas>',
  styles: [`
    :host {
      display: block;
      position: relative;
      width: 100%;
      height: 100%;
    }
    canvas {
      display: block;
      width: 100% !important;
      height: 100% !important;
    }
  `]
})
export class ChartComponent implements OnDestroy {

  readonly type = input.required<ChartType>();
  readonly data = input.required<ChartConfiguration['data']>();
  readonly options = input<ChartConfiguration['options']>({});

  private readonly canvas =
    viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  private chart?: Chart;

  constructor() {
    effect(() => {
      const type = this.type();
      const data = this.data();
      const options = this.options();
      const canvas = this.canvas().nativeElement;

      this.chart?.destroy();

      this.chart = new Chart(canvas, {
        type,
        data,
        options: {
          responsive: true,
          maintainAspectRatio: false,
          ...options
        }
      });
    });
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }
}
