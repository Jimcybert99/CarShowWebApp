window.darkMode = {
    toggle: function () {
        const next = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-bs-theme', next);
        localStorage.setItem('theme', next);
        return next;
    },
    get: function () {
        return document.documentElement.getAttribute('data-bs-theme') ?? 'light';
    }
};

// Update in real-time when the OS setting changes, but only if the user
// hasn't manually chosen a preference via the toggle.
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
    if (!localStorage.getItem('theme')) {
        document.documentElement.setAttribute('data-bs-theme', e.matches ? 'dark' : 'light');
    }
});
