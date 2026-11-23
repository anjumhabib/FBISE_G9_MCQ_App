import { element, empty, table } from './ui.js';

const palette = ['#4f46e5', '#047a77', '#8c3ab8', '#ba5b1a', '#2866b0'];
const charts = new Map();

export function drawChart(container, { type, label, labels, series, percent = false, dataTable, emptyText }) {
    charts.get(container)?.destroy();
    charts.delete(container);
    container.replaceChildren();
    if (!labels.length || !series.some(set => set.values.some(value => value != null))) {
        container.append(empty(emptyText ?? 'No chart data matches these filters.'));
        return;
    }

    const frame = element('div', 'chart admin-chart');
    const canvas = element('canvas');
    canvas.setAttribute('role', 'img');
    canvas.setAttribute('aria-label', label + '. Exact values are available in Chart data.');
    frame.append(canvas);
    const details = element('details', 'admin-chart-data');
    const chartData = element('div');
    details.append(element('summary', '', 'Chart data'), chartData);
    table(chartData, { label: label + ' values', ...dataTable });

    if (typeof window.Chart !== 'function') {
        details.open = true;
        container.append(details);
        return;
    }

    container.append(frame, details);
    const datasets = series.map((set, index) => ({
        label: set.label,
        data: set.values,
        borderColor: palette[index % palette.length],
        backgroundColor: type === 'line' ? '#4f46e51a' : palette[index % palette.length],
        borderRadius: type === 'bar' ? 4 : 0,
        maxBarThickness: 42,
        pointRadius: labels.length > 15 ? 2 : 4,
        tension: .26,
        fill: type === 'line'
    }));
    charts.set(container, new window.Chart(canvas, {
        type,
        data: { labels, datasets },
        options: {
            responsive: true, maintainAspectRatio: false, animation: false,
            scales: {
                y: { beginAtZero: true, max: percent ? 100 : undefined,
                    ticks: percent ? { callback: value => value + '%' } : { precision: 0 } },
                x: { grid: { display: false }, ticks: { maxRotation: 45, autoSkip: true } }
            },
            plugins: {
                legend: { display: series.length > 1, labels: { boxWidth: 12, boxHeight: 12 } },
                tooltip: percent ? { callbacks: {
                    label: context => context.dataset.label + ': ' + context.formattedValue + '%'
                } } : {}
            }
        }
    }));
}

window.addEventListener('beforeprint', () => {
    for (const chart of charts.values()) chart.resize(640, 180);
});
window.addEventListener('afterprint', () => {
    for (const chart of charts.values()) chart.resize();
});
