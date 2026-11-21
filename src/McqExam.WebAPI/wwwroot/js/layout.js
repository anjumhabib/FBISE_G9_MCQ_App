import { byId, session } from './site.js';

const student = session.get()?.user;
if (student) {
    byId('brand').href = '/setup';
    byId('student-nav').hidden = false;
    const account = byId('student-account');
    account.querySelector('.avatar').textContent = student.fullName.slice(0, 1).toUpperCase();
    account.querySelector('.account-name').textContent = student.fullName;
    account.hidden = false;
}

byId('logout').addEventListener('click', () => {
    session.set(null);
    location.replace('/login');
});
document.querySelector('.skip-link').addEventListener('click', event => {
    event.preventDefault();
    byId('content').focus();
});

// Recheck authentication when a full-page navigation is restored from the back-forward cache.
window.addEventListener('pageshow', event => {
    if (!event.persisted) return;
    const current = session.read('mcq-session');
    if (!current && document.body.dataset.protected === 'true') location.replace(session.loginUrl());
    else if (current?.token !== session.get()?.token || document.body.dataset.protected === 'true') location.reload();
});
