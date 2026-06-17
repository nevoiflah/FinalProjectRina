import { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogOut, ArrowRight, Globe, Check, X, Trash2 } from 'lucide-react';
import {
    fetchAdminStats,
    fetchLearningQueue,
    fetchLearnedFacts,
    approveLearningCandidate,
    rejectLearningCandidate,
    pruneLearnedFact,
} from '../api';
import { useLanguage } from '../context/LanguageContext';

const Admin = () => {
    const [tab, setTab] = useState('sessions');
    const [stats, setStats] = useState([]);
    const [candidates, setCandidates] = useState([]);
    const [learned, setLearned] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const navigate = useNavigate();
    const { t, language, toggleLanguage } = useLanguage();

    const userStr = localStorage.getItem('chatUser');
    const user = userStr ? JSON.parse(userStr) : null;
    const adminId = user?.userId || user?.UserId;

    useEffect(() => {
        const loadStats = async () => {
            try {
                const data = await fetchAdminStats(adminId);
                setStats(data);
            } catch {
                setError('Failed to load stats. Check server connection.');
            } finally {
                setLoading(false);
            }
        };
        loadStats();
    }, [adminId]);

    const loadLearning = useCallback(async () => {
        try {
            const [queue, facts] = await Promise.all([
                fetchLearningQueue(adminId),
                fetchLearnedFacts(adminId),
            ]);
            setCandidates(queue);
            setLearned(facts);
        } catch {
            setError('Failed to load learning data.');
        }
    }, [adminId]);

    useEffect(() => {
        if (tab === 'learning') loadLearning();
    }, [tab, loadLearning]);

    const handleApprove = async (id) => {
        await approveLearningCandidate(adminId, id);
        await loadLearning();
    };

    const handleReject = async (id) => {
        await rejectLearningCandidate(adminId, id);
        setCandidates(prev => prev.filter(c => c.id !== id));
    };

    const handlePrune = async (id) => {
        await pruneLearnedFact(adminId, id);
        setLearned(prev => prev.filter(f => f.id !== id));
    };

    const confidenceLabel = (c) => `${Math.round(c * 100)}%`;

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
                        <button className="icon-btn header-icon-btn" onClick={() => navigate('/chat')} title={t('backToChat')}>
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

                <div className="admin-tabs">
                    <button
                        className={`admin-tab ${tab === 'sessions' ? 'admin-tab-active' : ''}`}
                        onClick={() => setTab('sessions')}
                    >
                        {t('sessionsTab')}
                    </button>
                    <button
                        className={`admin-tab ${tab === 'learning' ? 'admin-tab-active' : ''}`}
                        onClick={() => setTab('learning')}
                    >
                        {t('learningTab')}
                        {candidates.length > 0 && <span className="admin-tab-badge">{candidates.length}</span>}
                    </button>
                </div>

                <div className="chat-messages admin-content">
                    {error && <div className="error-text">{error}</div>}

                    {tab === 'sessions' && (
                        loading ? (
                            <div className="loading-state">Loading insights...</div>
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
                        )
                    )}

                    {tab === 'learning' && (
                        <>
                            <h3 className="admin-section-title">{t('learningQueue')}</h3>
                            {candidates.length === 0 ? (
                                <div className="empty-state">{t('noCandidates')}</div>
                            ) : (
                                <div className="learning-list">
                                    {candidates.map((c) => (
                                        <div key={c.id} className="learning-card">
                                            <div className="learning-card-head">
                                                <span className="learning-category">{c.category}</span>
                                                <span className="learning-confidence">{t('confidence')}: {confidenceLabel(c.confidence)}</span>
                                            </div>
                                            <div className="learning-fact">{c.fact}</div>
                                            <details className="learning-source">
                                                <summary>{t('sourceQuestion')} / {t('sourceAnswer')}</summary>
                                                <p><strong>{t('sourceQuestion')}:</strong> {c.question}</p>
                                                <p><strong>{t('sourceAnswer')}:</strong> {c.answer}</p>
                                            </details>
                                            <div className="learning-actions">
                                                <button className="learning-btn approve" onClick={() => handleApprove(c.id)}>
                                                    <Check size={16} /> {t('approve')}
                                                </button>
                                                <button className="learning-btn reject" onClick={() => handleReject(c.id)}>
                                                    <X size={16} /> {t('reject')}
                                                </button>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            )}

                            <h3 className="admin-section-title">{t('learnedFacts')}</h3>
                            {learned.length === 0 ? (
                                <div className="empty-state">{t('noLearnedFacts')}</div>
                            ) : (
                                <div className="learning-list">
                                    {learned.map((f) => (
                                        <div key={f.id} className="learning-card learned">
                                            <div className="learning-card-head">
                                                <span className="learning-category">{f.category}</span>
                                                <button className="learning-btn prune" onClick={() => handlePrune(f.id)} title={t('prune')}>
                                                    <Trash2 size={16} />
                                                </button>
                                            </div>
                                            <div className="learning-fact">{f.factText}</div>
                                        </div>
                                    ))}
                                </div>
                            )}
                        </>
                    )}
                </div>
            </div>
        </div>
    );
};

export default Admin;
