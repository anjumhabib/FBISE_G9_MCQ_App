import { api, byId, clone, field, selectButton, session, showMessage } from './site.js';

if (session.get()) init();

function init() {
    const icons = { BIO: '✿', CHEM: '⚗', PHY: '⚡', MATH: '∑', CS: '⌘' };
    const form = byId('start-test');
    const subjectGrid = byId('subject-grid');
    const chapterGrid = byId('chapter-grid');
    const toggleAll = byId('toggle-chapters');
    const countInput = byId('custom-count');
    const diagrams = byId('include-diagrams');
    const startButton = byId('start-button');
    const filters = [...byId('bloom-filters').querySelectorAll('button')];
    const presets = [...byId('count-presets').querySelectorAll('button')];
    const modes = [...byId('mode-field').querySelectorAll('input')];
    let subject, matchRequest, debounce;
    let selected = new Set();
    let bloomLevel = null;
    let matching = null;
    let countError = '';
    let starting = false;

    function currentFilter() {
        return subject && selected.size ? { subjectId: subject.id, chapterIds: [...selected], bloomLevel,
            includeDiagrams: diagrams.checked } : null;
    }

    function updateMatches() {
        const count = Number(countInput.value);
        const hasCount = matching !== null;
        const label = !currentFilter() ? 'Select chapters to see matching questions.'
            : countError ? 'Question count unavailable.' : 'Checking available questions…';
        showMessage(byId('match-message'), hasCount ? '' : label);
        const result = byId('match-count');
        result.textContent = matching ?? '';
        result.hidden = !hasCount;
        byId('match-label').hidden = !hasCount;
        byId('count-warning').hidden = !hasCount || count <= matching;
        showMessage(byId('match-error'), countError);
        startButton.disabled = starting || !countInput.validity.valid || !Number.isInteger(count)
            || count < 1 || !hasCount || count > matching;
        for (const preset of presets) selectButton(preset, Number(preset.dataset.count) === count);
        for (const filter of filters) selectButton(filter, filter.dataset.level === (bloomLevel ?? 'All'));
    }

    function requestCount() {
        clearTimeout(debounce);
        matchRequest?.abort();
        matching = null;
        countError = '';
        updateMatches();
        const filter = currentFilter();
        if (!filter) return;
        const request = new AbortController();
        matchRequest = request;
        debounce = setTimeout(async () => {
            try {
                const result = await api('/api/exams/count', { method: 'POST', body: filter, signal: request.signal });
                if (!request.signal.aborted) matching = result.count;
            } catch (error) {
                if (!request.signal.aborted) countError = error.message;
            }
            if (!request.signal.aborted) updateMatches();
        }, 160);
    }

    function updateChapters() {
        for (const input of chapterGrid.querySelectorAll('input')) {
            input.checked = selected.has(Number(input.value));
            input.closest('label').classList.toggle('selected', input.checked);
        }
        byId('chapter-summary').textContent = subject.name + ' · ' + selected.size + ' selected';
        toggleAll.textContent = selected.size === subject.chapters.length ? 'Deselect All' : 'Select All';
        requestCount();
    }

    function selectSubject(item) {
        if (subject?.id === item.id) return;
        subject = item;
        selected = new Set(item.chapters.map(chapter => chapter.id));
        for (const button of subjectGrid.querySelectorAll('button')) {
            selectButton(button, Number(button.dataset.subjectId) === item.id, 'selected');
        }
        const hasChapters = item.chapters.length > 0;
        toggleAll.hidden = !hasChapters;
        chapterGrid.hidden = !hasChapters;
        showMessage(byId('empty-chapters'), hasChapters ? '' : 'No chapters have been imported for ' + item.name + ' yet.');
        chapterGrid.replaceChildren(...item.chapters.map(chapter => {
            const label = clone('chapter-template');
            const input = label.querySelector('input');
            input.value = chapter.id;
            field(label, 'number').textContent = 'Chapter ' + chapter.chapterNo;
            field(label, 'title').textContent = chapter.title;
            field(label, 'count').textContent = chapter.questionCount + ' MCQs';
            input.addEventListener('change', () => {
                if (input.checked) selected.add(chapter.id);
                else selected.delete(chapter.id);
                updateChapters();
            });
            return label;
        }));
        byId('setup-chapters').hidden = false;
        form.hidden = false;
        showMessage(byId('start-error'));
        updateChapters();
    }

    async function loadSubjects() {
        byId('loading-subjects').hidden = false;
        byId('empty-subjects').hidden = true;
        showMessage(byId('setup-error'));
        try {
            const subjects = await api('/api/subjects');
            subjectGrid.replaceChildren(...subjects.map(item => {
                const button = clone('subject-template');
                button.dataset.subjectId = item.id;
                const icon = field(button, 'icon');
                icon.dataset.subject = item.code;
                icon.textContent = icons[item.code] ?? '';
                field(button, 'name').textContent = item.name;
                field(button, 'count').textContent = item.questionCount + ' MCQs';
                button.addEventListener('click', () => selectSubject(item));
                return button;
            }));
            byId('setup-subjects').hidden = subjects.length === 0;
            byId('empty-subjects').hidden = subjects.length > 0;
            const first = subjects.find(item => item.questionCount > 0) ?? subjects[0];
            if (first) selectSubject(first);
        } catch (error) {
            showMessage(byId('setup-error'), error.message);
            byId('empty-subjects').hidden = false;
        } finally { byId('loading-subjects').hidden = true; }
    }

    byId('retry-subjects').addEventListener('click', loadSubjects);
    toggleAll.addEventListener('click', () => {
        selected = new Set(selected.size === subject.chapters.length ? [] : subject.chapters.map(chapter => chapter.id));
        updateChapters();
    });
    diagrams.addEventListener('change', requestCount);
    countInput.addEventListener('input', updateMatches);

    for (const filter of filters) filter.addEventListener('click', () => {
        bloomLevel = filter.dataset.level === 'All' ? null : filter.dataset.level;
        requestCount();
    });
    for (const preset of presets) preset.addEventListener('click', () => {
        countInput.value = preset.dataset.count;
        updateMatches();
    });
    for (const mode of modes) mode.addEventListener('change', () => {
        for (const input of modes) input.closest('label').classList.toggle('selected', input.checked);
    });

    form.addEventListener('submit', async event => {
        event.preventDefault();
        if (startButton.disabled) return;
        starting = true;
        startButton.textContent = 'Starting…';
        showMessage(byId('start-error'));
        updateMatches();
        try {
            const body = { ...currentFilter(), questionCount: Number(countInput.value), mode: modes.find(input => input.checked).value };
            const attempt = await api('/api/exams/start', { method: 'POST', body });
            location.assign('/exam/' + encodeURIComponent(attempt.attemptId));
        } catch (error) {
            showMessage(byId('start-error'), error.message);
        } finally {
            starting = false;
            startButton.textContent = 'Start Test';
            updateMatches();
        }
    });

    void loadSubjects();
}
