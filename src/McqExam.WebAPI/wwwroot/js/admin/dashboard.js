import { byId } from '../site.js';
import { drawChart } from './charts.js';
import { mountReport } from './report.js';
import { calendarDate, count, deviceBreakdown, paginate, percent, renderAttempts } from './ui.js';

mountReport({
    endpoint: 'dashboard',
    render(data, { setPage }) {
        byId('dashboard-students').textContent = count(data.totalStudents);
        byId('dashboard-active').textContent = count(data.activeStudentsLast7Days) + ' active in 7 days · '
            + count(data.activeStudentsLast30Days) + ' in 30 days';
        byId('dashboard-attempts').textContent = count(data.totalAttempts);
        byId('dashboard-abandoned').textContent = count(data.abandonedTests) + ' incomplete';
        byId('dashboard-devices').textContent = deviceBreakdown(data.attemptDevices);
        byId('dashboard-average').textContent = percent(data.overallAverageScorePercent);
        byId('dashboard-questions').textContent = count(data.totalQuestions);

        const trend = data.dailyAttempts;
        drawChart(byId('dashboard-trend'), {
            type: 'line', label: 'Daily exam attempts', labels: trend.map(item => calendarDate(item.date, true)),
            series: [{ label: 'Attempts', values: trend.map(item => item.attempts) }],
            dataTable: { headers: ['Date', 'Attempts'], rows: trend.map(item => [calendarDate(item.date), count(item.attempts)]) }
        });

        const averages = data.subjectAverages;
        drawChart(byId('dashboard-subjects'), {
            type: 'bar', label: 'Average scores by subject', labels: averages.map(item => item.subject), percent: true,
            series: [{ label: 'Average score', values: averages.map(item => item.average) }],
            dataTable: { headers: ['Subject', 'Average score'], rows: averages.map(item => [item.subject, percent(item.average)]) },
            emptyText: 'Average scores appear after students complete tests.'
        });

        renderAttempts(byId('dashboard-table'), data.recentAttempts.items, { showStudent: true });
        paginate(byId('dashboard-pages'), data.recentAttempts, setPage, 'attempts');
    }
});
