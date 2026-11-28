import { api, byId, field, letters, renderDiagram, renderFormula, selectButton, session, showMessage } from './site.js';
import { activateAds } from './ads.js';

const student = session.get()?.user;
if (student) void init(student);

async function init(student) {
    const id = document.body.dataset.attemptId;
    const path = '/api/exams/' + encodeURIComponent(id);
    const draftKey = 'mcq-draft-' + student.id + '-' + id;
    const signIn = byId('exam-sign-in');
    signIn.href = session.loginUrl(true);
    function showError(error) {
        showMessage(byId('exam-error'), error?.message);
        signIn.hidden = error?.status !== 401;
    }
    let test;
    try {
        test = await api(path, { keepPage: true });
    } catch (error) {
        if (error.status === 409) {
            session.write(draftKey, null);
            location.replace('/review/' + encodeURIComponent(id));
        } else {
            showError(error);
            byId('new-test').hidden = false;
        }
        return;
    } finally { byId('loading-exam').hidden = true; }

    const title = byId('question-title');
    const clock = byId('exam-clock');
    const options = byId('exam-options');
    const markButton = byId('mark-question');
    const previous = byId('previous');
    const next = byId('next');
    const submitButton = byId('open-submit');
    const dialog = byId('confirm-submit');
    const total = test.questions.length;
    const draft = session.read(draftKey);
    const answers = { ...Object.fromEntries(test.savedAnswers.map(item => [item.questionId, item.selectedOption])), ...draft?.answers };
    const marked = new Set(draft?.marked);
    const pending = { ...draft?.pending };
    let index = Math.min(Math.max(draft?.index ?? 0, 0), total - 1);
    const offset = Date.parse(test.serverTime) - Date.now();
    const started = Date.parse(test.startedAt);
    const deadline = test.deadline && Date.parse(test.deadline);
    let remaining = 0;
    let submitting = false;
    let autoSubmitted = false;
    let retryTimer;
    let completed = false;
    let checkpoint = Promise.resolve();
    let timer;

    const current = () => test.questions[index];
    const locked = () => submitting || Boolean(deadline && !remaining);
    const reviewUrl = '/review/' + encodeURIComponent(id);

    const palette = test.questions.map((question, position) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.textContent = question.position;
        button.addEventListener('click', () => go(position));
        return button;
    });
    byId('question-palette').append(...palette);

    function save() { session.write(draftKey, { answers, marked: [...marked], index, pending }); }

    function renderQuestion() {
        const question = current();
        title.textContent = 'Question ' + question.position;
        const chapter = byId('question-chapter');
        chapter.textContent = 'Chapter ' + question.chapterNo;
        chapter.title = question.chapterTitle;
        byId('question-bloom').textContent = question.bloomLevel;
        renderFormula(byId('question-text'), question.questionText);
        renderDiagram(byId('question-image'), question.imagePath);
        for (const [position, label] of [...options.querySelectorAll('label')].entries()) {
            renderFormula(field(label, 'option'), question.options[position]);
        }
        byId('question-position').textContent = 'Question ' + (index + 1) + ' of ' + total;
        byId('exam-progress').value = index + 1;
        updateControls();
    }

    function updateControls() {
        const question = current();
        const answered = Object.keys(answers).length;
        byId('answered-count').textContent = answered + ' / ' + total;
        for (const input of options.querySelectorAll('input')) {
            input.checked = answers[question.id] === input.value;
            input.closest('label').classList.toggle('selected', input.checked);
        }
        options.disabled = markButton.disabled = Boolean(locked());
        previous.disabled = index === 0 || submitting;
        const finalQuestion = index === total - 1;
        next.disabled = submitting;
        next.textContent = finalQuestion ? (submitting ? 'Submitting…' : 'Finish Exam') : 'Next';
        const isMarked = marked.has(question.id);
        selectButton(markButton, isMarked, 'marked');
        markButton.textContent = isMarked ? 'Marked for Review' : 'Mark for Review';
        submitButton.disabled = submitting;
        submitButton.textContent = submitting ? 'Submitting…' : 'Submit Test';
        byId('submit-summary').textContent = answered + ' of ' + total + ' questions answered.';
        byId('submit-skipped').hidden = answered === total;

        for (const [position, button] of palette.entries()) {
            const item = test.questions[position];
            const isAnswered = Boolean(answers[item.id]);
            const isMarked = marked.has(item.id);
            button.classList.toggle('current', position === index);
            button.classList.toggle('answered', isAnswered);
            button.classList.toggle('marked', isMarked);
            if (position === index) button.setAttribute('aria-current', 'step');
            else button.removeAttribute('aria-current');
            const status = isMarked ? 'marked for review' : isAnswered ? 'answered' : 'unanswered';
            button.setAttribute('aria-label', 'Question ' + item.position + ', ' + status);
        }
    }

    function choose(option) {
        if (locked()) return;
        const questionId = current().id;
        answers[questionId] = pending[questionId] = option;
        save();
        updateControls();
        syncAnswer(questionId, option);
    }

    function syncAnswer(questionId, option) {
        const status = byId('exam-save-status');
        showMessage(status, 'Saving answers…');
        checkpoint = checkpoint.then(async () => {
            if (completed || pending[questionId] !== option) return;
            await api(path + '/answers', {
                method: 'PUT', body: { questionId: Number(questionId), selectedOption: option }, keepPage: true
            });
            if (completed) return;
            if (pending[questionId] === option) delete pending[questionId];
            save();
            if (!Object.keys(pending).length) showMessage(status);
        }).catch(error => {
            if (completed) return;
            if (error.status === 401) showError(error);
            showMessage(status, 'Some answers are still on this device. Finish the exam to send them.');
        });
    }

    function syncPending() {
        if (locked()) return;
        for (const [questionId, option] of Object.entries(pending)) syncAnswer(questionId, option);
    }

    function go(position) {
        if (position < 0 || position >= total || position === index || submitting) return;
        index = position;
        save();
        renderQuestion();
        title.focus({ preventScroll: true });
        title.scrollIntoView({ block: 'nearest' });
    }

    async function submit() {
        if (submitting || completed) return;
        clearTimeout(retryTimer);
        dialog.close();
        submitting = true;
        showError();
        updateControls();
        try {
            const timeSpentSeconds = Math.max(0, Math.floor((Date.now() + offset - started) / 1000));
            const selected = Object.entries(answers).map(([questionId, selectedOption]) => ({ questionId: Number(questionId), selectedOption }));
            await api(path + '/submit', { method: 'POST', body: { timeSpentSeconds, answers: selected }, keepPage: true });
            completed = true;
            clearInterval(timer);
            session.write(draftKey, null);
            location.assign(reviewUrl);
        } catch (error) {
            showError(error);
            submitting = false;
            updateControls();
            if (autoSubmitted && navigator.onLine && (!error.status || error.status >= 500)) {
                retryTimer = setTimeout(() => { void submit(); }, 5000);
            }
        }
    }

    function tick() {
        remaining = Math.max(0, Math.ceil((deadline - Date.now() - offset) / 1000));
        const part = value => String(value).padStart(2, '0');
        const hours = Math.floor(remaining / 3600);
        clock.querySelector('strong').textContent = (hours ? part(hours) + ':' : '')
            + part(Math.floor(remaining / 60) % 60) + ':' + part(remaining % 60);
        clock.classList.toggle('warning', remaining < 60);
        if (remaining === 0 && !autoSubmitted) {
            autoSubmitted = true;
            void submit();
        }
    }

    options.addEventListener('change', event => choose(event.target.value));
    markButton.addEventListener('click', () => {
        if (locked()) return;
        const questionId = current().id;
        if (marked.has(questionId)) marked.delete(questionId);
        else marked.add(questionId);
        save();
        updateControls();
    });
    previous.addEventListener('click', () => go(index - 1));
    function finishExam() {
        if (locked()) void submit();
        else dialog.showModal();
    }
    next.addEventListener('click', () => {
        if (index === total - 1) finishExam();
        else go(index + 1);
    });
    submitButton.addEventListener('click', finishExam);
    window.addEventListener('online', () => {
        if (autoSubmitted) void submit();
        else syncPending();
    });
    byId('keep-working').addEventListener('click', () => dialog.close());
    byId('submit-test').addEventListener('click', submit);
    document.addEventListener('keydown', event => {
        if (event.ctrlKey || event.altKey || event.metaKey || event.isComposing || event.defaultPrevented || dialog.open
            || event.target.closest?.('input:not([type="radio"]), textarea, select, [contenteditable]')) return;
        const letter = letters.indexOf(event.key.toUpperCase());
        const number = ['1', '2', '3', '4'].indexOf(event.key);
        if (letter >= 0 || number >= 0) {
            event.preventDefault();
            choose(letters[letter >= 0 ? letter : number]);
        } else if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
            event.preventDefault();
            go(index + (event.key === 'ArrowRight' ? 1 : -1));
        }
    });

    byId('exam-subject').textContent = test.subject;
    byId('exam-mode').textContent = test.mode === 'Exam' ? 'Timed Exam' : 'Practice';
    byId('exam-progress').max = total;
    renderQuestion();
    if (deadline) {
        clock.querySelector('small').textContent = 'Time remaining';
        tick();
        timer = setInterval(tick, 250);
        updateControls();
    } else {
        clock.querySelector('strong').textContent = 'Untimed';
        clock.querySelector('small').textContent = 'Practice mode';
    }
    syncPending();
    byId('exam-toolbar').hidden = byId('exam-layout').hidden = false;
    void activateAds();
}
