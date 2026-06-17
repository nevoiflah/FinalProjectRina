import { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Mic, Send, LogOut, LayoutDashboard, Globe, Volume2, VolumeX, AlertCircle, ThumbsUp, ThumbsDown } from 'lucide-react';
import { sendChatMessage, getSttTranscript, getTtsAudio, endSession, submitFeedback } from '../api';
import { useLanguage } from '../context/LanguageContext';

// Conversation style -> TTS voice + a tone directive for the system prompt.
const STYLE_VOICE = { friendly: 'nova', formal: 'onyx' };
const STYLE_PERSONA = {
    friendly: 'Speak in a warm, friendly and encouraging tone.',
    formal: 'Speak in a formal, professional and concise tone.',
};
// Target audience -> speech speed + a simplification directive (children / elderly / accessibility).
const AUDIENCE_SPEED = { standard: 1.0, simple: 0.9 };
const AUDIENCE_PERSONA = {
    standard: '',
    simple: 'The listener may be a child, an elderly person, or someone who needs accessibility: use very simple words and short sentences, avoid jargon, and briefly explain any term you use.',
};

const Chat = () => {
    const [messages, setMessages] = useState([]);
    const [inputVal, setInputVal] = useState('');
    const [loading, setLoading] = useState(false);
    const [autoPlayVoice, setAutoPlayVoice] = useState(true);
    const [inputError, setInputError] = useState('');
    const [recording, setRecording] = useState(false);
    const [transcribing, setTranscribing] = useState(false);
    const [feedback, setFeedback] = useState({}); // message index -> 'up' | 'down'
    const [style, setStyle] = useState('friendly');     // conversation style
    const [audience, setAudience] = useState('standard'); // target-audience mode
    const messagesEndRef = useRef(null);
    const mediaRecorderRef = useRef(null);
    const audioChunksRef = useRef([]);
    const audioContextRef = useRef(null);
    const analyserRef = useRef(null);
    const sourceRef = useRef(null);
    const animationFrameRef = useRef(null);
    const bar1Ref = useRef(null);
    const bar2Ref = useRef(null);
    const bar3Ref = useRef(null);
    const bar4Ref = useRef(null);
    const navigate = useNavigate();
    const { t, language, toggleLanguage } = useLanguage();

    const userStr = localStorage.getItem('chatUser');
    const user = userStr ? JSON.parse(userStr) : null;
    const userId = user?.userId || user?.UserId;
    const isAdmin = user?.isAdmin || user?.IsAdmin;

    useEffect(() => {
        // Initial greeting
        const greeting = language === 'ar'
            ? `مرحبًا بك في مركز روبين الأكاديمي، ${user?.name?.split(' ')[0] || 'طالب'}! كيف يمكنني مساعدتك اليوم؟`
            : `ברוך הבא למרכז האקדמי רופין, ${user?.name?.split(' ')[0] || 'סטודנט'}! איך אוכל לעזור לך היום?`;

        setMessages([{ sender: 'bot', text: greeting }]);
    }, [language, user?.name]);

    useEffect(() => {
        messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages]);

    // A 👍 on a bot reply feeds the self-improving RAG loop; a 👎 is logged only.
    const handleFeedback = async (idx, rating) => {
        if (feedback[idx]) return; // one rating per message

        // Pair the reply with the user question that prompted it.
        let question = '';
        for (let i = idx - 1; i >= 0; i--) {
            if (messages[i].sender === 'user') { question = messages[i].text; break; }
        }
        if (!question) return; // e.g. the opening greeting — nothing to learn from

        setFeedback(prev => ({ ...prev, [idx]: rating }));
        try {
            await submitFeedback(userId, question, messages[idx].text, rating);
        } catch (err) {
            console.error('Feedback failed:', err);
        }
    };

    const validateInputLanguage = (text) => {
        const regex = /^[\u0590-\u05FF\u0600-\u06FF0-9\s.,?!'"(){}-]+$/;
        return regex.test(text.trim());
    };

    const handleSend = async () => {
        const text = inputVal.trim();
        if (!text) return;

        if (!validateInputLanguage(text)) {
            setInputError(t('inputError'));
            return;
        }

        setInputError('');
        setInputVal('');
        setMessages(prev => [...prev, { sender: 'user', text }]);
        setLoading(true);

        try {
            // Pass the prior conversation so the AI actually remembers it. `messages` here is the
            // conversation BEFORE this new turn (state updates are async), which is exactly the
            // history we want. The language directive goes as a separate field so the stored/visible
            // message text stays clean (no [SYSTEM INSTRUCTION] leaking into the transcript).
            const history = messages.map(m => ({
                role: m.sender === 'user' ? 'user' : 'assistant',
                content: m.text,
            }));
            // Compose the style + audience adaptation sent to the model and the voice engine.
            const persona = [STYLE_PERSONA[style], AUDIENCE_PERSONA[audience]].filter(Boolean).join(' ');
            const response = await sendChatMessage(text, userId, history, t('aiPrompt'), persona);

            try {
                if (autoPlayVoice) {
                    // Fetch audio while still showing "thinking" (loading is true)
                    const audioBlob = await getTtsAudio(response.reply, STYLE_VOICE[style], AUDIENCE_SPEED[audience]);
                    const audioUrl = URL.createObjectURL(audioBlob);
                    const audio = new Audio(audioUrl);

                    // Now show the text, stop loading, and play audio simultaneously
                    setMessages(prev => [...prev, { sender: 'bot', text: response.reply }]);
                    setLoading(false);

                    audio.onended = () => {
                        URL.revokeObjectURL(audioUrl);
                    };

                    await audio.play();
                } else {
                    // If autoPlayVoice is disabled, just show the text immediately
                    setMessages(prev => [...prev, { sender: 'bot', text: response.reply }]);
                    setLoading(false);
                }
            } catch (ttsErr) {
                console.error("Failed to play TTS audio:", ttsErr);
                // Fallback: show text even if audio fails
                setMessages(prev => [...prev, { sender: 'bot', text: response.reply }]);
                setLoading(false);
            }

        } catch {
            setMessages(prev => [...prev, { sender: 'bot', text: 'Error communicating with server.' }]);
            setLoading(false);
        }
    };

    const startRecording = async () => {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
            mediaRecorderRef.current = new MediaRecorder(stream);
            audioChunksRef.current = [];

            // Web Audio API setup for dynamic visualizer
            const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            const analyser = audioCtx.createAnalyser();
            const source = audioCtx.createMediaStreamSource(stream);
            source.connect(analyser);
            analyser.fftSize = 64;

            audioContextRef.current = audioCtx;
            analyserRef.current = analyser;
            sourceRef.current = source;

            const bufferLength = analyser.frequencyBinCount;
            const dataArray = new Uint8Array(bufferLength);

            const updateEqualizer = () => {
                if (!analyserRef.current) return;
                analyserRef.current.getByteFrequencyData(dataArray);

                // Map the frequency data into 4 visual bars (max height 24px)
                if (bar1Ref.current) bar1Ref.current.style.height = `${Math.max(4, (dataArray[2] / 255) * 24)}px`;
                if (bar2Ref.current) bar2Ref.current.style.height = `${Math.max(4, (dataArray[4] / 255) * 24)}px`;
                if (bar3Ref.current) bar3Ref.current.style.height = `${Math.max(4, (dataArray[6] / 255) * 24)}px`;
                if (bar4Ref.current) bar4Ref.current.style.height = `${Math.max(4, (dataArray[8] / 255) * 24)}px`;

                animationFrameRef.current = requestAnimationFrame(updateEqualizer);
            };

            updateEqualizer();

            mediaRecorderRef.current.ondataavailable = e => {
                if (e.data.size > 0) audioChunksRef.current.push(e.data);
            };

            mediaRecorderRef.current.onstop = async () => {
                const audioBlob = new Blob(audioChunksRef.current, { type: 'audio/webm' });
                if (audioBlob.size > 1000) {
                    setTranscribing(true);
                    try {
                        const transcript = await getSttTranscript(audioBlob, language);
                        setInputVal(transcript);
                    } catch (error) {
                        console.error(error);
                    } finally {
                        setTranscribing(false);
                    }
                }
            };

            mediaRecorderRef.current.start();
            setRecording(true);
        } catch (err) {
            console.error('Mic access denied', err);
        }
    };

    const stopRecording = () => {
        if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
            mediaRecorderRef.current.stop();
            mediaRecorderRef.current.stream.getTracks().forEach(track => track.stop());
        }

        if (animationFrameRef.current) {
            cancelAnimationFrame(animationFrameRef.current);
        }
        if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
            audioContextRef.current.close().catch(console.error);
        }

        setRecording(false);
    };

    const toggleRecording = () => {
        recording ? stopRecording() : startRecording();
    };

    const handleLogout = async () => {
        if (userId) await endSession(userId);
        localStorage.removeItem('chatUser');
        navigate('/login');
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
            <div className="chat-container">
                <header className="chat-header">
                    <div className="chat-title-block with-toggle-space">
                        <h2>{user?.name || t('user')}</h2>
                        <div className="chat-subtitle">{t('appTitle')}</div>
                    </div>
                    <div className="header-actions">
                        {isAdmin && (
                            <button className="icon-btn header-icon-btn" onClick={() => navigate('/admin')} data-tooltip={t('adminDashboard')} aria-label={t('adminDashboard')}>
                                <LayoutDashboard size={20} />
                            </button>
                        )}
                        <button className="icon-btn header-icon-btn" onClick={handleLogout} data-tooltip={t('logout')} aria-label={t('logout')}>
                            <LogOut size={20} />
                        </button>
                    </div>
                </header>

                <div className="chat-messages">
                    {messages.map((m, idx) => (
                        <div key={idx} className={`message ${m.sender}`}>
                            <div className="message-content">{m.text}</div>
                            {m.sender === 'bot' && idx > 0 && (
                                <div className="message-feedback">
                                    <button
                                        className={`feedback-btn ${feedback[idx] === 'up' ? 'feedback-active' : ''}`}
                                        onClick={() => handleFeedback(idx, 'up')}
                                        disabled={!!feedback[idx]}
                                        data-tooltip={t('helpful')}
                                        aria-label={t('helpful')}
                                    >
                                        <ThumbsUp size={14} />
                                    </button>
                                    <button
                                        className={`feedback-btn ${feedback[idx] === 'down' ? 'feedback-active' : ''}`}
                                        onClick={() => handleFeedback(idx, 'down')}
                                        disabled={!!feedback[idx]}
                                        data-tooltip={t('notHelpful')}
                                        aria-label={t('notHelpful')}
                                    >
                                        <ThumbsDown size={14} />
                                    </button>
                                    {feedback[idx] && <span className="feedback-thanks">{t('feedbackThanks')}</span>}
                                </div>
                            )}
                        </div>
                    ))}
                    {loading && (
                        <div className="message bot">
                            <div className="message-content">
                                <span className="typing-indicator" aria-label={t('thinking')}>
                                    <span></span>
                                    <span></span>
                                    <span></span>
                                </span>
                            </div>
                        </div>
                    )}
                    <div ref={messagesEndRef} />
                </div>

                <div className="chat-input-area">
                    {transcribing ? (
                        <div className="transcribing-indicator">
                            <span className="transcribing-spinner" />
                            {t('transcribing')}
                        </div>
                    ) : (
                        inputError && (
                            <div className="error-text input-error-centered" role="alert">
                                <AlertCircle size={14} aria-hidden="true" />
                                {inputError}
                            </div>
                        )
                    )}

                    <div className="chat-controls-row">
                        <label className="chat-control">
                            <span>{t('styleLabel')}</span>
                            <select value={style} onChange={(e) => setStyle(e.target.value)}>
                                <option value="friendly">{t('styleFriendly')}</option>
                                <option value="formal">{t('styleFormal')}</option>
                            </select>
                        </label>
                        <label className="chat-control">
                            <span>{t('audienceLabel')}</span>
                            <select value={audience} onChange={(e) => setAudience(e.target.value)}>
                                <option value="standard">{t('audienceStandard')}</option>
                                <option value="simple">{t('audienceSimple')}</option>
                            </select>
                        </label>
                    </div>

                    <div className="chat-input-row">
                        <button
                            className={`icon-btn ${autoPlayVoice ? '' : 'voice-toggle-off'}`}
                            onClick={() => setAutoPlayVoice(!autoPlayVoice)}
                            data-tooltip={autoPlayVoice ? t('voiceOn') : t('voiceOff')}
                            aria-label={autoPlayVoice ? t('voiceOn') : t('voiceOff')}
                            aria-pressed={autoPlayVoice}
                        >
                            {autoPlayVoice ? <Volume2 size={20} /> : <VolumeX size={20} />}
                        </button>
                        <button
                            className={`icon-btn ${recording ? 'recording-btn' : ''}`}
                            onClick={toggleRecording}
                            data-tooltip={recording ? t('stopRecording') : t('startRecording')}
                            aria-label={recording ? t('stopRecording') : t('startRecording')}
                            aria-pressed={recording}
                        >
                            {recording ? (
                                <div className="equalizer">
                                    <div className="eq-bar" ref={bar1Ref}></div>
                                    <div className="eq-bar" ref={bar2Ref}></div>
                                    <div className="eq-bar" ref={bar3Ref}></div>
                                    <div className="eq-bar" ref={bar4Ref}></div>
                                </div>
                            ) : (
                                <Mic size={20} />
                            )}
                        </button>
                        <input
                            type="text"
                            className={`chat-input${transcribing ? ' chat-input--transcribing' : ''}`}
                            placeholder={recording ? t('listening') : t('chatPlaceholder')}
                            value={inputVal}
                            disabled={transcribing}
                            onChange={(e) => {
                                setInputVal(e.target.value);
                                setInputError('');
                            }}
                            onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                        />
                        <button className="icon-btn" onClick={handleSend} disabled={loading || transcribing} data-tooltip={t('sendMessage')} aria-label={t('sendMessage')}>
                            <Send size={20} />
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Chat;
