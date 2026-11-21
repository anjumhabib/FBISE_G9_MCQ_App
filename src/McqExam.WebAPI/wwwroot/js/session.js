(() => {
    const key = 'mcq-session';

    function read(key) {
        try { return JSON.parse(sessionStorage.getItem(key)); }
        catch { return null; }
    }

    function write(key, value) {
        try {
            if (value === null) sessionStorage.removeItem(key);
            else sessionStorage.setItem(key, JSON.stringify(value));
        } catch { /* The app can run with browser storage disabled. */ }
    }

    let current = read(key);
    function set(value) {
        current = value;
        write(key, value);
    }

    function get() {
        if (current && Date.parse(current.expiresAt) > Date.now()) return current;
        if (current) set(null);
        return null;
    }

    function loginUrl() {
        return '/login?' + new URLSearchParams({ returnUrl: location.pathname + location.search });
    }

    window.mcqSession = { get, set, read, write, loginUrl };

    if (!get() && !['/login', '/register'].includes(location.pathname.toLowerCase().replace(/\/$/, ''))) {
        document.documentElement.classList.add('redirecting');
        location.replace(loginUrl());
    }
})();
