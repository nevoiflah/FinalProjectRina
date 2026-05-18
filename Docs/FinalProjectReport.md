# TTS and STT Wrapper for virtual and physical robots enhanced by GenAI

## מעטפת טקסט-לדיבור ודיבור-לטקסט לרובוטים וירטואליים ופיזיים משופרים באמצעות בינה מלאכותית גנרטיבית

```
פרויקט גמר בפיתוח מערכות תוכנה
המכללה האקדמית רופין
```
```
מנחה אקדמית: ד״ר רינה צביאל - גירשין
```
```
פרויקט מספר: 9
שנת לימודים: תשפ״ו (2025 - 2026)
```

---

## תוכן העניינים

- [תקציר מנהלים](#תקציר-מנהלים)
- [הצגת הבעיה והצורך](#הצגת-הבעיה-והצורך)
- [רקע ומצב קיים](#רקע-ומצב-קיים)
- [תיאור הפתרון המוצע](#תיאור-הפתרון-המוצע)
- [דרישות המערכת](#דרישות-המערכת)
- [ארכיטקטורה ותכנון](#ארכיטקטורה-ותכנון)
- [מימוש והטמעה](#מימוש-והטמעה)
- [תוצאות והערכה](#תוצאות-והערכה)
- [מסקנות והמלצות](#מסקנות-והמלצות)
- [רשימה ביבליוגרפית](#רשימה-ביבליוגרפית)
- [נספח א׳: מבנה קבצי הפרויקט](#נספח-א-מבנה-קבצי-הפרויקט)
- [נספח ב׳: הוראות התקנה](#נספח-ב-הוראות-התקנה)
- [נספח ג׳: ממשקי API](#נספח-ג-ממשקי-api)
- [נספח ד׳: אינטגרציה עם רובוטים פיזיים](#נספח-ד-אינטגרציה-עם-רובוטים-פיזיים)

---

## תקציר מנהלים

פרויקט זה מציג פיתוח של מערכת Wrapper (מעטפת) חדשנית לשילוב פונקציות טקסט-לדיבור (TTS) ודיבור-לטקסט (STT) ברובוטים וירטואליים ופיזיים, המונעים על ידי בינה מלאכותית גנרטיבית. המערכת נועדה ליצור ממשק גמיש ונגיש המאפשר תקשורת טבעית ואינטואיטיבית בין בני אדם לרובוטים.

הפרויקט כולל פיתוח מערכת תוכנה מבוססת ענן המשלבת טכנולוגיות מתקדמות:

1. **Backend היברידי**: שרת C# ASP.NET Core 8.0 לניהול משתמשים ולוגיקה עסקית, בשילוב שירות Python Flask ייעודי המנהל את האינטראקציה עם מודלי השפה (LLM).
2. **RAG (Retrieval-Augmented Generation)**: מנגנון "זיכרון" ארגוני הלומד משיחות עבר מוצלחות ומנחה את ה-AI בזמן אמת, ללא צורך באימון יקר (Fine-Tuning).
3. **מודל Embedding מקומי (PyTorch)**: שימוש בספריית `sentence-transformers` מבוססת PyTorch לחישוב וקטורי הטמעה 384-ממדיים מקומית — ללא קריאת API לחישוב embeddings.
4. **ממשק ניהול (Admin Dashboard)**: כלי בקרה למנהלי המערכת המאפשר מעקב אחר משתמשים, ניתוח שיחות (שאלה ראשונה מול תוצאה סופית) ושיפור מתמיד של המענה.
5. **OpenAI Whisper & TTS**: לזיהוי והפקת דיבור ברמה אנושית, עם תמיכה מלאה בעברית ובערבית.

ה-Wrapper פועל כשכבת הפשטה כללית שאינה תלויה בדומיין — ניתן להחליף את הפרסונה (System Prompt) ולהפוך את אותה מערכת ליועץ רפואי, שירות לקוחות, או מדריך מוזיאון ללא שינוי קוד. כהוכחת ישימות (PoC), יושם "Ruppin Academic Advisor" — יועץ קבלה אקדמי למרכז האקדמי רופין.

המערכת פרוסה בייצור על Azure ונגישה בכתובת: `https://red-meadow-01cbfc50f.7.azurestaticapps.net`

---

## הצגת הבעיה והצורך

### הגדרת הבעיה

בעידן המודרני, רובוטים וסוכנים וירטואליים הופכים לחלק בלתי נפרד מחיי היומיום. עם זאת, האינטראקציה בין בני אדם לרובוטים מוגבלת בדרך כלל לממשקים גרפיים מסורתיים או לשפות תכנות מורכבות. הצורך באינטראקציה טבעית באמצעות שפה מדוברת הולך וגובר.

הבעיות המרכזיות שהפרויקט מטפל בהן:

1. **חוסר ב"זיכרון" ארגוני**: מודלי שפה רגילים (כמו GPT-3.5) אינם מכירים את היסטוריית הארגון או מקרים דומים מהעבר, מה שמוביל לתשובות גנריות שאינן מותאמות להקשר הספציפי.
2. **מורכבות אינטגרציה**: שילוב STT, TTS ומודל שפה (LLM) דורש סנכרון מורכב בין שירותים נפרדים, עם latency גבוה ועלויות תחזוקה כבדות.
3. **העדר כלי בקרה**: למנהלי מערכת אין בדרך כלל גישה נוחה לנתוני השיחות ("על מה המשתמשים שואלים ומה הם מקבלים בסוף?").
4. **פיצול בין ספקים**: שימוש בספקים שונים עבור STT, TTS ו-LLM יוצר מורכבות של אותנטיקציה, חיוב ותחזוקה.

### הצורך העסקי והחברתי

- **חינוך וייעוץ אקדמי**: יועצים וירטואליים המסוגלים להכווין סטודנטים למסלול הלימודים המתאים להם על סמך נתוני סף קבלה וציונים (כפי שמיושם בפרויקט זה כ-"Ruppin Academic Advisor").
- **שירות לקוחות חכם**: סוכנים וירטואליים שלומדים מפתרונות עבר (RAG) ומספקים שירות מדויק 24/7.
- **נגישות**: מתן גישה לטכנולוגיה לאנשים עם מוגבלויות באמצעות שיחה קולית טבעית.
- **רובוטיקה חינוכית ורפואית**: שילוב ממשק קולי-AI ברובוטים פיזיים כגון NAO ו-Pepper בסביבות חינוכיות ובתי חולים.

---

## רקע ומצב קיים

### סקירת טכנולוגיות קיימות

#### Speech-to-Text (STT)
טכנולוגיות ה-STT המובילות בשוק כוללות:
- **OpenAI Whisper**: מודל open-source ומסחרי, אומן על 680,000 שעות דיבור ב-99 שפות. מצטיין בעברית ובערבית.
- **Google Cloud Speech-to-Text**: שירות ענן בתשלום, תמיכה בעברית חלקית, דורש הגדרת locale מפורשת.
- **IBM Watson Speech-to-Text**: מתמחה בסביבות ארגוניות, תמיכה מוגבלת בשפות שמיות.

#### Text-to-Speech (TTS)
- **OpenAI TTS**: מודל נוירוני עם 6 קולות, איכות אנושית, תמיכה מלאה בעברית/ערבית.
- **Google Cloud TTS**: WaveNet ו-Neural2, איכות גבוהה אך עלות גבוהה לכמות גדולה.
- **IBM Watson TTS**: מוגבל יחסית בשפות שמיות.

#### Large Language Models (LLM)
- **GPT-3.5-turbo (OpenAI)**: מאזן אידיאלי בין עלות לביצועים, תגובה מהירה, תמיכה מלאה בעברית/ערבית.
- **GPT-4**: איכות גבוהה יותר אך עלות פי 10, לא נדרש לייעוץ אקדמי.

#### Embedding Models
- **OpenAI text-embedding-3-small**: 1536 מימדים, עלות לפי שימוש ($0.02/מיליון טוקן).
- **paraphrase-multilingual-MiniLM-L12-v2 (sentence-transformers)**: 384 מימדים, פועל מקומית בחינם, מבוסס PyTorch, תמיכה מלאה בעברית/ערבית.

### פתרונות קיימים ומגבלותיהם

פתרונות ה-chatbot וה-voice-AI הקיימים בשוק סובלים ממספר מגבלות:

| פתרון | מגבלה עיקרית |
|---|---|
| Dialogflow (Google) | ממשק נוקשה, ללא RAG מובנה, תלות ב-Google Cloud |
| Amazon Lex | מורכבות הגדרה גבוהה, עלות גבוהה בנפח קטן |
| Microsoft Bot Framework | כבד לפריסה, דורש ידע Azure מעמיק |
| ChatGPT API ישיר | ללא זיכרון ארגוני, ללא ממשק ניהול, ללא TTS/STT מובנה |

כולם משותפים בכך שאינם מספקים **פתרון E2E (End-to-End)** הכולל STT + LLM + TTS + זיכרון ארגוני + ממשק ניהול — בחבילה אחת ניתנת להתאמה.

### הצורך בפתרון חדש

הפרויקט נועד למלא פער זה על ידי יצירת מערכת Wrapper המאחדת את טכנולוגיות הדיבור עם יכולות בינה מלאכותית מתקדמות (RAG), ומספקת פתרון מקצה-לקצה הכולל ממשק ניהול, ניתוח נתונים, ותמיכה רב-לשונית.

---

## תיאור הפתרון המוצע

### סקירה כללית

הפתרון שפיתחנו הוא מערכת מקיפה המשלבת:

I. **ממשק API אחיד**: שכבת הפשטה ל-TTS, STT ו-Chat — כל לקוח (דפדפן, רובוט, אפליקציה) מתקשר דרך אותן 3 קריאות REST.
II. **מנגנון RAG**: המערכת מחפשת באופן אקטיבי מקרים דומים מהעבר במסד הנתונים ומזריקה את הפתרונות המוצלחים לתוך השיחה הנוכחית.
III. **מערכת ניהול חכמה (Admin Dashboard)**: לוח בקרה המציג את כל המשתמשים ואת "סיפור" השיחה שלהם (Initial Question → Final Result).
IV. **ארכיטקטורת Microservices**: הפרדה בין השרת הראשי (C#) לבין שירות ה-AI (Python) לגמישות וביצועים.
V. **מצב שיחה (Conversation Mode)**: דיאלוג קולי רציף עם visualizer אקוולייזר חי.

### ה-Wrapper כשכבת הפשטה כללית — ויישום פיילוט: Ruppin Academic Advisor

מטרת הפרויקט, כפי שמוגדרת במפרט, היא בניית Wrapper — שכבה כללית ועצמאית מהדומיין — שתוכל לשמש כבסיס לכל יישום המשלב TTS, STT ו-GenAI, בין אם ברובוט וירטואלי ובין אם בפיזי.

**כיצד מבוצעת ההכללה בפועל?**

שירות ה-AI (Python Flask) מקבל בכל בקשה שלושה שדות קונפיגורביליים:

| שדה | תפקיד |
|---|---|
| `message` | הודעת המשתמש |
| `system_prompt` | אישיות הסוכן (ניתן להחליף בלי לגעת בקוד) |
| `model` | מודל ה-GPT לשימוש (ברירת מחדל: `gpt-3.5-turbo`) |

**החלפת `system_prompt` הופכת את אותה מערכת ליועץ רפואי, שירות לקוחות, מדריך מוזיאון, או כל פרסונה אחרת** — ללא שינוי שורת קוד אחת בתשתית. שרת ה-C# מנתב STT, TTS ו-Chat דרך אותם ממשקי API ללא קשר לתוכן הדומיין.

**Ruppin Academic Advisor הוא ה-Proof of Concept (PoC) הרשמי של ה-Wrapper**, שנבחר מטעמי רלוונטיות (מוסד אקדמי, קהל יעד ידוע, קריטריוני הצלחה מדידים — ציוני קבלה, מסלולי לימוד).

### רכיבי המערכת

#### 1. צד שרת — Backend היברידי (C# + Python)

**ASP.NET Core 8.0 — שרת מרכזי**:
- `UserController` / `UserService`: הרשמה, התחברות, ניהול הרשאות (`isAdmin`), קידום מנהלים אוטומטי לפי כתובת מייל.
- `AdminController`: מחזיר לממשק הניהול את כל המשתמשים עם נתוני שיחה (Initial Question + Final Result).
- `ChatController`: מאמת משתמש, קורא ל-`ChatService` לקבלת תשובה, מנהל `session`.
- `SttController`: מקבל קובץ אודיו (WebM/WAV), מעביר ל-OpenAI Whisper, מחזיר תמליל.
- `TtsController`: מקבל טקסט, מעביר ל-OpenAI TTS, מחזיר MP3 bytes.
- `ChatService` (BL): מממש את לוגיקת ה-RAG — שולח שאלה ל-`/embed` בשירות Python, מחשב Cosine Similarity מול בסיס הידע, מזריק הקשר רלוונטי לתשובת ה-AI.
- `KnowledgeSeeder`: בזמן עלייה, מזרע את בסיס הידע האקדמי ב-MongoDB עם embeddings מקומיים (sentence-transformers). מזהה אוטומטית אי-התאמת מימד ומבצע re-seed במידת הצורך.
- `KnowledgeCache`: שומר את בסיס הידע בזיכרון (in-memory) למניעת שאילתות חוזרות למסד הנתונים בכל בקשה.

**Python Flask Microservice — שירות AI**:
- `POST /chat`: מקבל הודעה + הקשר (Context) מה-C#, בונה System Prompt דינמי, מחזיר תשובת GPT.
- `POST /embed`: מקבל מערך טקסטים, מחשב embeddings מקומית דרך `sentence-transformers` (PyTorch), מחזיר מערך וקטורים 384-ממדיים.
- `GET /health`: בדיקת תקינות השירות.

#### 2. צד לקוח — Frontend (React/Vite)

- **`Chat.jsx`**: ממשק הצ׳אט הקולי והטקסטואלי. כולל: הקלטת מיקרופון עם visualizer אקוולייזר חי (Web Audio API), כפתור הפעלה/כיבוי קול, תמיכה ב-TTS אוטומטי לכל תשובת AI, מצב "מאזין" ו"ממיר דיבור".
- **`Admin.jsx`**: דף ניהול עם טבלת משתמשים, שאלה ראשונה ותוצאה סופית לכל משתמש.
- **`LanguageContext.jsx`**: Provider לניהול שפה (עברית/ערבית) עם שמירת העדפה ב-localStorage.
- **`Login.jsx` / `Register.jsx`**: דפי כניסה ורישום עם תמיכה מלאה ב-RTL.

#### 3. מסד נתונים — MongoDB Atlas

| אוסף | תיאור |
|---|---|
| `users` | פרטי משתמשים: שם, מייל, `isAdmin`, תאריך הצטרפות |
| `chatSessions` | שיחות: `userId`, `initialQuestion`, `finalResult`, `startedAt`, `endedAt` |
| `ruppinKnowledge` | בסיס ידע אקדמי: `category`, `factText`, `embedding` (384-dim, PyTorch) |

---

## דרישות המערכת

### דרישות פונקציונליות

**ניהול משתמשים**:
- הרשמה והתחברות עם אימות.
- זיהוי אוטומטי של מנהלים לפי כתובת מייל מוגדרת.
- תמיכה בשדה `isAdmin` לשליטה בגישה ל-Dashboard.

**ממשק צ׳אט**:
- שליחת הודעות טקסט עם אימות שפה (עברית/ערבית בלבד).
- הקלטת קול (STT) עם אקוולייזר חי ומעבר אוטומטי לטקסט.
- הפקת תשובה קולית (TTS) עם אפשרות כיבוי.
- תמיכה ב-RTL מלא (עברית + ערבית).

**מערכת לומדת (RAG)**:
- איסוף אוטומטי של `initialQuestion` ו-`finalResult` בכל שיחה.
- חיפוש סמנטי בבסיס הידע (Cosine Similarity על embeddings PyTorch).
- הזרקת הקשר רלוונטי לכל בקשת AI.

**ממשק ניהול (Admin)**:
- צפייה בכל המשתמשים והסטוריית שיחות.
- הצגת שאלה ראשונה ותוצאה סופית לכל משתמש.

### דרישות לא פונקציונליות

| קטגוריה | דרישה |
|---|---|
| **ביצועים** | זמן תגובה לצ׳אט < 5 שניות (כולל STT + AI + TTS) |
| **זמינות** | שירות ייצור פעיל 24/7 על Azure (SLA של App Service B1: 99.95%) |
| **אבטחה** | CORS מוגבל, ניהול API Keys דרך Azure App Settings (לא בקוד) |
| **סקלביליות** | ארכיטקטורת Microservices מאפשרת scaling עצמאי לכל שירות |
| **רב-לשוניות** | תמיכה מלאה בעברית ובערבית ב-UI, STT, TTS ו-AI |
| **נגישות** | ממשק RTL, tooltips לכל כפתור, תמיכה בקלט קולי |
| **תחזוקתיות** | קוד מופרד לשכבות (DAL/BL/Controllers), logging מסודר ב-Python |

---

## ארכיטקטורה ותכנון

### ארכיטקטורת המערכת

```
[ דפדפן / רובוט — לקוח כלשהו ]
  |--> Login / Register
  |--> Chat Interface (קול + טקסט)
  |--> Admin Dashboard
        |
      HTTPS / REST
        |
[ שרת ראשי — C# ASP.NET Core 8.0 ]
  |-- UserController
  |-- AdminController
  |-- ChatController
  |-- SttController  →  OpenAI Whisper
  |-- TtsController  →  OpenAI TTS
  |-- BL: ChatService (RAG Logic)
  |-- BL: KnowledgeSeeder / KnowledgeCache
        |
        |---[ MongoDB Atlas ]
        |     |-- users
        |     |-- chatSessions
        |     |-- ruppinKnowledge (embeddings 384-dim)
        |
      HTTP (Internal)
        |
[ שירות AI — Python Flask ]
  |-- POST /chat    (GPT-3.5-turbo + Context Injection)
  |-- POST /embed   (sentence-transformers / PyTorch)
  |-- GET  /health
        |
      HTTPS
        |
[ OpenAI API ]
  |-- GPT-3.5-turbo  (Chat)
  |-- Whisper        (STT)
  |-- TTS            (Text-to-Speech)
```

### עקרונות תכנון

1. **הפרדת אחריויות (Separation of Concerns)**: DAL לגישה לנתונים, BL ללוגיקה עסקית, Controllers לממשק HTTP.
2. **Stateless AI Service**: שירות ה-Python אינו שומר מצב — כל ההקשר מועבר בכל בקשה מה-C#, מה שמאפשר scaling אופקי.
3. **In-Memory Caching**: `KnowledgeCache` שומר את בסיס הידע בזיכרון ומונע שאילתות DB חוזרות בכל בקשת משתמש.
4. **Fail-Fast**: שירות ה-Python מבצע בדיקת `OPENAI_API_KEY` בזמן עלייה ונכשל מיד עם הודעה ברורה.
5. **Auto Re-seed**: `KnowledgeSeeder` בודק את מימד ה-embedding בזמן עלייה ומבצע re-seed אוטומטי אם המימד אינו תואם (מנגנון מיגרציה אוטומטי).

### תכנון מסד הנתונים

**אוסף `chatSessions`**:

| שדה | סוג | תיאור |
|---|---|---|
| `_id` | ObjectId | מזהה ייחודי |
| `userId` | String | מזהה המשתמש |
| `initialQuestion` | String | השאלה הראשונה בשיחה |
| `finalResult` | String | התשובה הסופית שניתנה |
| `startedAt` | DateTime | זמן פתיחת השיחה |
| `endedAt` | DateTime? | זמן סיום השיחה (null אם פעילה) |

**אוסף `ruppinKnowledge`**:

| שדה | סוג | תיאור |
|---|---|---|
| `_id` | ObjectId | מזהה ייחודי |
| `category` | String | קטגוריה (Computer Science, Nursing, וכו׳) |
| `factText` | String | טקסט עובדת הידע בעברית |
| `embedding` | float[384] | וקטור PyTorch (sentence-transformers) |

---

## מימוש והטמעה

### טכנולוגיות וכלים

| שכבה | טכנולוגיה | גרסה |
|---|---|---|
| Frontend | React + Vite | React 18.3, Vite 5.4 |
| UI Components | Lucide React | עדכני |
| Backend | C# ASP.NET Core | .NET 8.0 |
| AI Microservice | Python Flask | 3.1 |
| LLM | OpenAI GPT-3.5-turbo | API v1 |
| STT | OpenAI Whisper | API v1 |
| TTS | OpenAI TTS | API v1 |
| Embedding (PyTorch) | sentence-transformers | paraphrase-multilingual-MiniLM-L12-v2 |
| Database | MongoDB Atlas | Cluster M0 (Free Tier) |
| Deployment | Microsoft Azure | App Service B1, Static Web Apps |
| IDE | Visual Studio Code | עדכני |

### בחירת ספקי TTS ו-STT: OpenAI במקום Google Cloud / IBM Watson

המפרט המקורי ציין את Google Cloud TTS/STT ו-IBM Watson כאפשרויות לממשקי דיבור. לאחר הערכה טכנית, התקבלה החלטה מושכלת לעבור ל-OpenAI Whisper (STT) ו-OpenAI TTS מהסיבות הבאות:

| קריטריון | Google Cloud / IBM Watson | OpenAI Whisper + TTS (נבחר) |
|---|---|---|
| **תמיכה בעברית וערבית** | חלקית, דורשת locale מפורש | מובנית ומדויקת בשתי השפות |
| **ספק יחיד** | שני ספקים נפרדים (auth, billing, SDK) | API אחד לכל שירותי ה-AI |
| **ביצועי STT לשפות שמיות** | דיוק בינוני לעברית/ערבית | State-of-the-art — אומן על 680K שעות רב-לשוניות |
| **עלות לפרויקט אקדמי** | דורש הפעלת שני projects, billing נפרד | מנוי אחד לכל השירותים |
| **latency** | שני round-trips לספקים שונים | round-trip אחד — latency מופחת |

**שיקול מפתח**: המערכת פועלת בעברית ובערבית — שתיהן שפות שמיות עם RTL וצלילים ייחודיים. Whisper של OpenAI אומן על נפח גדול מהותית של נתוני דיבור עברי וערבי בהשוואה ל-IBM Watson, ומדגים דיוק גבוה בהרבה. החלטה זו הביאה לאיכות מוצר גבוהה יותר ביחס לאלטרנטיבות שצוינו במפרט.

### שימוש ב-PyTorch דרך Sentence-Transformers לחישוב Embeddings מקומי

אחת מדרישות המפרט היא שימוש ב-TensorFlow או PyTorch לפיתוח מודלי AI. הפרויקט משתמש בספריית `sentence-transformers` — המבוססת על PyTorch — להפקת וקטורי הטמעה לצורך מנגנון ה-RAG.

**המודל בשימוש**: `paraphrase-multilingual-MiniLM-L12-v2`

| מאפיין | ערך |
|---|---|
| ספריית בסיס | PyTorch (דרך `sentence-transformers`) |
| שפות נתמכות | 50+ שפות, כולל עברית וערבית |
| מימד הוקטור | 384 |
| הפעלה | מקומית בשירות Python — ללא API חיצוני |
| עלות | חינמי (פועל על שרת ה-Python) |

**כיצד זה עובד במערכת**:

1. שירות ה-Python טוען את המודל בזמן עלייה (startup).
2. נחשף endpoint חדש: `POST /embed` — מקבל מערך טקסטים ומחזיר מערך וקטורים.
3. בעת Seeding של בסיס הידע, שרת ה-C# קורא ל-`/embed` עם כל עובדות הידע — המודל מחשב וקטורים ומאחסן ב-MongoDB.
4. בכל בקשת משתמש, שרת ה-C# שולח את שאלת המשתמש ל-`/embed`, ומשווה את הוקטור מול בסיס הידע באמצעות Cosine Similarity.

**תוצאה**: זיהוי סמנטי מלא ללא עלות API נוספת — כל embedding מחושב בחינם בשרת ה-Python.

### אתגרים וחידושים

1. **מימוש RAG ללא תשתית חיפוש כבדה**: האתגר היה לממש מנגנון זיכרון ללא הפעלת Vector DB נפרד (כמו Pinecone או Weaviate). הפתרון: MongoDB Atlas + embeddings PyTorch מקומיים + `KnowledgeCache` בזיכרון + Cosine Similarity בעלות תפעול נמוכה.
2. **מיגרציה אוטומטית של Embeddings**: מעבר מ-OpenAI embeddings (1536-dim) ל-sentence-transformers (384-dim) חייב שינוי כל הוקטורים במסד הנתונים. `KnowledgeSeeder` מזהה אוטומטית אי-התאמת מימד בזמן עלייה ומבצע re-seed — ללא התערבות ידנית.
3. **סנכרון בין שפות**: ניהול ממשק דו-לשוני (RTL) שמשפיע גם על ה-System Prompt של ה-AI — הנחיה דינמית לענות בשפת המשתמש מוזרקת בכל בקשה.
4. **ניהול סטייט ב-Python Stateless**: שירות ה-Python אינו שומר מצב — ה-C# מזריק את כל ההקשר (context) בכל בקשה, מה שמאפשר scaling ו-deployment עצמאי.
5. **אקוולייזר קולי חי**: שימוש ב-Web Audio API עם `AnalyserNode` ו-`requestAnimationFrame` ליצירת visualizer אמיתי המגיב לתדרי הקול — לא אנימציה מדומה.

### תהליך הפיתוח

הפיתוח עבר את השלבים הבאים:

1. **שלב 1 — תשתית בסיסית**: הקמת ASP.NET Core + MongoDB + React/Vite. מימוש Login/Register, ממשק צ׳אט טקסטואלי.
2. **שלב 2 — AI Integration**: חיבור Python Flask כ-Microservice. שילוב GPT-3.5-turbo עם System Prompt לייעוץ אקדמי.
3. **שלב 3 — Voice Features**: הוספת STT (Whisper) ו-TTS (OpenAI). מימוש אקוולייזר חי ב-Web Audio API.
4. **שלב 4 — RAG**: מימוש `KnowledgeSeeder` עם 60+ עובדות אקדמיות, `KnowledgeCache`, וחיפוש סמנטי.
5. **שלב 5 — Admin Dashboard**: פיתוח `AdminController` ו-`Admin.jsx`, מנגנון `InitialQuestion`/`FinalResult`.
6. **שלב 6 — PyTorch Embeddings**: החלפת OpenAI embeddings ב-sentence-transformers מקומי, מיגרציה אוטומטית.
7. **שלב 7 — פריסה לייצור**: הגדרת Azure App Services + Azure Static Web Apps, CI/CD ידני, בדיקות עומס.

---

## תוצאות והערכה

### תוצרי הפרויקט

| תוצר | תיאור |
|---|---|
| **Ruppin Academic Advisor** | יועץ קבלה AI עם תמיכה קולית מלאה, פרוס בייצור על Azure |
| **Admin Dashboard** | ממשק ניהול ויזואלי לניתוח שיחות משתמשים בזמן אמת |
| **RAG Engine** | מנגנון זיכרון ארגוני פעיל עם 60+ עובדות אקדמיות ו-embeddings מקומיים |
| **Python /embed API** | שירות embedding מקומי מבוסס PyTorch, נגיש לכל לקוח HTTP |
| **robot_client.py** | לקוח ייחוס לרובוטים פיזיים — מדגים מחזור STT→Chat→TTS מלא |
| **Azure Deployment** | פריסה מלאה: Static Web App + 2x App Service |

### הערכת ביצועים

| מדד | ערך מדוד |
|---|---|
| זמן תגובה ממוצע (Chat בלי TTS) | ~1.5–2.5 שניות |
| זמן תגובה ממוצע (Chat + TTS) | ~3–5 שניות |
| זמן STT (10 שניות דיבור) | ~1–2 שניות |
| זמן חישוב embedding מקומי (שאלה בודדת) | < 100ms |
| גודל מסד ידע | 60+ עובדות, 384-dim vectors |
| שפות נתמכות | עברית + ערבית (RTL מלא) |

### פריסה בסביבת ייצור

| שירות | כתובת |
|---|---|
| Frontend (React) | `https://red-meadow-01cbfc50f.7.azurestaticapps.net` |
| .NET API | `https://rina-api-05111937.azurewebsites.net` |
| Python AI Service | `https://rina-ai-05111937.azurewebsites.net` |
| MongoDB | Atlas Cluster M0, Region: US East |

בדיקת תקינות:
```bash
curl https://rina-api-05111937.azurewebsites.net/health
curl https://rina-ai-05111937.azurewebsites.net/health
```

---

## מסקנות והמלצות

### מסקנות

1. **ה-Wrapper הוכיח ישימות**: ה-PoC של Ruppin Academic Advisor מדגים בסביבת ייצור אמיתית שהארכיטקטורה תומכת בדומיינים שונים ובלקוחות שונים (דפדפן, רובוט).
2. **RAG ללא Vector DB**: הוכח שניתן לממש חיפוש סמנטי אפקטיבי עם MongoDB + sentence-transformers מקומי, ללא תשתית חיפוש ייעודית יקרה.
3. **PyTorch מקומי > OpenAI Embeddings בפרויקט מסוג זה**: שילוב `sentence-transformers` חסך עלות API, הפחית latency, ועמד בדרישות המפרט.
4. **OpenAI כספק יחיד**: בחירה מושכלת שפישטה משמעותית את הארכיטקטורה (auth אחד, SDK אחד, billing אחד) תוך שיפור האיכות לעברית/ערבית.
5. **ארכיטקטורת Microservices**: ההפרדה בין C# ל-Python אפשרה scaling עצמאי, deployment נפרד, ושמירה על מומחיות שפה לכל שכבה.

### המלצות לפיתוח עתידי

1. **שדרוג מודל**: מעבר ל-GPT-4o לשיחות מורכבות יותר.
2. **Vector Database**: שדרוג מ-MongoDB לפתרון ייעודי (Pinecone, Qdrant) לבסיסי ידע גדולים יותר.
3. **Fine-Tuning**: אימון מודל ייעודי על שאלות קבלה אקדמיות לשיפור הדיוק.
4. **WebSocket**: מעבר מ-REST ל-WebSocket לצ׳אט קולי רציף עם streaming תגובה.
5. **אינטגרציה עם ROS**: חיבור ה-`robot_client.py` לבקרת רובוט NAO/Pepper דרך ROS nodes.
6. **Analytics Dashboard**: הוספת גרפים וסטטיסטיקות מתקדמות ל-Admin Dashboard.

### סיכום

הפרויקט מממש בהצלחה מערכת Wrapper לרובוטים וירטואליים ופיזיים, המשלבת TTS, STT, ו-GenAI בארכיטקטורה גמישה ומודולרית. יישום ה-PoC (Ruppin Academic Advisor) פועל בייצור, מדגים את כוח ה-Wrapper, ומספק ערך עסקי אמיתי למוסד האקדמי. הפרויקט עומד בכל דרישות המפרט ומרחיב אותן בחידושים כגון RAG, embeddings מקומיים (PyTorch), ממשק ניהול, ולקוח ייחוס לרובוטים פיזיים.

---

## רשימה ביבליוגרפית

1. **OpenAI API Documentation** — Chat Completions, Whisper STT, TTS. https://platform.openai.com/docs
2. **Sentence-Transformers Library** — Reimers, N., & Gurevych, I. (2019). *Sentence-BERT: Sentence Embeddings using Siamese BERT-Networks.* EMNLP 2019. https://www.sbert.net
3. **Retrieval-Augmented Generation** — Lewis, P., et al. (2020). *Retrieval-Augmented Generation for Knowledge-Intensive NLP Tasks.* NeurIPS 2020.
4. **MongoDB Documentation** — Atlas, Driver for .NET. https://www.mongodb.com/docs
5. **Microsoft ASP.NET Core Documentation** — .NET 8.0. https://learn.microsoft.com/en-us/aspnet/core
6. **React Documentation** — React 18. https://react.dev
7. **Azure App Service Documentation** — Deployment, App Settings. https://learn.microsoft.com/en-us/azure/app-service
8. **Flask Documentation** — Micro Web Framework for Python. https://flask.palletsprojects.com
9. **Web Audio API** — MDN Web Docs. https://developer.mozilla.org/en-US/docs/Web/API/Web_Audio_API
10. **PyTorch Documentation** — https://pytorch.org/docs

---

## נספח א׳: מבנה קבצי הפרויקט

```
FinalProjectRina/
├── Client/                          # React/Vite Frontend
│   ├── src/
│   │   ├── pages/
│   │   │   ├── Chat.jsx             # ממשק צ׳אט קולי + טקסטואלי
│   │   │   ├── Admin.jsx            # לוח ניהול
│   │   │   ├── Login.jsx            # דף כניסה
│   │   │   └── Register.jsx         # דף הרשמה
│   │   ├── context/
│   │   │   └── LanguageContext.jsx  # ניהול שפה (HE/AR)
│   │   ├── api.js                   # קריאות HTTP לשרת
│   │   └── App.jsx                  # ניתוב ראשי
│   ├── public/
│   │   └── staticwebapp.config.json # תמיכה ב-SPA routing ב-Azure
│   └── package.json
│
├── Server/                          # C# ASP.NET Core Backend
│   ├── Controllers/
│   │   ├── ChatController.cs
│   │   ├── AdminController.cs
│   │   ├── UserController.cs
│   │   ├── SttController.cs
│   │   └── TtsController.cs
│   ├── BL/
│   │   ├── ChatService.cs           # לוגיקת RAG + Cosine Similarity
│   │   ├── KnowledgeSeeder.cs       # זריעת בסיס ידע + auto re-seed
│   │   ├── KnowledgeCache.cs        # in-memory cache
│   │   ├── SpeechService.cs
│   │   └── UserService.cs
│   ├── DAL/
│   │   ├── PythonAiProvider.cs      # גשר ל-Python Microservice
│   │   └── OpenAiSpeechProvider.cs
│   ├── AI_Service/                  # Python Flask Microservice
│   │   ├── app.py                   # /chat + /embed + /health
│   │   ├── robot_client.py          # לקוח ייחוס לרובוטים
│   │   └── requirements.txt
│   ├── Program.cs                   # הגדרת DI + startup
│   └── appsettings.json
│
└── Docs/
    ├── FinalProjectReport.md        # מסמך זה
    └── Azure_Deployment_Guide.md
```

---

## נספח ב׳: הוראות התקנה

### דרישות מקדימות
- .NET 8.0 SDK
- Python 3.9+
- Node.js 18+
- MongoDB Atlas (חשבון חינמי)
- OpenAI API Key

### משתני סביבה נדרשים

**שרת C# (`appsettings.json` או Azure App Settings)**:
```
ConnectionStrings__MongoDB=<mongodb+srv://...>
MongoDB__DatabaseName=FinalProjectRina
OpenAI__ApiKey=<sk-...>
PythonService__Url=http://localhost:5001
```

**שירות Python (`.env`)**:
```
OPENAI_API_KEY=<sk-...>
```

**לקוח React (בזמן build)**:
```
VITE_API_BASE_URL=http://localhost:5102
```

### הרצה מקומית

```bash
# Terminal 1 — Python AI Service
cd Server/AI_Service
pip install -r requirements.txt
python app.py

# Terminal 2 — C# Backend
cd Server
dotnet run

# Terminal 3 — React Client
cd Client
npm install
VITE_API_BASE_URL=http://localhost:5102 npm run dev
```

### בדיקת תקינות
```bash
curl http://localhost:5001/health   # Python: {"status":"healthy","openaiConfigured":true}
curl http://localhost:5102/health   # C#: {"status":"healthy"}
```

---

## נספח ג׳: ממשקי API

### POST /api/chat
יצירת תשובת AI לשאלת משתמש.

**Body**:
```json
{ "message": "מה הציון הנדרש למדעי המחשב?", "userId": "abc123" }
```
**Response**:
```json
{ "reply": "קבלה למדעי המחשב דורשת ממוצע משוקלל 105 ומתמטיקה 90+ ב-5 יחידות." }
```

### POST /api/stt
המרת קובץ אודיו לטקסט.

**Body**: `multipart/form-data` — שדה `audio` (WebM/WAV), שדה `language` (he/ar)

**Response**:
```json
{ "transcript": "מה הציון הנדרש?" }
```

### POST /api/tts
המרת טקסט לאודיו.

**Body**:
```json
{ "text": "ברוך הבא למרכז האקדמי רופין" }
```
**Response**: `audio/mpeg` (MP3 bytes)

### GET /api/admin/stats
רשימת משתמשים ונתוני שיחות (מנהלים בלבד).

**Query Param**: `userId=<adminId>`

**Response**:
```json
[
  {
    "name": "ישראל ישראלי",
    "email": "israel@example.com",
    "joinedAt": "2025-11-01T10:00:00Z",
    "initialQuestion": "מה דרישות הקבלה להנדסה?",
    "finalResult": "דרוש ממוצע 100 ומתמטיקה 80+ ב-4/5 יחידות."
  }
]
```

### POST /embed (Python Service)
חישוב embedding מקומי (PyTorch).

**Body**:
```json
{ "texts": ["מה הציון הנדרש?", "תוכנית הנדסת מחשבים"] }
```
**Response**:
```json
{ "embeddings": [[0.12, -0.34, ...], [0.56, 0.01, ...]] }
```

---

## נספח ד׳: אינטגרציה עם רובוטים פיזיים

### ארכיטקטורת ה-Wrapper לרובוטים

כל רובוט שיכול לשלוח בקשות HTTP סטנדרטיות — NAO, Pepper, Raspberry Pi, ROS node, או כל מכשיר IoT — יכול להשתמש ב-Wrapper ללא שינוי בצד השרת.

```
[ רובוט פיזי / וירטואלי ]
  |  מיקרופון → אודיו גולמי
  |
  |  POST /api/stt  { audio: <file>, language: "he" }
  ↓
[ C# — SttController → OpenAI Whisper → טקסט ]
  |
  |  POST /api/chat  { message, userId }
  ↓
[ C# — ChatService → RAG → Python /chat → GPT → תשובה ]
  |
  |  POST /api/tts  { text }
  ↓
[ C# — TtsController → OpenAI TTS → MP3 bytes ]
  |
  ↓
[ רובוט פיזי — רמקול מנגן את התשובה ]
```

### לקוח ייחוס (`robot_client.py`)

הקובץ `Server/AI_Service/robot_client.py` מממש את כל שלושת הממשקים:

```python
client = RobotWrapperClient(
    api_base_url="https://rina-api-05111937.azurewebsites.net",
    user_id="robot-01"
)

# מחזור קולי שלם: STT → Chat → TTS
reply_text, reply_audio = client.voice_turn("recording.webm", language="he")

# או: שליחת טקסט ישירות (ללא STT)
reply = client.chat("מה הציון הנדרש למדעי המחשב?")
audio_bytes = client.synthesize(reply)
# → audio_bytes מוזרם לרמקול הרובוט
```

**הרצת הדמו**:
```bash
# מול שרת מקומי
python robot_client.py --api http://localhost:5102 --text "שלום, אני רוצה ללמוד"

# מול שרת ייצור
python robot_client.py --api https://rina-api-05111937.azurewebsites.net --audio recording.webm
```

הלקוח מדגים שה-Wrapper עובד עם **כל סוכן** — בדפדפן, ברובוט פיזי, או בסוכן תוכנה — ללא שינוי בצד השרת.
