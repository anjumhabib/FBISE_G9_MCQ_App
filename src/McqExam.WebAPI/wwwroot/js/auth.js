import { api, byId, localUrl, session, showMessage } from './site.js';

const form = byId('auth-form');
const button = form.querySelector('button[type="submit"]');
const idleLabel = button.textContent;
const errorBox = byId('auth-error');
const fields = [...form.querySelectorAll('input')];

function validate(input) {
    const valid = input.checkValidity();
    byId(input.id + '-error').hidden = valid;
    input.setAttribute('aria-invalid', !valid);
    return valid;
}

for (const input of fields) {
    input.addEventListener('blur', () => validate(input));
    input.addEventListener('input', () => {
        if (input.hasAttribute('aria-invalid')) validate(input);
    });
}

form.addEventListener('submit', async event => {
    event.preventDefault();
    if (button.disabled || !fields.map(validate).every(Boolean)) return;
    button.disabled = true;
    button.textContent = 'Please wait…';
    showMessage(errorBox);
    try {
        const body = Object.fromEntries(new FormData(form));
        session.set(await api('/api/auth/' + form.dataset.mode, { method: 'POST', body }));
        const destination = new URLSearchParams(location.search).get('returnUrl');
        const target = destination && localUrl(destination);
        location.replace(target && /^\/(?:setup|history|exam\/[^/]+|review\/[^/]+)\/?$/i.test(target.pathname)
            ? target.pathname + target.search : '/setup');
    } catch (error) {
        showMessage(errorBox, error.message);
    } finally {
        button.disabled = false;
        button.textContent = idleLabel;
    }
});
