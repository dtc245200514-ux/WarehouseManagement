(() => {
    const form = document.getElementById('ai-analysis-form');
    const button = document.getElementById('ai-generate');
    const progress = document.getElementById('ai-progress');
    form?.addEventListener('submit', (event) => {
        if (button.disabled) { event.preventDefault(); return; }
        button.disabled = true;
        progress.hidden = false;
        form.setAttribute('aria-busy', 'true');
    });
    window.addEventListener('pageshow', () => {
        if (!form) return;
        button.disabled = false;
        progress.hidden = true;
        form.removeAttribute('aria-busy');
    });
})();
