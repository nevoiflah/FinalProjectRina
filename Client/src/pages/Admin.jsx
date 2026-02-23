import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogOut, ArrowRight } from 'lucide-react';
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
            } catch (err) {
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
                style={{ position: 'absolute', top: 20, left: 20, background: 'var(--primary-blue)', color: 'white', border: 'none', padding: '8px 16px', borderRadius: 8, cursor: 'pointer' }}
            >
                {language === 'he' ? 'עברית' : 'العربية'}
            </button>
            <div className="chat-container" style={{ maxWidth: '1200px' }}>
                <header className="chat-header">
                    <div>
                        <h2>{t('adminDashboard')}</h2>
                        <div style={{ fontSize: '13px', opacity: 0.8 }}>{t('appTitle')}</div>
                    </div>
                    <div style={{ display: 'flex', gap: '12px' }}>
                        <button className="icon-btn" onClick={() => navigate('/')} title={t('backToChat')} style={{ background: 'rgba(255,255,255,0.2)' }}>
                            <ArrowRight size={20} />
                        </button>
                        <button className="icon-btn" onClick={() => {
                            localStorage.removeItem('chatUser');
                            navigate('/login');
                        }} title={t('logout')} style={{ background: 'rgba(255,255,255,0.2)' }}>
                            <LogOut size={20} />
                        </button>
                    </div>
                </header>

                <div className="chat-messages" style={{ display: 'block' }}>
                    {loading ? (
                        <div>Loading insights...</div>
                    ) : error ? (
                        <div className="error-text">{error}</div>
                    ) : (
                        <div style={{ overflowX: 'auto' }}>
                            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'right' }}>
                                <thead>
                                    <tr style={{ borderBottom: '2px solid var(--primary-blue)', color: 'var(--primary-blue)' }}>
                                        <th style={{ padding: '12px', width: '15%' }}>{t('user')}</th>
                                        <th style={{ padding: '12px', width: '25%' }}>{t('firstTopic')}</th>
                                        <th style={{ padding: '12px', width: '45%' }}>{t('finalAnalysis')}</th>
                                        <th style={{ padding: '12px', width: '15%' }}>{t('date')}</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {stats.map((s, idx) => (
                                        <tr key={idx} style={{ borderBottom: '1px solid #e2e8f0', background: idx % 2 === 0 ? '#f7fafc' : 'white' }}>
                                            <td style={{ padding: '12px', fontWeight: '500' }}>{s.userName}</td>
                                            <td style={{ padding: '12px' }}>{s.question || 'N/A'}</td>
                                            <td style={{ padding: '12px' }}>{s.result || 'N/A'}</td>
                                            <td style={{ padding: '12px', color: '#718096', fontSize: '14px' }}>
                                                {new Date(s.date).toLocaleDateString('he-IL')}
                                            </td>
                                        </tr>
                                    ))}
                                    {stats.length === 0 && (
                                        <tr>
                                            <td colSpan="4" style={{ padding: '24px', textAlign: 'center', color: '#718096' }}>
                                                {t('noHistory')}
                                            </td>
                                        </tr>
                                    )}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default Admin;
