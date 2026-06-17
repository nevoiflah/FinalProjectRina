import { createContext, useContext, useState, useEffect } from 'react';

const LanguageContext = createContext();

const translations = {
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
        showPassword: 'הצג סיסמה',
        hidePassword: 'הסתר סיסמה',
        landingHero: 'הייעוץ האקדמי שלך, תמיד זמין',
        landingSubtitle: 'שאל על תוכניות לימוד, מלגות ולוחות זמנים – בעברית או ערבית',
        loginBtn: 'כניסה',
        registerBtn: 'הרשמה',
        feature1Title: 'שאל בקול',
        feature1Desc: 'הקלט את שאלתך ישירות',
        feature2Title: 'תשובות חכמות',
        feature2Desc: 'מענה מיידי מבוסס בינה מלאכותית',
        feature3Title: 'עברית וערבית',
        feature3Desc: 'שירות מלא בשתי השפות',
        helpful: 'תשובה מועילה',
        notHelpful: 'תשובה לא מועילה',
        feedbackThanks: 'תודה!',
        sessionsTab: 'שיחות',
        learningTab: 'תור למידה',
        learningQueue: 'עובדות שהמערכת רוצה ללמוד',
        learnedFacts: 'עובדות שנלמדו',
        confidence: 'ביטחון',
        category: 'קטגוריה',
        fact: 'עובדה',
        approve: 'אשר',
        reject: 'דחה',
        prune: 'הסר',
        noCandidates: 'אין עובדות הממתינות לאישור.',
        noLearnedFacts: 'עדיין לא נלמדו עובדות.',
        sourceQuestion: 'שאלה',
        sourceAnswer: 'תשובה',
        styleLabel: 'סגנון',
        styleFriendly: 'ידידותי',
        styleFormal: 'רשמי',
        audienceLabel: 'קהל',
        audienceStandard: 'רגיל',
        audienceSimple: 'נגיש',
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
        showPassword: 'إظهار كلمة المرور',
        hidePassword: 'إخفاء كلمة المرور',
        landingHero: 'مستشارك الأكاديمي، متاح دائماً',
        landingSubtitle: 'اسأل عن البرامج الدراسية والمنح والجداول – بالعربية أو العبرية',
        loginBtn: 'دخول',
        registerBtn: 'إنشاء حساب',
        feature1Title: 'تحدث بصوتك',
        feature1Desc: 'سجّل سؤالك مباشرة',
        feature2Title: 'إجابات ذكية',
        feature2Desc: 'ردود فورية بالذكاء الاصطناعي',
        feature3Title: 'عربي وعبري',
        feature3Desc: 'خدمة متكاملة بكلتا اللغتين',
        helpful: 'إجابة مفيدة',
        notHelpful: 'إجابة غير مفيدة',
        feedbackThanks: 'شكراً!',
        sessionsTab: 'المحادثات',
        learningTab: 'قائمة التعلّم',
        learningQueue: 'حقائق يريد النظام تعلّمها',
        learnedFacts: 'حقائق تم تعلّمها',
        confidence: 'الثقة',
        category: 'الفئة',
        fact: 'الحقيقة',
        approve: 'موافقة',
        reject: 'رفض',
        prune: 'إزالة',
        noCandidates: 'لا توجد حقائق بانتظار الموافقة.',
        noLearnedFacts: 'لم يتم تعلّم أي حقائق بعد.',
        sourceQuestion: 'سؤال',
        sourceAnswer: 'إجابة',
        styleLabel: 'الأسلوب',
        styleFriendly: 'ودّي',
        styleFormal: 'رسمي',
        audienceLabel: 'الجمهور',
        audienceStandard: 'عادي',
        audienceSimple: 'ميسّر',
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
