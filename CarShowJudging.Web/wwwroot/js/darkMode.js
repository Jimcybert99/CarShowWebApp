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
