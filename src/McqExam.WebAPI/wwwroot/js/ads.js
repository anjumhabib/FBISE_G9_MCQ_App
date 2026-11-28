import { api, isAdminSession } from './site.js';

const settings = document.querySelector('meta[name="mcq-ad-settings"]')?.dataset;
let activated = false;
let scriptLoaded = false;

export async function activateAds() {
    if (!settings || activated) return;

    if (settings.adminPreview === 'true') {
        if (!isAdminSession()) return;
        try {
            const settings = await api('/api/admin/ads/settings', { optional: true });
            if (!settings.global.adsEnabled || !settings.global.previewPlaceholderMode) return;
        } catch {
            return;
        }
    }

    if (activated) return;
    activated = true;
    matchMedia('(max-width: 850px)').addEventListener('change', refreshAds);
    window.addEventListener('afterprint', refreshAds);
    refreshAds();
}

export function refreshAds() {
    if (!activated || matchMedia('print').matches) return;

    const pending = [];
    for (const slot of document.querySelectorAll('[data-ad-container]')) {
        slot.hidden = slot.hasAttribute('data-ad-inactive');
        const template = slot.querySelector('template[data-ad-unit]');
        if (template && slot.getClientRects().length && slot.clientWidth > 0)
            pending.push(template);
    }

    if (!settings.scriptUrl || (!pending.length && settings.autoLoad !== 'true')) return;
    if (!scriptLoaded) {
        const script = document.createElement('script');
        script.async = true;
        script.crossOrigin = 'anonymous';
        script.src = settings.scriptUrl;
        document.head.append(script);
        scriptLoaded = true;
    }

    for (const template of pending) {
        template.replaceWith(template.content.cloneNode(true));
        try {
            (window.adsbygoogle ??= []).push({});
        } catch {
            // Leave reserved space in place if the ad network rejects the request.
        }
    }
}
