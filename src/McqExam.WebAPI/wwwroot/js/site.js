export const session = window.mcqSession;
export const byId = id => document.getElementById(id);
export const letters = ['A', 'B', 'C', 'D'];
export const field = (root, name) => root.querySelector('[data-field="' + name + '"]');
export const clone = id => byId(id).content.firstElementChild.cloneNode(true);

export function showMessage(element, message = '') {
    element.textContent = message;
    element.hidden = !message;
}

export async function api(path, { method = 'GET', body, signal } = {}) {
    const authRequest = path.startsWith('/api/auth/');
    const user = authRequest ? null : session.get();
    const headers = {};
    if (user) headers.Authorization = 'Bearer ' + user.token;
    if (body !== undefined) headers['Content-Type'] = 'application/json';

    let response;
    try {
        response = await fetch(path, { method, headers, signal, body: body === undefined ? undefined : JSON.stringify(body) });
    } catch (error) {
        if (error.name === 'AbortError') throw error;
        throw new Error('The server could not be reached. Check your connection and try again.');
    }
    if (response.status === 204) return;
    const data = await response.json().catch(() => null);
    if (response.ok && data !== null) return data;

    if (response.status === 401 && !authRequest && session.get()?.token === user?.token) {
        session.set(null);
        location.replace(session.loginUrl());
    }
    const message = response.status === 429 ? 'Too many sign-in attempts. Please wait a minute and try again.'
        : data?.detail || (data?.errors && Object.values(data.errors).flat().join(' '))
        || 'Something went wrong. Please try again.';
    throw Object.assign(new Error(message), { status: response.status });
}

export function duration(seconds) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainder = seconds % 60;
    return hours ? hours + 'h ' + minutes + 'm' : minutes + 'm ' + remainder + 's';
}

const dateFormats = {
    medium: { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit', second: '2-digit' },
    mediumDate: { month: 'short', day: 'numeric', year: 'numeric' },
    longDate: { month: 'long', day: 'numeric', year: 'numeric' }
};

export function formatDate(value, format = 'mediumDate') {
    return new Intl.DateTimeFormat('en-US', dateFormats[format]).format(new Date(value));
}

export function selectButton(button, selected, className = 'active') {
    button.classList.toggle(className, selected);
    button.setAttribute('aria-pressed', selected);
}

export function renderFormula(element, value) {
    // Decode text entities and allow only the two formula tags. All other input stays text.
    const tokens = value.split(/(<\/?(?:sub|sup)>|&(?:amp|lt|gt|quot|apos|#\d+|#x[0-9a-f]+);)/gi);
    const stack = [document.createDocumentFragment()];
    const decoder = document.createElement('textarea');
    for (const token of tokens) {
        const current = stack.at(-1);
        const tag = /^<(\/?)(sub|sup)>$/i.exec(token);
        if (tag) {
            const name = tag[2].toLowerCase();
            if (!tag[1]) {
                const child = document.createElement(name);
                current.append(child);
                stack.push(child);
            } else if (current.localName === name) stack.pop();
        } else if (token.startsWith('&') && token.endsWith(';') && !token.includes('<')) {
            decoder.innerHTML = token;
            current.append(decoder.value);
        } else current.append(token);
    }
    element.replaceChildren(stack[0]);
}

export function renderDiagram(element, path) {
    const url = path && localUrl(path);
    element.hidden = !url || !url.pathname.startsWith('/images/');
    if (element.hidden) element.removeAttribute('src');
    else element.src = url.href;
}

export function localUrl(value) {
    try {
        const url = new URL(value, location.origin);
        return url.origin === location.origin ? url : null;
    } catch { return null; }
}

export function renderBloom(container, items) {
    container.replaceChildren(...items.map(item => {
        const row = clone('bloom-row');
        const { total, correct, percent } = item.accuracy;
        field(row, 'level').textContent = item.level;
        field(row, 'accuracy').textContent = total ? percent + '% · ' + correct + '/' + total : 'No questions';
        const bar = row.querySelector('progress');
        bar.value = percent;
        bar.setAttribute('aria-label', item.level + ' accuracy');
        return row;
    }));
}
