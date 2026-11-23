import { api, byId, showMessage } from '../site.js';
import { mountReport, navigateUrl, reportUrl } from './report.js';
import { badge, count, date, deviceBreakdown, elapsed, facts, link, noValue, paginate, percent, renderAttempts, table, timestamp } from './ui.js';

const endpoint = query => query.has('studentId') ? 'reports/student-subjects' : 'reports/students';

mountReport({
    endpoint,
    url(query, tab, overrides) {
        return reportUrl(endpoint(query), query, {
            atRisk: !query.has('studentId') && tab === 'at-risk' ? 'true' : null,
            inactiveDays: !query.has('studentId') && tab === 'inactive' ? query.get('inactiveDays') || '7' : null,
            ...overrides
        });
    },
    async load(query, signal, urlFor) {
        if (!query.has('studentId')) return api(urlFor(query), { signal });

        const profileQuery = new URLSearchParams({ studentId: query.get('studentId'), pageSize: 1 });
        const [profile, matrix, dashboard] = await Promise.all([
            api(reportUrl('reports/students', profileQuery), { signal }),
            api(urlFor(query, { page: 1, pageSize: 200 }), { signal }),
            api(reportUrl('dashboard', query, { inactiveDays: null }), { signal })
        ]);
        return { profile: profile.items[0], matrix: matrix.items, attempts: dashboard.recentAttempts };
    },
    exportLabel: query => query.has('studentId') ? 'Export subject CSV' : 'Export CSV',
    render(data, context) {
        const detailed = context.query.has('studentId');
        byId('student-list').hidden = detailed;
        byId('student-detail').hidden = !detailed;
        if (detailed) renderDetail(data, context);
        else renderList(data, context);
    }
});

function renderList(data, { query, tab, setPage }) {
    showMessage(byId('student-tab-description'), tab === 'at-risk'
        ? 'Average score below 50% or declining scores over the last three completed tests.'
        : tab === 'inactive' ? 'Students without a login in the last ' + (query.get('inactiveDays') || '7') + ' days.' : '');

    table(byId('student-table'), {
        label: 'Student performance', wrap: [0, 4],
        headers: ['Student', 'Registered (UTC)', 'Last login (UTC)', 'Last visit device', 'Tests by device', 'Tests', 'Completed', 'Incomplete',
            'Average score', 'Best subject', 'Weakest subject', 'Time spent'],
        rows: data.items.map(row => {
            const detailQuery = new URLSearchParams(query);
            detailQuery.set('studentId', row.student.id);
            detailQuery.delete('page');
            detailQuery.delete('search');
            const name = document.createElement('div');
            name.className = 'admin-person';
            name.append(link(row.student.fullName, navigateUrl(detailQuery, '/admin/students')), row.student.email);
            if (!row.isActive) name.append(badge('Account inactive'));
            return [name, date(row.student.createdAt), row.lastLoginAt ? timestamp(row.lastLoginAt) : 'Never',
                row.student.lastVisitDevice ?? 'Unknown', deviceBreakdown(row.attemptDevices),
                count(row.totalTests), count(row.completedTests), count(row.abandonedTests), percent(row.averageScorePercent),
                row.bestSubject, row.weakestSubject, elapsed(row.totalTimeSpentSeconds)];
        }),
        emptyText: 'No students match these filters.'
    });
    paginate(byId('student-pages'), data, setPage, 'students');
}

function renderDetail(data, { query, setPage }) {
    const row = data.profile;
    const listQuery = new URLSearchParams(query);
    listQuery.delete('studentId');
    listQuery.delete('page');
    byId('student-back').href = navigateUrl(listQuery, '/admin/students');
    byId('student-name').textContent = row?.student.fullName ?? 'Student not found';
    byId('student-profile').replaceChildren();
    if (row) {
        facts(byId('student-profile'), [
            ['Email', row.student.email], ['Registered (UTC)', date(row.student.createdAt)],
            ['Last login (UTC)', row.lastLoginAt ? timestamp(row.lastLoginAt) : 'Never'],
            ['Last visit device', row.student.lastVisitDevice ?? 'Unknown'],
            ['Account', row.isActive ? 'Active' : 'Inactive']
        ]);
    }
    table(byId('student-subjects'), {
        label: 'Subject performance for this student',
        headers: ['Subject', 'Attempts', 'Attempts by device', 'Completed', 'Average', 'Highest', 'Lowest', 'Average time / question'],
        rows: data.matrix.map(item => [
            item.subject, count(item.attempts), deviceBreakdown(item.attemptDevices), count(item.completedAttempts), percent(item.averageScorePercent),
            percent(item.highestScorePercent), percent(item.lowestScorePercent),
            item.averageSecondsPerQuestion == null ? noValue : count(item.averageSecondsPerQuestion) + ' s'
        ]),
        emptyText: 'No subject data matches these filters.'
    });
    renderAttempts(byId('student-attempts'), data.attempts.items);
    paginate(byId('student-attempt-pages'), data.attempts, setPage, 'attempts');
}
