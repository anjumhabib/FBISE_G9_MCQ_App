import { api, byId, clone, duration, field, formatDate, letters, renderBloom, renderDiagram,
    renderFormula, selectButton, session, showMessage } from './site.js';
import { activateAds, refreshAds } from './ads.js';

if (session.get()) void load();

async function load() {
    try {
        const attemptId = encodeURIComponent(document.body.dataset.attemptId);
        const data = await api('/api/exams/' + attemptId + '/review');
        const { attempt, bloom } = data.result;
        byId('result-score').textContent = attempt.scorePercent + '%';
        byId('result-mode').textContent = attempt.mode === 'Exam' ? 'TIMED EXAM' : 'PRACTICE TEST';
        byId('result-subject').textContent = attempt.subject;
        byId('result-date').textContent = formatDate(attempt.completedAt, 'medium');
        byId('result-marks').textContent = attempt.correctCount + ' / ' + attempt.totalQuestions;
        byId('result-time').textContent = duration(attempt.timeSpentSeconds);
        renderBloom(byId('bloom-list'), bloom);
        const buttons = [...byId('review-filters').querySelectorAll('button')];
        const cards = data.questions.map(item => {
            const status = item.selectedOption === null ? 'Skipped' : item.isCorrect ? 'Correct' : 'Incorrect Only';
            return { status, node: makeCard(item, status) };
        });
        const list = byId('review-questions');
        const ads = insertReviewCards(list, cards);

        function update(filter) {
            for (const button of buttons) selectButton(button, button.textContent === filter);
            let hasQuestions = false;
            for (const card of cards) {
                card.node.hidden = filter !== 'All' && card.status !== filter;
                hasQuestions ||= !card.node.hidden;
            }
            for (const ad of ads) ad.toggleAttribute('data-ad-inactive', filter !== 'All');
            list.hidden = !hasQuestions;
            byId('empty-review').hidden = hasQuestions;
            refreshAds();
        }

        for (const button of buttons) button.addEventListener('click', () => update(button.textContent));
        update('All');
        for (const id of ['score-summary', 'review-bloom', 'question-review']) byId(id).hidden = false;
        void activateAds();
    } catch (error) {
        showMessage(byId('review-error'), error.message);
    } finally { byId('loading-review').hidden = true; }
}

function insertReviewCards(list, cards) {
    const template = document.querySelector('template[data-ad-infeed]');
    const interval = Number(template?.dataset.interval);
    const ads = [];
    for (const [index, card] of cards.entries()) {
        list.append(card.node);
        const position = index + 1;
        if (template && Number.isInteger(interval) && interval > 0 && position < cards.length && position % interval === 0) {
            const ad = template.content.firstElementChild.cloneNode(true);
            ads.push(ad);
            list.append(ad);
        }
    }
    return ads;
}

function makeCard(item, status) {
    const q = item.question;
    const card = clone('review-card');
    field(card, 'position').textContent = 'Question ' + q.position;
    const chapter = field(card, 'chapter');
    chapter.textContent = 'Chapter ' + q.chapterNo;
    chapter.title = q.chapterTitle;
    field(card, 'bloom').textContent = q.bloomLevel;
    const badge = field(card, 'status');
    badge.classList.add(status === 'Skipped' ? 'slate' : item.isCorrect ? 'success' : 'danger');
    badge.textContent = status === 'Incorrect Only' ? 'Incorrect' : status;
    renderFormula(field(card, 'text'), q.questionText);
    renderDiagram(field(card, 'diagram'), q.imagePath);
    field(card, 'options').append(...q.options.map((value, index) => {
        const option = clone('review-option');
        const letter = letters[index];
        field(option, 'letter').textContent = letter;
        renderFormula(field(option, 'text'), value);
        if (letter === item.correctOption) {
            option.classList.add('correct-option');
            showMessage(field(option, 'label'), '✓ Correct answer');
        } else if (letter === item.selectedOption) {
            option.classList.add('wrong-option');
            showMessage(field(option, 'label'), '✕ Your answer');
        }
        return option;
    }));
    return card;
}
