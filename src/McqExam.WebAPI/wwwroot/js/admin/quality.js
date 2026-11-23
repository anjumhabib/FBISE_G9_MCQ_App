import { byId, renderFormula, showMessage } from '../site.js';
import { mountReport } from './report.js';
import { attemptLink, badge, count, deviceBreakdown, element, empty, link, noValue, paginate, table, timestamp } from './ui.js';

const tabs = {
    duplicates: ['duplicates', 'Duplicate questions', 'Normalized matches can span subjects. Attempt counts show which copies have been used.'],
    media: ['media-audit', 'Broken diagrams / data audit', 'Questions with missing diagram files or empty or visibly truncated options.'],
    anomalies: ['anomalies', 'Abandoned / fast guessing', 'Shows incomplete tests and completed submissions averaging under 5 seconds per question.']
};

mountReport({
    endpoint: (query, tab) => 'reports/' + tabs[tab][0],
    render(data, { tab, setPage }) {
        const [, title, description] = tabs[tab];
        byId('quality-title').textContent = title;
        showMessage(byId('quality-description'), description);
        const results = byId('quality-results');
        if (tab === 'duplicates') renderDuplicates(results, data.items);
        else if (tab === 'media') renderMedia(results, data.items);
        else renderAnomalies(results, data.items);
        paginate(byId('quality-pages'), data, setPage, tab === 'duplicates' ? 'groups' : 'findings');
    }
});

function questionPrompt(question) {
    const node = element('span');
    renderFormula(node, question.questionText);
    return node;
}

function renderDuplicates(container, groups) {
    if (!groups.length) {
        container.replaceChildren(empty('No duplicate groups match these filters.'));
        return;
    }
    container.replaceChildren(...groups.map(group => {
        const article = element('article', 'admin-duplicate');
        const header = element('div', 'admin-section-heading');
        const title = element('h3');
        renderFormula(title, group.questions[0].question.questionText);
        header.append(title, badge(group.hasExactDuplicates ? 'Contains exact matches' : 'Normalized match'));
        const matches = element('div');
        table(matches, {
            label: 'Copies of this question', wrap: [1, 3],
            headers: ['Subject', 'Chapter', 'Q no.', 'Question text', 'Attempts', 'Served by device'],
            rows: group.questions.map(({ question, attemptCount, presentationDevices }) => [
                question.subject, question.chapterNo + '. ' + question.chapter, count(question.qNo),
                questionPrompt(question), count(attemptCount), deviceBreakdown(presentationDevices)
            ])
        });
        article.append(header, matches);
        return article;
    }));
}

function renderMedia(container, issues) {
    table(container, {
        label: 'Broken media and option issues', wrap: [1, 3, 4],
        headers: ['Subject', 'Chapter', 'Q no.', 'Question text', 'Diagram', 'Empty options', 'Truncated options', 'Served by device'],
        rows: issues.map(({ question, brokenDiagram, imagePath, emptyOptions, truncatedOptions, presentationDevices }) => {
            const media = element('div', 'admin-person');
            if (brokenDiagram) media.append(badge('Missing diagram', 'danger'), imagePath || 'No diagram path');
            return [question.subject, question.chapterNo + '. ' + question.chapter, count(question.qNo),
                questionPrompt(question), brokenDiagram ? media : noValue,
                emptyOptions.join(', ') || noValue, truncatedOptions.join(', ') || noValue, deviceBreakdown(presentationDevices)];
        }),
        emptyText: 'No media or option issues match these filters.'
    });
}

function renderAnomalies(container, issues) {
    table(container, {
        label: 'Unfinished and fast submissions', wrap: [1],
        headers: ['Started (UTC)', 'Student', 'Subject', 'Mode', 'Start device', 'Finding', 'Seconds / question', 'Review'],
        rows: issues.map(({ attempt, abandoned, secondsPerQuestion }) => [
            timestamp(attempt.startedAt),
            link(attempt.studentName, '/admin/students?studentId=' + encodeURIComponent(attempt.studentId)),
            attempt.subject, attempt.mode, attempt.device ?? 'Unknown',
            badge(abandoned ? 'Incomplete' : 'Fast submission', abandoned ? 'slate' : 'admin-warn'),
            secondsPerQuestion == null ? noValue : count(secondsPerQuestion) + ' s', attemptLink(attempt)
        ]),
        emptyText: 'No incomplete or fast tests match these filters.'
    });
}
