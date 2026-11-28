import { byId, clone, field, letters, renderBloom, renderDiagram, renderFormula } from '../site.js';
import { mountReport } from './report.js';
import { count, date, elapsed, element, empty, facts, noValue, percent, timestamp } from './ui.js';

const attemptId = encodeURIComponent(document.body.dataset.attemptId);

mountReport({
    endpoint: 'attempts/' + attemptId,
    render({ student, attempt, chapters, bloom, questions }) {
        const completed = Boolean(attempt.completedAt);
        byId('attempt-score').textContent = completed ? percent(attempt.scorePercent) : noValue;
        byId('attempt-mode').textContent = attempt.mode;
        byId('attempt-subject').textContent = attempt.subject;
        byId('attempt-state').textContent = completed ? 'Completed' : 'Incomplete';
        byId('attempt-marks').textContent = completed ? count(attempt.correctCount) + ' / ' + count(attempt.totalQuestions) : noValue;
        byId('attempt-time').textContent = completed ? elapsed(attempt.timeSpentSeconds) : noValue;
        byId('attempt-student-link').href = '/admin/students?studentId=' + encodeURIComponent(student.id);
        facts(byId('attempt-student'), [
            ['Name', student.fullName], ['Email', student.email], ['Registered', date(student.createdAt)]
        ]);
        facts(byId('attempt-details'), [
            ['Start device', attempt.device ?? 'Unknown'],
            ['Chapters', chapters.map(item => item.chapterNo + '. ' + item.title).join(', ') || noValue],
            ['Started', timestamp(attempt.startedAt)], ['Ended', timestamp(attempt.completedAt)],
            ['Questions', count(attempt.totalQuestions)]
        ]);
        renderBloom(byId('bloom-list'), bloom);
        byId('attempt-questions').replaceChildren(...(questions.length ? questions.map(reviewCard)
            : [empty('No questions are available for this attempt.')]));
    }
});

function reviewCard(item) {
    const question = item.question;
    const card = clone('admin-review-card');
    field(card, 'position').textContent = 'Question ' + question.position;
    field(card, 'chapter').textContent = 'Ch. ' + question.chapterNo + ' · ' + question.chapterTitle;
    field(card, 'bloom').textContent = question.bloomLevel;
    const status = field(card, 'status');
    status.textContent = item.status;
    status.classList.add(item.status === 'Correct' ? 'success' : item.status === 'Incorrect' ? 'danger' : 'slate');
    renderFormula(field(card, 'text'), question.questionText);

    const diagram = field(card, 'diagram');
    diagram.addEventListener('error', () => {
        diagram.hidden = true;
        diagram.after(element('p', 'empty-inline', 'Diagram unavailable.'));
    }, { once: true });
    renderDiagram(diagram, question.imagePath);
    field(card, 'summary').textContent = 'Selected: ' + (item.selectedOption ?? 'Skipped') + ' · Correct: ' + item.correctOption;
    field(card, 'options').append(...question.options.map((value, index) => {
        const letter = letters[index];
        const correct = letter === item.correctOption;
        const selected = letter === item.selectedOption;
        const option = element('div', 'option' + (correct ? ' correct-option' : selected ? ' wrong-option' : ''));
        const copy = element('span');
        renderFormula(copy, value);
        option.append(element('span', 'letter', letter), copy);
        if (correct || selected) option.append(element('strong', 'option-label',
            correct && selected ? 'Selected · correct' : correct ? 'Correct answer' : 'Selected'));
        return option;
    }));
    return card;
}
