import React, { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { Mic, Send, LogOut, LayoutDashboard, Globe } from 'lucide-react';
import { sendChatMessage, getSttTranscript, getTtsAudio, endSession } from '../api';
import { useLanguage } from '../context/LanguageContext';

const Chat = () => {
    const [messages, setMessages] = useState([]);
    const [inputVal, setInputVal] = useState('');
    const [loading, setLoading] = useState(false);
    const [inputError, setInputError] = useState('');
    const [recording, setRecording] = useState(false);
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
            // Secretly append the language instruction to force the AI's response language.
            const textWithLangPrompt = `${text}\n\n[SYSTEM INSTRUCTION: ${t('aiPrompt')}]`;
            const response = await sendChatMessage(textWithLangPrompt, userId);
            setMessages(prev => [...prev, { sender: 'bot', text: response.reply }]);
        } catch (err) {
            setMessages(prev => [...prev, { sender: 'bot', text: 'Error communicating with server.' }]);
        } finally {
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
                    try {
                        const transcript = await getSttTranscript(audioBlob);
                        setInputVal(transcript);
                    } catch (error) {
                        console.error(error);
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
                    <div style={{ marginRight: '80px' }}> {/* Space for the absolute toggle button */}
                        <h2>{user?.name || t('user')}</h2>
                        <div style={{ fontSize: '13px', opacity: 0.8 }}>{t('appTitle')}</div>
                    </div>
                    <div style={{ display: 'flex', gap: '12px' }}>
                        {isAdmin && (
                            <button className="icon-btn" onClick={() => navigate('/admin')} title={t('adminDashboard')} style={{ background: 'rgba(255,255,255,0.2)' }}>
                                <LayoutDashboard size={20} />
                            </button>
                        )}
                        <button className="icon-btn" onClick={handleLogout} title={t('logout')} style={{ background: 'rgba(255,255,255,0.2)' }}>
                            <LogOut size={20} />
                        </button>
                    </div>
                </header>

                <div className="chat-messages">
                    {messages.map((m, idx) => (
                        <div key={idx} className={`message ${m.sender}`}>
                            <div className="message-content" dangerouslySetInnerHTML={{ __html: m.text }} />
                        </div>
                    ))}
                    {loading && (
                        <div className="message bot">
                            <div className="message-content">{t('thinking')}</div>
                        </div>
                    )}
                    <div ref={messagesEndRef} />
                </div>

                <div className="chat-input-area" style={{ flexDirection: 'column', alignItems: 'stretch' }}>
                    {inputError && <div className="error-text" style={{ alignSelf: 'center' }}>{inputError}</div>}

                    <div style={{ display: 'flex', gap: '12px', width: '100%' }}>
                        <button
                            className="icon-btn"
                            onClick={toggleRecording}
                            style={{ background: recording ? '#e53e3e' : 'var(--primary-blue)' }}
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
                            className="chat-input"
                            placeholder={recording ? t('listening') : t('chatPlaceholder')}
                            value={inputVal}
                            onChange={(e) => {
                                setInputVal(e.target.value);
                                setInputError('');
                            }}
                            onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                        />
                        <button className="icon-btn" onClick={handleSend} disabled={loading}>
                            <Send size={20} />
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Chat;
