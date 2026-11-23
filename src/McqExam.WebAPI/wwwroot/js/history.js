import { api, byId, clone, duration, field, formatDate, renderBloom, session, showMessage } from './site.js';
import { activateAds } from './ads.js';

const student = session.get()?.user;
if (student) void load(student);

async function load(student) {
    try {
        const data = await api('/api/reports/history');
        const { attempts, bloom, weakestChapters, subjectAverages } = data;
        const count = attempts.length;
        const best = subjectAverages[0];
        const weakest = bloom.filter(item => item.accuracy.total)
            .sort((a, b) => a.accuracy.percent - b.accuracy.percent || b.accuracy.total - a.accuracy.total)[0];

        byId('report-student').textContent = student.fullName;
        byId('report-date').textContent = formatDate(Date.now(), 'longDate');
        byId('total-tests').textContent = count;
        byId('overall-average').textContent = count ? data.overallAverage + '%' : '—';
        byId('best-subject').textContent = best?.subject ?? '—';
        showMessage(byId('best-score'), best ? best.average + '% average' : '');
        byId('weakest-bloom').textContent = weakest?.level ?? '—';
        showMessage(byId('weakest-score'), weakest ? weakest.accuracy.percent + '% accuracy' : '');
        byId('history-tests').textContent = count + (count === 1 ? ' test' : ' tests');
        byId('attempt-rows').append(...attempts.map(makeAttempt));
        byId('history-table').hidden = !count;
        byId('empty-history').hidden = Boolean(count);
        byId('score-chart').hidden = !count;
        byId('empty-chart').hidden = Boolean(count);

        renderBloom(byId('bloom-list'), bloom);
        const chapters = byId('weakest-chapters');
        chapters.append(...weakestChapters.map(chapter => diagnostic(chapter.title, chapter.accuracy.percent,
            chapter.subject + ' · Ch. ' + chapter.chapterNo)));
        chapters.hidden = !weakestChapters.length;
        byId('empty-chapters-report').hidden = Boolean(weakestChapters.length);
        byId('average-list').append(...subjectAverages.map(item => diagnostic(item.subject, item.average)));
        byId('subject-averages').hidden = !subjectAverages.length;
        for (const id of ['print-header', 'history-kpis', 'score-trend', 'attempt-history', 'history-diagnostics']) byId(id).hidden = false;
        const print = byId('print-report');
        print.disabled = false;
        print.addEventListener('click', () => window.print());
        if (count) drawChart(attempts.slice(0, 15).reverse());
        void activateAds();
    } catch (error) {
        showMessage(byId('history-error'), error.message);
    } finally { byId('loading-history').hidden = true; }
}

function makeAttempt(attempt) {
    const row = clone('attempt-row');
    field(row, 'date').textContent = formatDate(attempt.completedAt);
    field(row, 'subject').textContent = attempt.subject;
    field(row, 'mode').textContent = attempt.mode;
    field(row, 'score').textContent = attempt.correctCount + ' / ' + attempt.totalQuestions;
    field(row, 'time').textContent = duration(attempt.timeSpentSeconds);
    const percentage = field(row, 'percentage');
    percentage.textContent = attempt.scorePercent + '%';
    percentage.classList.toggle('success', attempt.scorePercent >= 75);
    percentage.classList.toggle('danger', attempt.scorePercent < 50);
    row.querySelector('a').href = '/review/' + encodeURIComponent(attempt.id);
    return row;
}

function diagnostic(title, percent, detail) {
    const row = clone('diagnostic-row');
    field(row, 'title').textContent = title;
    field(row, 'score').textContent = percent + '%';
    showMessage(field(row, 'detail'), detail);
    return row;
}

function drawChart(attempts) {
    const date = new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric' });
    new window.Chart(byId('plot'), {
        type: 'line',
        data: {
            labels: attempts.map(item => date.format(new Date(item.completedAt))),
            datasets: [{ label: 'Score (%)', data: attempts.map(item => item.scorePercent),
                borderColor: '#4f46e5', backgroundColor: 'rgba(79,70,229,.1)', pointBackgroundColor: '#4f46e5',
                pointRadius: 4, pointHoverRadius: 6, tension: 0.28, fill: true }]
        },
        options: { responsive: true, maintainAspectRatio: false, animation: false,
            scales: { y: { min: 0, max: 100, ticks: { callback: value => value + '%' } },
                x: { grid: { display: false }, offset: attempts.length === 1 } },
            plugins: { legend: { display: false },
                tooltip: { callbacks: { afterLabel: context => attempts[context.dataIndex].subject } } } }
    });
}
