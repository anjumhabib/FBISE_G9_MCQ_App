(() => {
    const key = 'mcq-session';
    const protectedPage = document.currentScript.dataset.protected === 'true';

    function read(key) {
        try { return JSON.parse(sessionStorage.getItem(key)); }
        catch { return null; }
    }

    function write(key, value) {
        try {
            if (value === null) sessionStorage.removeItem(key);
            else sessionStorage.setItem(key, JSON.stringify(value));
        } catch { /* Cookie recovery remains available when browser storage is disabled. */ }
    }

    let current = read(key);
    let refreshing;
    const get = () => current;
    function set(value) {
        current = value;
        write(key, value);
    }

    function loginUrl(expired = false) {
        const query = new URLSearchParams({ returnUrl: location.pathname + location.search });
        if (expired) query.set('reason', 'expired');
        return '/login?' + query;
    }

    function withCookie(action) {
        return navigator.locks ? navigator.locks.request('mcq-refresh-cookie', action) : action();
    }

    async function cookieRequest(action) {
        const response = await fetch('/api/auth/' + action, {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}',
            signal: AbortSignal.timeout(10000)
        });
        if (action === 'refresh' && response.status === 401) return null;
        if (!response.ok) throw new Error('The session service is unavailable. Please try again.');
        return action === 'refresh' ? response.json() : null;
    }

    function refresh() {
        refreshing ??= withCookie(async () => {
            let renewed = await cookieRequest('refresh');
            if (!renewed && !navigator.locks) {
                // Another tab may have won the rotation. Retry with the newest cookie.
                await new Promise(resolve => setTimeout(resolve, 250));
                renewed = await cookieRequest('refresh');
            }
            if (renewed?.token) set(renewed);
            return renewed;
        }).finally(() => { refreshing = undefined; });
        return refreshing;
    }

    async function logout() {
        try {
            await withCookie(async () => {
                await cookieRequest('logout');
                set(null);
                location.replace('/login');
            });
        } catch {
            throw new Error('Sign out could not be completed. Check your connection and try again.');
        }
    }

    async function restore() {
        if (!protectedPage || (current?.token && Date.parse(current.expiresAt) > Date.now())) return;
        try {
            if (await refresh() || current?.token) return;
            document.documentElement.classList.add('redirecting');
            location.replace(loginUrl());
        } catch {
            if (current?.token) return;
            if (!document.body) {
                await new Promise(resolve => document.addEventListener('DOMContentLoaded', resolve, { once: true }));
            }
            const message = document.createElement('p');
            message.setAttribute('role', 'alert');
            message.className = 'alert';
            message.append('The session could not be restored. ');
            const retry = document.createElement('button');
            retry.type = 'button';
            retry.textContent = 'Retry';
            retry.addEventListener('click', () => location.reload());
            message.append(retry);
            (document.querySelector('main') ?? document.body).prepend(message);
        }
    }

    window.mcqSession = { get, set, read, write, loginUrl, withCookie, refresh, logout, ready: restore() };
})();
