import { api, byId, selectButton, session, showMessage } from '../site.js';

const filterKeys = ['fromDate', 'toDate', 'subjectId', 'chapterId', 'studentId', 'device', 'search', 'page', 'pageSize',
    'inactiveDays', 'bloomLevel', 'maxAccuracy', 'suspectedWrongKey', 'neverAttempted', 'eventType'];

export function reportUrl(endpoint, query, overrides = {}) {
    const params = new URLSearchParams([...query].filter(([key]) => filterKeys.includes(key)));
    for (const [key, value] of Object.entries(overrides)) {
        if (value === null) params.delete(key);
        else params.set(key, value);
    }
    return '/api/admin/' + endpoint + (params.size ? '?' + params : '');
}

export function navigateUrl(query, path = location.pathname) {
    return path + (query.size ? '?' + query : '');
}

export function mountReport({ endpoint, url, load, render, exportLabel }) {
    if (!session.get()) return;

    const form = byId('admin-filters');
    const controls = form ? [...form.elements].filter(control => control.name) : [];
    const filterNames = new Set(controls.map(control => control.name));
    const tabs = [...document.querySelectorAll('[data-report-tab]')];
    const report = byId('admin-report');
    const loading = byId('admin-loading');
    const errorBox = byId('admin-error');
    const retry = byId('admin-retry');
    const workspace = byId('admin-workspace');
    const exportButton = byId('admin-export');
    const printButton = byId('admin-print');
    const printScope = byId('admin-print-scope');
    const formatTimestamp = new Intl.DateTimeFormat('en-GB', {
        dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC'
    });

    let request;
    let lastQuery;
    let catalog = [];
    let catalogRequested = false;

    const query = () => new URLSearchParams(location.search);
    const currentTab = params => tabs.find(button => button.dataset.reportTab === params.get('tab'))?.dataset.reportTab
        ?? tabs[0]?.dataset.reportTab;
    const endpointFor = params => typeof endpoint === 'function' ? endpoint(params, currentTab(params)) : endpoint;
    const urlFor = (params, overrides) => url
        ? url(params, currentTab(params), overrides)
        : reportUrl(endpointFor(params), params, overrides);

    function commit(params) {
        const path = navigateUrl(params);
        if (path !== location.pathname + location.search) history.pushState(null, '', path);
        void refresh();
    }

    function updateChapter(subjectId, chapterId, keepUnknown) {
        const chapters = catalog.filter(subject => !subjectId || String(subject.id) === subjectId)
            .flatMap(subject => subject.chapters.map(item => ({
                id: item.id,
                title: subjectId ? item.chapterNo + '. ' + item.title
                    : subject.name + ' · ' + item.chapterNo + '. ' + item.title
            })));
        fillSelect(form.elements.chapterId, 'All chapters', chapters, chapterId, keepUnknown, 'Chapter');
    }

    function syncForm(params) {
        const activeTab = currentTab(params);
        for (const button of tabs) selectButton(button, button.dataset.reportTab === activeTab);
        if (!form) return;

        const inactive = byId('student-inactive-filter');
        if (inactive) {
            inactive.hidden = activeTab !== 'inactive' || params.has('studentId');
            form.elements.inactiveDays.disabled = inactive.hidden;
        }

        const subjectId = params.get('subjectId') ?? '';
        fillSelect(form.elements.subjectId, 'All subjects', catalog.map(item => ({ id: item.id, title: item.name })),
            subjectId, true, 'Subject');
        updateChapter(subjectId, params.get('chapterId') ?? '', true);

        for (const control of controls) {
            if (control.name === 'subjectId' || control.name === 'chapterId') continue;
            const value = params.get(control.name);
            if (control.type === 'checkbox') control.checked = value === 'true';
            else control.value = value ?? (control.name === 'inactiveDays' ? '7' : '');
        }
    }

    function updatePrintScope(params) {
        const subject = form?.elements.subjectId;
        const chapter = form?.elements.chapterId;
        const values = [
            params.has('fromDate') && 'From ' + params.get('fromDate') + ' UTC',
            params.has('toDate') && 'To ' + params.get('toDate') + ' UTC',
            subject?.value && subject.selectedOptions[0]?.textContent,
            chapter?.value && 'Chapter ' + chapter.selectedOptions[0]?.textContent,
            params.has('device') && 'Device ' + params.get('device'),
            params.has('search') && 'Search: ' + params.get('search')
        ].filter(Boolean);
        values.push('Generated ' + formatTimestamp.format(new Date()) + ' UTC');
        showMessage(printScope, values.join(' · '));
    }

    function showWorkspace() {
        workspace.hidden = false;
        if (catalogRequested || !form) return;
        catalogRequested = true;
        api('/api/subjects').then(subjects => {
            catalog = subjects;
            const subject = form.elements.subjectId;
            const subjectId = subject.value;
            const chapterId = form.elements.chapterId.value;
            fillSelect(subject, 'All subjects', catalog.map(item => ({ id: item.id, title: item.name })),
                subjectId, true, 'Subject');
            updateChapter(subjectId, chapterId, true);
            if (lastQuery && subjectId === (lastQuery.get('subjectId') ?? '') &&
                chapterId === (lastQuery.get('chapterId') ?? '')) updatePrintScope(lastQuery);
        }).catch(() => { catalogRequested = false; });
    }

    function denyAccess() {
        request?.abort();
        lastQuery = null;
        workspace.hidden = true;
        report.hidden = true;
        loading.hidden = true;
        retry.hidden = true;
        byId('admin-menu').hidden = true;
        byId('admin-sidebar').hidden = true;
        byId('admin-account').hidden = true;
        byId('admin-shell').classList.remove('with-sidebar');
        showMessage(errorBox);
        byId('admin-denied').hidden = false;
    }

    async function refresh() {
        request?.abort();
        const controller = new AbortController();
        request = controller;
        const { signal } = controller;
        const params = query();
        syncForm(params);
        showMessage(errorBox);
        retry.hidden = true;
        loading.hidden = false;
        report.hidden = true;
        exportButton.disabled = true;
        printButton.disabled = true;
        lastQuery = null;

        try {
            const data = load ? await load(params, signal, urlFor) : await api(urlFor(params), { signal });
            if (signal.aborted) return;
            showWorkspace();
            enableNavigation();
            report.hidden = false;
            render(data, {
                query: params,
                tab: currentTab(params),
                setPage(page) {
                    const next = new URLSearchParams(params);
                    next.set('page', page);
                    commit(next);
                }
            });
            exportButton.textContent = exportLabel?.(params) ?? 'Export CSV';
            lastQuery = params;
            updatePrintScope(params);
            exportButton.disabled = false;
            printButton.disabled = false;
        } catch (error) {
            if (signal.aborted || error.name === 'AbortError') return;
            if (error.status === 403) {
                denyAccess();
                return;
            }

            report.hidden = true;
            if (error.status === 400 || error.status === 404) {
                showWorkspace();
                enableNavigation();
            }
            showMessage(errorBox, error.message);
            retry.hidden = false;
        } finally {
            if (request === controller) loading.hidden = true;
        }
    }

    form?.addEventListener('submit', event => {
        event.preventDefault();
        const params = query();
        for (const key of filterNames) params.delete(key);
        for (const [key, value] of new FormData(form)) {
            if (value.trim()) params.set(key, value.trim());
        }
        params.delete('page');
        commit(params);
    });
    form?.addEventListener('reset', event => {
        event.preventDefault();
        const params = new URLSearchParams();
        const previous = query();
        if (tabs.length && previous.has('tab')) params.set('tab', previous.get('tab'));
        if (!filterNames.has('studentId') && previous.has('studentId')) params.set('studentId', previous.get('studentId'));
        commit(params);
    });
    form?.elements.subjectId.addEventListener('change', () => {
        updateChapter(form.elements.subjectId.value, form.elements.chapterId.value, false);
    });

    for (const button of tabs) button.addEventListener('click', () => {
        const params = query();
        params.set('tab', button.dataset.reportTab);
        params.delete('page');
        commit(params);
    });
    byId('admin-retry-button').addEventListener('click', refresh);
    printButton.addEventListener('click', () => window.print());
    exportButton.addEventListener('click', async () => {
        const params = lastQuery;
        if (!params) return;
        exportButton.disabled = true;
        showMessage(errorBox);
        const activeSession = session.get();
        try {
            const response = await fetch(urlFor(params, { export: 'csv' }), {
                headers: { Authorization: 'Bearer ' + (activeSession?.token ?? '') }
            });
            if (response.status === 401 && session.get()?.token === activeSession?.token) {
                session.set(null);
                location.replace(session.loginUrl());
                return;
            }
            if (response.status === 403 && lastQuery === params) {
                denyAccess();
                return;
            }
            if (!response.ok) throw new Error('CSV export failed. Please try again.');
            const file = await response.blob();
            if (lastQuery !== params) return;

            const href = URL.createObjectURL(file);
            const anchor = document.createElement('a');
            anchor.href = href;
            anchor.download = endpointFor(params).split('/').at(-1) + '.csv';
            document.body.append(anchor);
            anchor.click();
            anchor.remove();
            setTimeout(() => URL.revokeObjectURL(href), 1000);
        } catch (error) {
            if (lastQuery === params) showMessage(errorBox, error.message);
        } finally {
            if (lastQuery === params) exportButton.disabled = false;
        }
    });

    window.addEventListener('popstate', refresh);
    window.addEventListener('pagehide', () => { workspace.hidden = true; });
    window.addEventListener('pageshow', event => { if (event.persisted) location.reload(); });
    let expandedForPrint = [];
    window.addEventListener('beforeprint', () => {
        expandedForPrint = [...report.querySelectorAll('.admin-choices:not([open])')];
        for (const item of expandedForPrint) item.open = true;
    });
    window.addEventListener('afterprint', () => {
        for (const item of expandedForPrint) item.open = false;
        expandedForPrint = [];
    });

    let navigationReady = false;
    function enableNavigation() {
        if (navigationReady) return;
        navigationReady = true;
        const menu = byId('admin-menu');
        const sidebar = byId('admin-sidebar');
        const shell = byId('admin-shell');
        const wideScreen = matchMedia('(min-width: 901px)');
        function sidebarOpen(open) {
            sidebar.hidden = !open;
            shell.classList.toggle('with-sidebar', open);
            menu.setAttribute('aria-expanded', open);
        }

        sidebarOpen(wideScreen.matches);
        wideScreen.addEventListener('change', event => sidebarOpen(event.matches));
        menu.hidden = false;
        menu.addEventListener('click', () => sidebarOpen(sidebar.hidden));
        const account = byId('admin-account');
        account.querySelector('.account-name').textContent = session.get()?.user.fullName ?? '';
        account.hidden = false;
        byId('admin-logout').addEventListener('click', () => {
            session.set(null);
            location.replace('/login');
        });
    }

    void refresh();
}

function fillSelect(select, defaultLabel, items, selected, keepUnknown, itemLabel) {
    select.replaceChildren(new Option(defaultLabel, ''),
        ...items.map(item => new Option(item.title, item.id)));
    if (selected && !items.some(item => String(item.id) === selected) && keepUnknown)
        select.append(new Option(itemLabel + ' ' + selected, selected));
    select.value = selected;
    if (select.selectedIndex < 0) select.value = '';
}
