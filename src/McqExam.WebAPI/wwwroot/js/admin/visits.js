import { byId, showMessage } from '../site.js';
import { drawChart } from './charts.js';
import { mountReport, navigateUrl } from './report.js';
import { badge, count, date, deviceBreakdown, link, noValue, paginate, table, timestamp } from './ui.js';

const hour = value => String(value).padStart(2, '0') + ':00';
const eventNames = {
    Login: 'Login', PageView: 'Page view', ExamStart: 'Exam start', ExamSubmit: 'Exam submission'
};
const weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

mountReport({
    endpoint: 'reports/visits',
    render(data, { query, setPage }) {
        const visits = data.visits;
        byId('visits-total').textContent = count(visits.totalItems);
        byId('visits-devices').textContent = deviceBreakdown(data.visitDevices);
        byId('visits-logins').textContent = count(data.totalLogins);
        const peakHour = data.hourlyTraffic.reduce((best, next) => next.visits > best.visits ? next : best);
        const peakDay = data.weekdayTraffic.reduce((best, next) => next.visits > best.visits ? next : best);
        byId('visits-peak-hour').textContent = peakHour.visits ? hour(peakHour.hour) : noValue;
        byId('visits-peak-day').textContent = peakDay.visits ? peakDay.day : noValue;
        showMessage(byId('visits-peak-hour-count'), peakHour.visits ? count(peakHour.visits) + ' visits' : '');
        showMessage(byId('visits-peak-day-count'), peakDay.visits ? count(peakDay.visits) + ' visits' : '');

        const daily = data.dailyUniqueVisitors;
        const day = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', timeZone: 'UTC' });
        drawChart(byId('visits-daily'), {
            type: 'line', label: 'Daily unique visitors', labels: daily.map(item => day.format(new Date(item.date))),
            series: [{ label: 'Unique visitors', values: daily.map(item => item.uniqueVisitors) }],
            dataTable: { headers: ['Date (UTC)', 'Unique visitors'], rows: daily.map(item => [date(item.date), count(item.uniqueVisitors)]) },
            emptyText: 'No visits match these filters.'
        });

        const hourly = visits.totalItems ? data.hourlyTraffic : [];
        drawChart(byId('visits-hourly'), {
            type: 'bar', label: 'Visits by hour', labels: hourly.map(item => hour(item.hour)),
            series: [{ label: 'Visits', values: hourly.map(item => item.visits) }],
            dataTable: { headers: ['Hour (UTC)', 'Visits'], rows: hourly.map(item => [hour(item.hour), count(item.visits)]) },
            emptyText: 'No visits match these filters.'
        });

        const week = [...data.weekdayTraffic].sort((a, b) => weekdays.indexOf(a.day) - weekdays.indexOf(b.day));
        table(byId('visits-weekdays'), {
            label: 'Weekday traffic', headers: ['Weekday', 'Visits'], rows: week.map(item => [item.day, count(item.visits)])
        });
        table(byId('visits-students'), {
            label: 'Top active students', headers: ['Student', 'Visits', 'Visits by device'], wrap: [0, 2],
            rows: data.topActiveStudents.map(item => [studentFilter(item, query), count(item.visits), deviceBreakdown(item.visitDevices)]),
            emptyText: 'No identified student visits match these filters.'
        });
        table(byId('visits-table'), {
            label: 'Visit log', wrap: [1, 3],
            headers: ['Timestamp (UTC)', 'Student', 'Event', 'Path', 'IP address', 'Device', 'Browser'],
            rows: visits.items.map(visit => [
                timestamp(visit.visitedAt), visit.studentId ? studentFilter(visit, query) : 'Anonymous',
                badge(eventNames[visit.eventType] ?? visit.eventType), visit.path, visit.ipAddress,
                visit.device, visit.browser
            ]),
            emptyText: 'No visits match these filters.'
        });
        paginate(byId('visits-pages'), visits, setPage, 'visits');
    }
});

function studentFilter(item, query) {
    const params = new URLSearchParams(query);
    params.set('studentId', item.studentId);
    params.delete('page');
    return link(item.studentName, navigateUrl(params));
}
