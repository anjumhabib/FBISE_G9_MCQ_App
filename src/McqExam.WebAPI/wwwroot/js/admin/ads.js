import { api, byId, clone, field, showMessage } from '../site.js';
import { mountReport } from './report.js';

const settingsPath = '/api/admin/ads/settings';
const titles = {
    PublicHome: 'Public Home',
    PublicSubjectGuide: 'Subject Guides',
    PublicSloGuide: 'FBISE SLO Guide',
    SampleQuiz: 'Sample Quiz',
    TestSetup: 'Test Setup',
    ExamRunner: 'Exam Runner',
    AnswerReview: 'Answer Review',
    HistoryReport: 'History & Reports'
};
const globalForm = byId('ad-global-form');

mountReport({
    endpoint: 'ads/settings',
    render(settings) {
        setGlobal(settings.global);
        byId('ad-pages').replaceChildren(...settings.pages.map(makePage));
    }
});

function setGlobal(global) {
    const inputs = globalForm.elements;
    inputs.adsEnabled.checked = global.adsEnabled;
    inputs.publisherId.value = global.publisherId ?? '';
    inputs.autoAdsEnabled.checked = global.autoAdsEnabled;
    inputs.previewPlaceholderMode.checked = global.previewPlaceholderMode;
    inputs.inFeedInterval.value = global.inFeedInterval;
    inputs.adsTxtContent.value = global.adsTxtContent ?? '';
}

globalForm.addEventListener('submit', async event => {
    event.preventDefault();
    const inputs = globalForm.elements;
    const fields = globalForm.querySelector('fieldset');
    if (fields.disabled) return;
    const body = {
        adsEnabled: inputs.adsEnabled.checked,
        publisherId: inputs.publisherId.value.trim() || null,
        autoAdsEnabled: inputs.autoAdsEnabled.checked,
        previewPlaceholderMode: inputs.previewPlaceholderMode.checked,
        inFeedInterval: Number(inputs.inFeedInterval.value),
        adsTxtContent: inputs.adsTxtContent.value
    };
    await save({
        path: '/api/admin/ads/global', body,
        status: globalForm.querySelector('[role=status]'), error: globalForm.querySelector('[role=alert]'),
        lock: busy => { fields.disabled = busy; },
        async onSaved() {
            const settings = await api(settingsPath);
            setGlobal(settings.global);
        },
        message: 'Global settings saved.'
    });
});

function makePage(page) {
    const card = clone('ad-page-template');
    const enable = field(card, 'enable');
    const disable = field(card, 'disable');
    const slots = [];
    let busy = false;
    field(card, 'name').textContent = titles[page.pageKey] ?? page.pageKey;

    function updateSummary() {
        const enabled = page.placements.filter(slot => slot.isEnabled).length;
        field(card, 'summary').textContent = enabled + ' of ' + slots.length + ' placements enabled';
        enable.disabled = busy || enabled === slots.length;
        disable.disabled = busy || enabled === 0;
    }

    function lock(value) {
        busy = value;
        for (const slot of slots) slot.querySelector('fieldset').disabled = value;
        updateSummary();
    }

    for (const slot of page.placements) {
        const form = clone('ad-placement-template');
        const inputs = form.elements;
        field(form, 'name').textContent = slot.displayName;
        field(form, 'description').textContent = slot.description;
        inputs.isEnabled.checked = slot.isEnabled;
        inputs.adSlotId.value = slot.adSlotId ?? '';
        inputs.hideOnMobile.checked = slot.hideOnMobile;
        inputs.hideOnDesktop.checked = slot.hideOnDesktop;
        inputs.minHeightPx.value = slot.minHeightPx;
        slots.push(form);
        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (busy) return;
            const body = {
                isEnabled: inputs.isEnabled.checked,
                adSlotId: inputs.adSlotId.value.trim() || null,
                hideOnMobile: inputs.hideOnMobile.checked,
                hideOnDesktop: inputs.hideOnDesktop.checked,
                minHeightPx: Number(inputs.minHeightPx.value)
            };
            showMessage(field(card, 'status'));
            showMessage(field(card, 'error'));
            await save({
                path: '/api/admin/ads/placements/' + encodeURIComponent(slot.id), body, lock,
                status: form.querySelector('[role=status]'), error: form.querySelector('[role=alert]'),
                onSaved() { Object.assign(slot, body); },
                message: 'Placement saved.'
            });
        });
    }
    field(card, 'placements').append(...slots);

    async function toggle(isEnabled) {
        if (busy) return;
        for (const form of slots) showMessage(form.querySelector('[role=status]'));
        await save({
            path: '/api/admin/ads/pages/' + encodeURIComponent(page.pageKey) + '/toggle',
            body: { isEnabled }, lock,
            status: field(card, 'status'), error: field(card, 'error'),
            onSaved() {
                for (const slot of page.placements) slot.isEnabled = isEnabled;
                for (const form of slots) form.elements.isEnabled.checked = isEnabled;
            },
            message: isEnabled ? 'All placements enabled.' : 'All placements disabled.'
        });
    }

    enable.addEventListener('click', () => toggle(true));
    disable.addEventListener('click', () => toggle(false));
    updateSummary();
    return card;
}

async function save({ path, body, status, error, lock, onSaved, message }) {
    showMessage(error);
    showMessage(status, 'Saving…');
    lock(true);
    try {
        await api(path, { method: 'PUT', body });
        await onSaved();
        showMessage(status, message);
    } catch (failure) {
        showMessage(status);
        if (failure.status === 403) {
            byId('admin-workspace').hidden = true;
            location.reload();
        } else if (failure.status !== 401) {
            showMessage(error, failure.message);
        }
    } finally {
        lock(false);
    }
}
