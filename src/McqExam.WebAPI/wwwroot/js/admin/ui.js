import { duration } from '../site.js';

const number = new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 });
const dateOnly = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeZone: 'UTC' });
const dateAndTime = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' });

export const noValue = '—';
export const count = value => value == null ? noValue : number.format(value);
export const percent = value => value == null ? noValue : count(value) + '%';

export function deviceBreakdown(totals) {
    const labels = { mobile: 'Mobile', desktop: 'Desktop', tablet: 'Tablet', bot: 'Bot', unknown: 'Unknown' };
    return Object.entries(labels).filter(([key]) => totals?.[key] > 0)
        .map(([key, label]) => label + ' ' + count(totals[key])).join(' · ') || noValue;
}
export const date = value => value ? dateOnly.format(new Date(value)) : noValue;
export const timestamp = value => value ? dateAndTime.format(new Date(value)) : noValue;
export const elapsed = value => value == null ? noValue : duration(value);

export function element(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text != null) node.textContent = text;
    return node;
}

export function link(text, url, className = '') {
    const node = element('a', className, text);
    node.href = url;
    return node;
}

export function badge(text, tone = 'slate') {
    return element('span', 'badge ' + tone, text);
}

export function empty(message) {
    return element('p', 'empty-inline', message);
}

export function facts(container, entries) {
    container.replaceChildren(...entries.map(([label, value]) => {
        const node = element('div');
        const data = element('dd');
        data.append(value ?? noValue);
        node.append(element('dt', '', label), data);
        return node;
    }));
}

export function table(container, { label, headers, rows, emptyText = 'No results match these filters.', wrap = [] }) {
    if (!rows.length) {
        container.replaceChildren(empty(emptyText));
        return;
    }

    const region = element('div', 'table-scroll admin-table-scroll');
    region.setAttribute('role', 'region');
    region.setAttribute('aria-label', label);
    region.tabIndex = 0;
    const grid = element('table');
    const head = grid.createTHead().insertRow();
    for (const label of headers) {
        const heading = element('th', '', label);
        heading.scope = 'col';
        head.append(heading);
    }

    const body = grid.createTBody();
    for (const values of rows) {
        const row = body.insertRow();
        for (const [index, value] of values.entries()) {
            const cell = row.insertCell();
            if (wrap.includes(index)) cell.className = 'admin-wrap';
            cell.append(value ?? noValue);
        }
    }
    region.append(grid);
    container.replaceChildren(region);
}

export function paginate(container, page, setPage, noun = 'results') {
    container.replaceChildren();
    if (!page.totalItems) return;

    const last = Math.ceil(page.totalItems / page.pageSize);
    const start = (page.page - 1) * page.pageSize + 1;
    const end = Math.min(page.page * page.pageSize, page.totalItems);
    const controls = element('div', 'admin-pagination');
    const range = element('span', '', start <= end
        ? count(start) + '–' + count(end) + ' of ' + count(page.totalItems) + ' ' + noun
        : count(page.totalItems) + ' ' + noun);
    range.setAttribute('role', 'status');
    controls.append(range);
    if (last > 1 || page.page > last) {
        const navigation = element('nav', 'admin-actions screen-only');
        navigation.setAttribute('aria-label', noun + ' pages');
        const previous = pageButton('Previous', page.page <= 1,
            () => setPage(Math.min(page.page - 1, last)));
        const next = pageButton('Next', page.page >= last, () => setPage(page.page + 1));
        navigation.append(previous, element('span', '', 'Page ' + count(page.page) + ' of ' + count(last)), next);
        controls.append(navigation);
    }
    container.append(controls);
}

function pageButton(label, disabled, action) {
    const button = element('button', 'button secondary small', label);
    button.type = 'button';
    button.disabled = disabled;
    button.addEventListener('click', action);
    return button;
}

export function attemptLink(attempt, label = 'Review test') {
    return link(label, '/admin/attempts/' + encodeURIComponent(attempt.id), 'button secondary small');
}

export function renderAttempts(container, attempts, { showStudent = false } = {}) {
    table(container, {
        label: 'Test attempts',
        headers: ['Started (UTC)', ...(showStudent ? ['Student'] : []), 'Subject', 'Mode', 'Start device', 'Score', 'Time', 'Review'],
        rows: attempts.map(attempt => [
            timestamp(attempt.startedAt),
            ...(showStudent ? [link(attempt.studentName, '/admin/students?studentId=' + encodeURIComponent(attempt.studentId))] : []),
            attempt.subject,
            attempt.mode,
            attempt.device ?? 'Unknown',
            attempt.completedAt ? percent(attempt.scorePercent) : badge('Incomplete'),
            attempt.completedAt ? elapsed(attempt.timeSpentSeconds) : noValue,
            attemptLink(attempt)
        ]),
        emptyText: 'No tests match these filters.'
    });
}
