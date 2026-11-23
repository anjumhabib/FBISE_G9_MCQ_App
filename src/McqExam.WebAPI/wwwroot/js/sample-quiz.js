import { api, byId, letters, renderBloom, renderDiagram, renderFormula, showMessage } from './site.js';
import { activateAds, refreshAds } from './ads.js';

const page = byId('sample-page');
const start = byId('start-sample');
const form = byId('sample-form');
const submit = byId('submit-sample');
const result = byId('sample-result');
const error = byId('sample-error');
const endpoint = '/api/public/sample-quiz';
let quiz;

start.addEventListener('click', async () => {
    if (start.disabled) return;
    start.disabled = true;
    showMessage(error);
    try {
        quiz = await api(endpoint + '/' + encodeURIComponent(page.dataset.slug));
        byId('sample-questions').replaceChildren(...quiz.questions.map(question => questionCard(question)));
        result.hidden = true;
        form.hidden = false;
        start.hidden = true;
        showSampleAd(false);
        form.querySelector('input')?.focus();
    } catch (failure) {
        showMessage(error, failure.message);
    } finally {
        start.disabled = false;
    }
});

form.addEventListener('submit', async event => {
    event.preventDefault();
    if (submit.disabled || !quiz) return;
    submit.disabled = true;
    showMessage(error);
    try {
        const data = new FormData(form);
        const answers = quiz.questions.map(question => ({
            questionId: question.id, selectedOption: data.get('question-' + question.id)
        }));
        const graded = await api(endpoint + '/submit', { method: 'POST', body: { ticket: quiz.ticket, answers } });
        const score = byId('sample-score');
        score.textContent = graded.correct + ' of ' + graded.total + ' correct · ' + graded.percent + '%';
        renderBloom(byId('bloom-list'), graded.bloom);
        byId('sample-review').replaceChildren(...graded.questions.map(item => questionCard(item.question, item)));
        form.hidden = true;
        result.hidden = false;
        start.textContent = 'Try another sample quiz';
        start.hidden = false;
        score.focus();
        showSampleAd(true);
        void activateAds();
    } catch (failure) {
        showMessage(error, failure.message);
        if (failure.status === 400 || failure.status === 409) {
            form.hidden = true;
            start.hidden = false;
            start.focus();
        }
    } finally {
        submit.disabled = false;
    }
});

function showSampleAd(visible) {
    page.querySelector('[data-slot-key="sample_quiz_bottom"]')?.toggleAttribute('data-ad-inactive', !visible);
    refreshAds();
}

function questionCard(question, review) {
    const card = document.createElement(review ? 'article' : 'fieldset');
    card.className = 'card question-card sample-question';
    const heading = document.createElement(review ? 'h3' : 'legend');
    heading.textContent = 'Question ' + question.position;
    card.append(heading);

    const prompt = document.createElement('p');
    prompt.className = 'question-text';
    renderFormula(prompt, question.questionText);
    card.append(prompt);

    if (question.imagePath) {
        const diagram = document.createElement('img');
        diagram.className = 'diagram';
        diagram.alt = 'Diagram for this question';
        diagram.loading = 'lazy';
        renderDiagram(diagram, question.imagePath);
        card.append(diagram);
    }

    if (review) {
        const status = document.createElement('p');
        status.className = 'muted';
        const selection = review.selectedOption ?? 'Skipped';
        status.textContent = 'Your answer · ' + selection + ' | Correct answer · ' + review.correctOption + ' | ' + question.bloomLevel;
        card.append(status);
    }

    const options = document.createElement('div');
    options.className = 'option-list';
    question.options.forEach((option, index) => {
        const letter = letters[index];
        const row = document.createElement(review ? 'div' : 'label');
        row.className = 'option';
        if (review) {
            if (review.correctOption === letter) row.classList.add('correct-option');
            else if (review.selectedOption === letter) row.classList.add('wrong-option');
        } else {
            const radio = document.createElement('input');
            radio.type = 'radio';
            radio.name = 'question-' + question.id;
            radio.value = letter;
            row.append(radio);
        }
        const prefix = document.createElement('span');
        prefix.className = 'letter';
        prefix.textContent = letter;
        const text = document.createElement('span');
        renderFormula(text, option);
        row.append(prefix, text);
        options.append(row);
    });
    card.append(options);
    return card;
}
