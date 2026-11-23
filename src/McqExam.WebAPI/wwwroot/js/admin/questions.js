import { byId, letters, renderFormula } from '../site.js';
import { mountReport } from './report.js';
import { badge, count, deviceBreakdown, element, noValue, paginate, percent, table } from './ui.js';

mountReport({
    endpoint: 'reports/questions',
    render(data, { setPage }) {
        table(byId('question-table'), {
            label: 'Question item analysis', wrap: [0],
            headers: ['Question', 'Bloom’s level', 'Served', 'Served by device', 'Correct', 'Accuracy', 'Skipped', 'Choices'],
            rows: data.items.map(item => [
                questionCell(item), item.question.bloomLevel, count(item.timesServed), deviceBreakdown(item.presentationDevices), count(item.correctCount),
                percent(item.accuracyPercent), count(item.skippedCount), choices(item)
            ]),
            emptyText: 'No questions match these filters.'
        });
        paginate(byId('question-pages'), data, setPage, 'questions');
    }
});

function questionCell(item) {
    const question = item.question;
    const node = element('div', 'admin-question-copy');
    const prompt = element('strong');
    renderFormula(prompt, question.questionText);
    node.append(element('small', '', question.subject + ' · Ch. ' + question.chapterNo + ' · Q' + question.qNo), prompt);
    if (item.suspectedWrongKey) node.append(badge('Suspected wrong-key', 'danger'));
    if (!item.timesServed) node.append(badge('Never attempted'));
    return node;
}

function choices(item) {
    if (item.completedPresentations === item.skippedCount) return noValue;

    const disclosure = element('details', 'admin-choices');
    const choices = element('summary', '', 'A–D breakdown');
    choices.setAttribute('aria-label', 'Option breakdown for question ' + item.question.qNo + ' in ' + item.question.subject);
    disclosure.append(choices);
    for (const letter of letters) {
        const value = item.optionDistribution[letter.toLowerCase()];
        const bar = element('progress');
        bar.max = 100;
        bar.value = value;
        const label = letter + (item.correctOption === letter ? ' · key' : '');
        bar.setAttribute('aria-label', label + ' selection percentage');
        const row = element('div', 'admin-choice-row');
        row.append(element('span', '', label), bar, element('strong', '', percent(value)));
        disclosure.append(row);
    }
    return disclosure;
}
