import { byId, isAdminSession, session } from './site.js';
import { activateAds } from './ads.js';

const student = session.get()?.user;
if (student) {
    const dashboard = byId('dashboard-link');
    dashboard.href = '/setup';
    dashboard.textContent = 'Dashboard';
    byId('history-link').hidden = false;
    byId('admin-shortcut').hidden = !isAdminSession();
    const account = byId('student-account');
    account.querySelector('.avatar').textContent = student.fullName.slice(0, 1).toUpperCase();
    account.querySelector('.account-name').textContent = student.fullName;
    account.hidden = false;
}
if (document.body.dataset.publicAds === 'true') void activateAds();

const logout = byId('logout');
logout.addEventListener('click', async () => {
    logout.disabled = true;
    try { await session.logout(); }
    catch {
        logout.textContent = 'Retry sign out';
        logout.disabled = false;
    }
});
document.querySelector('.skip-link').addEventListener('click', event => {
    event.preventDefault();
    byId('content').focus();
});

// Recheck authentication when a full-page navigation is restored from the back-forward cache.
window.addEventListener('pageshow', event => {
    if (!event.persisted) return;
    const current = session.read('mcq-session');
    if (current?.token !== session.get()?.token || document.body.dataset.protected === 'true') location.reload();
});
