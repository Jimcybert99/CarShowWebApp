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

// Always respond to OS/browser preference changes and keep localStorage in sync.
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
    const theme = e.matches ? 'dark' : 'light';
    document.documentElement.setAttribute('data-bs-theme', theme);
    localStorage.setItem('theme', theme);
});
