class LanguageManager {
    constructor() {
        this.currentLang = localStorage.getItem('appLang') || 'en';
        this.translations = window.TRANSLATIONS;
        this.init();
    }

    init() {
        // Apply initial language
        this.applyLanguage(this.currentLang);

        // Bind toggle button if exists
        const toggleBtn = document.getElementById('langToggle');
        if (toggleBtn) {
            this.updateToggleButton(toggleBtn);
            toggleBtn.addEventListener('click', () => this.toggleLanguage());
        }
    }

    toggleLanguage() {
        this.currentLang = this.currentLang === 'en' ? 'he' : 'en';
        this.applyLanguage(this.currentLang);
        this.updateToggleButton(document.getElementById('langToggle'));
        localStorage.setItem('appLang', this.currentLang);
    }

    applyLanguage(lang) {
        const t = this.translations[lang];
        if (!t) return;

        // Set HTML lang and dir
        document.documentElement.lang = lang;
        document.documentElement.dir = lang === 'he' ? 'rtl' : 'ltr';

        // Update all elements with data-i18n attribute
        const elements = document.querySelectorAll('[data-i18n]');
        elements.forEach(el => {
            const key = el.getAttribute('data-i18n');
            if (t[key]) {
                if (el.tagName === 'INPUT' && el.getAttribute('placeholder')) {
                    el.placeholder = t[key];
                } else {
                    el.innerHTML = t[key];
                }
            }
        });

        // Dispatch event for other scripts (like Chat)
        window.dispatchEvent(new CustomEvent('languageChanged', { detail: { lang } }));
    }

    updateToggleButton(btn) {
        if (!btn) return;
        // If current is EN, button shows HE (option to switch to)
        // Or button shows current? Standard is showing current or option.
        // User requested: "instead of emojis use EN/ HE"
        // Let's show the *current* state or the *switch* option.
        // Usually a toggle shows what it IS or what it WILL BE.
        // Let's make it show the *other* option to click, or just the current one.
        // "switch from english to hebrew".
        // Let's display the code of the *current* language, nicely styled.
        // Actually user said "use EN/ HE".
        btn.textContent = this.currentLang === 'en' ? 'HE' : 'EN';
        btn.title = this.currentLang === 'en' ? 'Switch to Hebrew' : ' Switch to English';
    }

    getText(key) {
        return this.translations[this.currentLang][key] || key;
    }

    static resetToDefault() {
        localStorage.setItem('appLang', 'en');
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.langManager = new LanguageManager();
});
