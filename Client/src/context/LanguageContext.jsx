import { createContext, useContext, useState, useEffect } from 'react';

const LanguageContext = createContext();

export const translations = {
    he: {
        appTitle: 'היועץ האקדמי של רופין',
        appSubtitle: 'התחבר לחשבון הסטודנט שלך',
        loginTitle: 'כניסה למערכת',
        emailPlaceholder: 'כתובת דוא"ל',
        passwordPlaceholder: 'סיסמה',
        signIn: 'התחבר',
        registerPrompt: 'אין לך חשבון? צור אחד עכשיו',
        registerTitle: 'הרשמה לייעוץ',
        namePlaceholder: 'שם מלא',
        createAccount: 'צור חשבון',
        loginPrompt: 'יש לך כבר חשבון? התחבר',
        chatPlaceholder: 'הקלד בעברית או ערבית...',
        listening: 'מקשיב...',
        thinking: 'חושב...',
        logout: 'התנתק',
        adminDashboard: 'לוח בקרה למנהלים',
        transcribing: 'ממיר דיבור לטקסט...',
        voiceOn: 'קול מופעל',
        voiceOff: 'קול כבוי',
        startRecording: 'הקלט הודעה',
        stopRecording: 'עצור הקלטה',
        sendMessage: 'שלח הודעה',
        backToChat: 'חזור לצ\'אט',
        user: 'שם משתמש',
        firstTopic: 'נושא ראשון',
        finalAnalysis: 'ניתוח סופי',
        date: 'תאריך',
        noHistory: 'לא נמצאה היסטוריית שיחות.',
        inputError: 'נא להשתמש בעברית או ערבית בלבד.',
        aiPrompt: 'ענה בבקשה בעברית בלבד.',
    },
    ar: {
        appTitle: 'مساعد روبين الأكاديمي',
        appSubtitle: 'تسجيل الدخول إلى حساب الطالب الخاص بك',
        loginTitle: 'تسجيل الدخول',
        emailPlaceholder: 'البريد الإلكتروني',
        passwordPlaceholder: 'كلمة المرور',
        signIn: 'دخول',
        registerPrompt: 'ليس لديك حساب؟ إنشاء واحد الآن',
        registerTitle: 'التسجيل في نظام روبين',
        namePlaceholder: 'الاسم الكامل',
        createAccount: 'إنشاء حساب',
        loginPrompt: 'هل لديك حساب؟ تسجيل الدخول',
        chatPlaceholder: 'اكتب بالعربية أو العبرية...',
        listening: 'أستمع...',
        thinking: 'أفكر...',
        logout: 'تسجيل خروج',
        adminDashboard: 'لوحة تحكم المسؤول',
        transcribing: 'جارٍ تحويل الكلام إلى نص...',
        voiceOn: 'الصوت مفعّل',
        voiceOff: 'الصوت معطّل',
        startRecording: 'تسجيل رسالة',
        stopRecording: 'إيقاف التسجيل',
        sendMessage: 'إرسال رسالة',
        backToChat: 'العودة للدردشة',
        user: 'المستخدم',
        firstTopic: 'الموضوع الأول',
        finalAnalysis: 'التحليل النهائي',
        date: 'تاريخ',
        noHistory: 'لم يتم العثور على سجل محادثات.',
        inputError: 'الرجاء استخدام العربية أو العبرية فقط.',
        aiPrompt: 'أجب باللغة العربية فقط.',
    }
};

export const LanguageProvider = ({ children }) => {
    const [language, setLanguage] = useState(() => {
        return localStorage.getItem('appLang') || 'he';
    });

    useEffect(() => {
        localStorage.setItem('appLang', language);
        // document.documentElement.dir = 'rtl'; // Both are RTL
    }, [language]);

    const toggleLanguage = () => {
        setLanguage(prev => (prev === 'he' ? 'ar' : 'he'));
    };

    const t = (key) => {
        return translations[language][key] || key;
    };

    return (
        <LanguageContext.Provider value={{ language, toggleLanguage, t }}>
            {children}
        </LanguageContext.Provider>
    );
};

export const useLanguage = () => useContext(LanguageContext);
