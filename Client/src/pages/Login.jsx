import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Globe } from 'lucide-react';
import { loginUser } from '../api';
import { useLanguage } from '../context/LanguageContext';

const Login = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
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
            navigate('/');
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
            >
                <Globe size={16} />
                <span>{language === 'he' ? 'עברית' : 'العربية'}</span>
            </button>

            <div className="auth-card">
                <div className="auth-header">
                    <img src="/logo.png" alt="Ruppin Logo" />
                    <h1>{t('appTitle')}</h1>
                    <p>{t('appSubtitle')}</p>
                </div>

                <form className="auth-form" onSubmit={handleLogin}>
                    <input
                        type="email"
                        className="input-field"
                        placeholder={t('emailPlaceholder')}
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                    />
                    <input
                        type="password"
                        className="input-field"
                        placeholder={t('passwordPlaceholder')}
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                    />

                    {error && <div className="error-text">{error}</div>}

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
