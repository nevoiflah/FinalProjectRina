import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Globe, Eye, EyeOff, AlertCircle } from 'lucide-react';
import { loginUser } from '../api';
import { useLanguage } from '../context/LanguageContext';

const Login = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();
    const { t, language, toggleLanguage } = useLanguage();

    const handleLogin = async (e) => {
        e.preventDefault();
        setError('');

        if (!email || !password) {
            setError(t('inputError'));
            return;
        }

        try {
            setLoading(true);
            const user = await loginUser(email, password);
            localStorage.setItem('chatUser', JSON.stringify(user));
            navigate('/chat');
        } catch (err) {
            setError(err.response?.data?.error || err.message || 'Login failed.');
        } finally {
            setLoading(false);
        }
    };

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

            <div className="auth-card">
                <div className="auth-header">
                    <img src="/logo.png" alt="Ruppin Logo" width="70" height="70" />
                    <h1>{t('appTitle')}</h1>
                    <p>{t('appSubtitle')}</p>
                </div>

                <form className="auth-form" onSubmit={handleLogin}>
                    <div>
                        <label htmlFor="login-email" className="sr-only">{t('emailPlaceholder')}</label>
                        <input
                            id="login-email"
                            type="email"
                            className="input-field"
                            placeholder={t('emailPlaceholder')}
                            value={email}
                            autoFocus
                            autoComplete="email"
                            onChange={(e) => setEmail(e.target.value)}
                        />
                    </div>

                    <div>
                        <label htmlFor="login-password" className="sr-only">{t('passwordPlaceholder')}</label>
                        <div className="input-wrapper">
                            <input
                                id="login-password"
                                type={showPassword ? 'text' : 'password'}
                                className="input-field has-toggle"
                                placeholder={t('passwordPlaceholder')}
                                value={password}
                                autoComplete="current-password"
                                onChange={(e) => setPassword(e.target.value)}
                            />
                            <button
                                type="button"
                                className="password-toggle-btn"
                                onClick={() => setShowPassword(p => !p)}
                                aria-label={showPassword ? t('hidePassword') : t('showPassword')}
                            >
                                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                            </button>
                        </div>
                    </div>

                    {error && (
                        <div className="error-text" role="alert">
                            <AlertCircle size={14} aria-hidden="true" />
                            {error}
                        </div>
                    )}

                    <button type="submit" className="primary-btn" disabled={loading}>
                        {loading ? '...' : t('signIn')}
                    </button>
                </form>

                <button className="link-btn" onClick={() => navigate('/register')}>
                    {t('registerPrompt')}
                </button>
            </div>
        </div>
    );
};

export default Login;
