import { byId } from '../site.js';
import { drawChart } from './charts.js';
import { mountReport, reportUrl } from './report.js';
import { badge, count, deviceBreakdown, element, empty, facts, percent, table } from './ui.js';

mountReport({
    endpoint: 'reports/subjects',
    url(query, tab, overrides) {
        return reportUrl('reports/subjects', query, { page: 1, pageSize: 200, ...overrides });
    },
    render(data) {
        const subjects = data.items;
        byId('subject-summary').replaceChildren(...(subjects.length ? subjects.map(subjectCard)
            : [empty('No subjects match these filters.')]));
        renderBloom(subjects);
        renderChapters(subjects);
    }
});

function subjectCard(subject) {
    const card = element('article', 'card report-card admin-subject');
    const summary = element('dl', 'admin-facts');
    facts(summary, [
        ['Attempts', count(subject.attempts)], ['Unique students', count(subject.uniqueStudents)],
        ['Average score', percent(subject.averageScorePercent)], ['Pass rate', percent(subject.passRatePercent)],
        ['Attempts by device', deviceBreakdown(subject.attemptDevices)]
    ]);
    card.append(element('h2', '', subject.subject), summary);
    return card;
}

function renderBloom(subjects) {
    const levels = subjects[0]?.bloom.map(stat => stat.level) ?? [];
    const accuracy = subject => level => {
        const value = subject.bloom.find(item => item.level === level)?.accuracy;
        return value?.total ? value.percent : null;
    };
    drawChart(byId('subject-bloom'), {
        type: 'bar', label: 'Bloom’s Taxonomy accuracy', labels: levels, percent: true,
        series: subjects.map(subject => ({ label: subject.subject, values: levels.map(accuracy(subject)) })),
        dataTable: {
            headers: ['Bloom’s level', ...subjects.map(subject => subject.subject)],
            rows: levels.map(level => [level, ...subjects.map(subject => percent(accuracy(subject)(level)))])
        },
        emptyText: 'Bloom’s accuracy appears after tests are completed.'
    });
}

function renderChapters(subjects) {
    const chapters = subjects.flatMap(subject => subject.weakestChapters.map(chapter => ({ subject: subject.subject, ...chapter })));
    chapters.sort((a, b) => Number(!a.accuracy.total) - Number(!b.accuracy.total) ||
        a.accuracy.percent - b.accuracy.percent || a.subject.localeCompare(b.subject) || a.chapterNo - b.chapterNo);
    table(byId('subject-chapters'), {
        label: 'Chapter weakness heatmap', wrap: [1],
        headers: ['Subject', 'Chapter', 'Correct', 'Questions seen', 'Answers by device', 'Accuracy'],
        rows: chapters.map(chapter => {
            const { total, correct, percent: score } = chapter.accuracy;
            const tone = !total ? 'slate' : score < 50 ? 'danger' : score < 75 ? 'admin-warn' : 'success';
            return [chapter.subject, chapter.chapterNo + '. ' + chapter.title, count(correct), count(total), deviceBreakdown(chapter.answerDevices),
                badge(total ? percent(score) : 'No results', tone)];
        }),
        emptyText: 'No chapters match these filters.'
    });
}
