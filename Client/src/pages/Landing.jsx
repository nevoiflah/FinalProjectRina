import { useNavigate } from 'react-router-dom';
import { Globe, Mic, Bot, Languages } from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';

const Landing = () => {
    const navigate = useNavigate();
    const { t, language, toggleLanguage } = useLanguage();

    return (
        <div className="app-container" dir={language === 'he' || language === 'ar' ? 'rtl' : 'ltr'}>
            <button
                onClick={toggleLanguage}
                className="language-toggle-btn"
                aria-label={language === 'he' ? 'עברית' : 'العربية'}
            >
                <Globe size={16} />
                <span>{language === 'he' ? 'עברית' : 'العربية'}</span>
            </button>

            <div className="landing-card">
                <div className="landing-hero">
                    <img src="/logo.png" alt="Ruppin Logo" className="landing-logo" width="84" height="84" />
                    <h1 className="landing-title">{t('landingHero')}</h1>
                    <p className="landing-subtitle">{t('landingSubtitle')}</p>

                    <div className="landing-cta-row">
                        <button
                            className="primary-btn landing-cta-btn"
                            onClick={() => navigate('/login')}
                        >
                            {t('loginBtn')}
                        </button>
                        <button
                            className="landing-outline-btn landing-cta-btn"
                            onClick={() => navigate('/register')}
                        >
                            {t('registerBtn')}
                        </button>
                    </div>
                </div>

                <div className="landing-features">
                    <div className="landing-feature">
                        <div className="landing-feature-icon" aria-hidden="true">
                            <Mic size={22} />
                        </div>
                        <div className="landing-feature-text">
                            <div className="landing-feature-title">{t('feature1Title')}</div>
                            <div className="landing-feature-desc">{t('feature1Desc')}</div>
                        </div>
                    </div>
                    <div className="landing-feature">
                        <div className="landing-feature-icon" aria-hidden="true">
                            <Bot size={22} />
                        </div>
                        <div className="landing-feature-text">
                            <div className="landing-feature-title">{t('feature2Title')}</div>
                            <div className="landing-feature-desc">{t('feature2Desc')}</div>
                        </div>
                    </div>
                    <div className="landing-feature">
                        <div className="landing-feature-icon" aria-hidden="true">
                            <Languages size={22} />
                        </div>
                        <div className="landing-feature-text">
                            <div className="landing-feature-title">{t('feature3Title')}</div>
                            <div className="landing-feature-desc">{t('feature3Desc')}</div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Landing;
