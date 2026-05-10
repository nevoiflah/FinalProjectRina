import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogOut, ArrowRight, Globe } from 'lucide-react';
import { fetchAdminStats } from '../api';
import { useLanguage } from '../context/LanguageContext';

const Admin = () => {
    const [stats, setStats] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const navigate = useNavigate();
    const { t, language, toggleLanguage } = useLanguage();

    const userStr = localStorage.getItem('chatUser');
    const user = userStr ? JSON.parse(userStr) : null;

    useEffect(() => {
        const loadStats = async () => {
            try {
                const data = await fetchAdminStats(user?.userId || user?.UserId);
                setStats(data);
            } catch {
                setError('Failed to load stats. Check server connection.');
            } finally {
                setLoading(false);
            }
        };
        loadStats();
    }, [user]);

    return (
        <div className="app-container" dir={language === 'he' || language === 'ar' ? 'rtl' : 'ltr'}>
            <button
                onClick={toggleLanguage}
                className="language-toggle-btn"
            >
                <Globe size={16} />
                <span>{language === 'he' ? 'עברית' : 'العربية'}</span>
            </button>
            <div className="chat-container admin-container">
                <header className="chat-header">
                    <div className="chat-title-block">
                        <h2>{t('adminDashboard')}</h2>
                        <div className="chat-subtitle">{t('appTitle')}</div>
                    </div>
                    <div className="header-actions">
                        <button className="icon-btn header-icon-btn" onClick={() => navigate('/')} title={t('backToChat')}>
                            <ArrowRight size={20} />
                        </button>
                        <button className="icon-btn" onClick={() => {
                            localStorage.removeItem('chatUser');
                            navigate('/login');
                        }} title={t('logout')}>
                            <LogOut size={20} />
                        </button>
                    </div>
                </header>

                <div className="chat-messages admin-content">
                    {loading ? (
                        <div className="loading-state">Loading insights...</div>
                    ) : error ? (
                        <div className="error-text">{error}</div>
                    ) : (
                        <>
                            <div className="admin-summary">
                                <div className="stat-card">
                                    <div className="stat-label">{t('finalAnalysis')}</div>
                                    <div className="stat-value">{stats.length}</div>
                                </div>
                                <div className="stat-card">
                                    <div className="stat-label">{t('user')}</div>
                                    <div className="stat-value">{new Set(stats.map(s => s.userName)).size}</div>
                                </div>
                                <div className="stat-card">
                                    <div className="stat-label">{t('date')}</div>
                                    <div className="stat-value">
                                        {stats[0]?.date ? new Date(stats[0].date).toLocaleDateString('he-IL') : '-'}
                                    </div>
                                </div>
                            </div>

                            <div className="admin-table-wrap">
                                <table className="admin-table">
                                    <thead>
                                        <tr>
                                            <th>{t('user')}</th>
                                            <th>{t('firstTopic')}</th>
                                            <th>{t('finalAnalysis')}</th>
                                            <th>{t('date')}</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {stats.map((s, idx) => (
                                            <tr key={idx}>
                                                <td className="admin-user-cell">{s.userName}</td>
                                                <td>{s.question || 'N/A'}</td>
                                                <td>{s.result || 'N/A'}</td>
                                                <td className="admin-date-cell">
                                                    {new Date(s.date).toLocaleDateString('he-IL')}
                                                </td>
                                            </tr>
                                        ))}
                                        {stats.length === 0 && (
                                            <tr>
                                                <td colSpan="4" className="empty-state">
                                                    {t('noHistory')}
                                                </td>
                                            </tr>
                                        )}
                                    </tbody>
                                </table>
                            </div>
                        </>
                    )}
                </div>
            </div>
        </div>
    );
};

export default Admin;
