import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Globe } from 'lucide-react';
import { registerUser } from '../api';
import { useLanguage } from '../context/LanguageContext';

const Register = () => {
    const [name, setName] = useState('');
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();
    const { t, language, toggleLanguage } = useLanguage();

    const handleRegister = async (e) => {
        e.preventDefault();
        setError('');

        if (!name || !email || !password) {
            setError(t('inputError'));
            return;
        }

        try {
            setLoading(true);
            await registerUser({ name, email, password });

            // Auto login or redirect to login
            navigate('/login');
        } catch (err) {
            setError(err.response?.data?.error || err.message || 'Registration failed.');
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
                    <h1>{t('registerTitle')}</h1>
                    <p>{t('createAccount')}</p>
                </div>

                <form className="auth-form" onSubmit={handleRegister}>
                    <input
                        type="text"
                        className="input-field"
                        placeholder={t('namePlaceholder')}
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                    />
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
                        {loading ? '...' : t('createAccount')}
                    </button>
                </form>

                <button className="link-btn" onClick={() => navigate('/login')}>
                    {t('loginPrompt')}
                </button>
            </div>
        </div>
    );
};

export default Register;
