using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MongoDB.Driver;

namespace FinalProjectRina.Server.BL;

public static class KnowledgeSeeder
{
    // sentence-transformers 'paraphrase-multilingual-MiniLM-L12-v2' produces 384-dim vectors
    private const int ExpectedEmbeddingDim = 384;

    public static async Task SeedAsync(IMongoDatabase db, string pythonServiceUrl, bool force = false)
    {
        var col = db.GetCollection<KnowledgeFact>("ruppinKnowledge");

        // Auto-detect stale embeddings: if existing facts use a different dimension
        // (e.g. 1536 from the old OpenAI embeddings), force a re-seed
        if (!force && col.CountDocuments(Builders<KnowledgeFact>.Filter.Empty) > 0)
        {
            var sample = col.Find(Builders<KnowledgeFact>.Filter.Ne<float[]>("embedding", null!))
                            .FirstOrDefault();
            if (sample?.Embedding != null && sample.Embedding.Length != ExpectedEmbeddingDim)
            {
                Console.WriteLine($"Embedding dimension mismatch ({sample.Embedding.Length} vs {ExpectedEmbeddingDim}). Re-seeding knowledge base.");
                force = true;
            }
            else
            {
                // Already populated (embeddings present at the correct dim, OR absent because
                // sentence-transformers isn't installed in this environment). Either way, do NOT
                // re-insert — re-inserting appended a duplicate copy of every fact on each startup.
                return;
            }
        }

        var facts = GetFacts();
        var now = DateTime.UtcNow;
        facts.ForEach(f => { f.Source = "seed"; f.CreatedAt = now; });

        if (!string.IsNullOrEmpty(pythonServiceUrl))
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(120);

                var texts = facts.Select(f => f.FactText).ToList();
                var response = await http.PostAsJsonAsync(
                    $"{pythonServiceUrl}/embed",
                    new { texts });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<EmbedResponse>();
                    if (result?.Embeddings != null)
                        for (int i = 0; i < result.Embeddings.Length && i < facts.Count; i++)
                            facts[i].Embedding = result.Embeddings[i];
                }
                else
                {
                    Console.WriteLine($"Warning: Python /embed returned {response.StatusCode}. Seeding without embeddings.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not generate embeddings: {ex.Message}. Falling back to keyword search.");
            }
        }

        // On a forced reseed, replace only the statically seeded facts — never wipe
        // facts the system learned from chats (source == "learned").
        if (force) col.DeleteMany(Builders<KnowledgeFact>.Filter.Ne(f => f.Source, "learned"));
        col.InsertMany(facts);
        Console.WriteLine($"Knowledge base seeded with {facts.Count} facts{(facts[0].Embedding != null ? $" + {ExpectedEmbeddingDim}-dim embeddings (sentence-transformers)" : " (no embeddings)")}.");
    }

    private record EmbedResponse(
        [property: JsonPropertyName("embeddings")] float[][] Embeddings);

    private static List<KnowledgeFact> GetFacts() => new()
    {
        // General
        new() { Category = "Institutional History", FactText = "מרכז אקדמי רופין הוקם בשנת 1949 והוא אחת המכללות המובילות בישראל." },
        new() { Category = "Student Population", FactText = "המוסד משרת כ-5,000 סטודנטים מכל חלקי הארץ." },
        new() { Category = "Academic Structure", FactText = "המרכז כולל ארבע פקולטות: מדעי החברה והקהילה (פסיכולוגיה, עבודה סוציאלית, סיעוד), הנדסה (הנדסות שונות ומדעי מחשב), מדעי הים, וניהול וכלכלה." },
        // Campuses
        new() { Category = "Campus Locations", FactText = "קמפוס הירוק ממוקם בעמק חפר על 150 דונם, מוקף בפרדסים וישובים כפריים, וקמפוס הכחול ממוקם במכמורת על קו הים." },
        new() { Category = "Main Campus Address", FactText = "קמפוס עמק חפר ממוקם בכביש 4, צפונית לצומת השרון." },
        new() { Category = "Marine Campus Address", FactText = "קמפוס מכמורת סמוך לקו הים, צפונית למחלף בית ינאי." },
        // Tuition
        new() { Category = "Tuition, Bachelor", FactText = "שכר לימוד בסיסי לתואר ראשון (מתכונת מלאה) לשנת תשפ\"ו הוא 12,017 ₪." },
        new() { Category = "Tuition, Master", FactText = "שכר לימוד בסיסי לתואר שני (מתכונת מלאה) הוא 16,239 ₪ (בחלוקה על פני שנתיים)." },
        new() { Category = "Tuition, Preparatory", FactText = "שכר לימוד למכינות קדם אקדמיות (9 חודשים) הוא 16,329 ₪." },
        new() { Category = "Tuition, Additional Fees", FactText = "בנוסף לשכר הבסיסי, סטודנטים משלמים עבור אבטחה, שרותי רווחה, ביטוח תאונות אישיות, ודמי חברות בארגוני סטודנטים." },
        new() { Category = "Tuition, Marine Sciences", FactText = "סטודנטים בפקולטה למדעי הים משלמים בנוסף דמי קמפוס ימי." },
        new() { Category = "Registration Fees", FactText = "דמי הרשמה במקוון הם 400 ₪, דמי הרשמה בפגישה עם יועצת הם 300 ₪, וקיימת הנחה זמנית של 50% בדמי הרישום." },
        // Scholarships
        new() { Category = "Scholarships, Accounting", FactText = "הפקולטה למנהל וכלכלה מעניקה מלגות למתקבלים מצטיינים בשיעור שכר לימוד בסיסי מלא לשנה אקדמית אחת לסטודנטים עם ציון פסיכומטרי של 700+ בשנה הראשונה." },
        new() { Category = "Scholarships, Reserves Service", FactText = "מלגות מיוחדות בגובה 5,000 ₪ זמינות למשרתי מילואים בתוכניות מסוימות." },
        new() { Category = "Scholarships, Economics Management", FactText = "הפקולטה לניהול וכלכלה מעניקה מלגות למתקבלים מצטיינים בשיעור שכר לימוד בסיסי מלא לשנה אקדמית אחת לסטודנטים עם ציון פסיכומטרי של 670 ומעלה בשנה הראשונה." },
        new() { Category = "Scholarships, MBA", FactText = "מלגות מיוחדות בגובה 5,000 ₪ זמינות למשרתי מילואים בתוכנית ה-MBA." },
        new() { Category = "Scholarships, Organizational Psychology", FactText = "משרתי מילואים בתוכנית הפסיכולוגיה הארגונית זכאים למלגה בגובה 5,000 ₪ אם עשו לפחות 90 יום שמ\"פ בשנה האחרונה החל מאוקטובר 2025." },
        new() { Category = "Scholarships, Marine Programs", FactText = "משרתי מילואים בתוכניות מדעי הים זכאים למלגה בגובה 5,000 ₪ אם עשו לפחות 90 יום שמ\"פ בשנה האחרונה החל מאוקטובר 2025." },
        new() { Category = "Scholarships, Preparatory Funding", FactText = "כ-70% מהסטודנטים במכינות קדם אקדמיות למדו במימון משרד הביטחון או משרד החינוך, וחלקם אף קיבלו מלגת קיום חודשית." },
        new() { Category = "Scholarships, Marine First Year", FactText = "פקולטת מדעי הים מעניקה מלגת שכר לימוד מלאה (100%) לשנה הראשונה ללימודי BSc לסטודנטים עם ציון פסיכומטרי של 690 ומעלה." },
        // Engineering Faculty
        new() { Category = "Electrical Engineering", FactText = "תואר ראשון בהנדסת חשמל ואלקטרוניקה זמין כחלק מפקולטת ההנדסה." },
        new() { Category = "Industrial Engineering", FactText = "תוכנית הנדסת תעשייה וניהול נמשכת 4 שנים אקדמיות (160 נ\"ז) בשתי אפשרויות: לימודי יום (3-5 ימים בשבוע) או לימודי ערב (2 ערבים בשבוע וביום שישי)." },
        new() { Category = "Industrial Engineering, Specializations", FactText = "תכנית הנדסת תעשייה וניהול מציעה שלוש התמחויות: ייצור ושירות בסביבה דיגיטאלית, מערכות מידע ובינה מלאכותית, ומדעי נתונים." },
        new() { Category = "Computer Engineering", FactText = "תוכנית הנדסת מחשבים נמשכת 4 שנים, 162 נקודות זכות, עם אפשרויות לימודי יום (3-5 ימים בשבוע) או לימודי ערב (2 ערבים בשבוע וביום שישי)." },
        new() { Category = "Computer Engineering, Specializations", FactText = "תוכנית הנדסת מחשבים מציעה קורסי בחירה בתחומים: בינה מלאכותית, סייבר ותקשורת מחשבים, ומערכות מחשבים." },
        new() { Category = "Computer Science", FactText = "תואר ראשון במדעי המחשב נמשך 3 שנים אקדמיות, 120 נקודות זכות, 3-4 ימי לימוד בשבוע." },
        new() { Category = "Computer Science, Specializations", FactText = "תכנית מדעי המחשב מציעה שני מסלולי התמחות: התמחות בבינה מלאכותית ומדעי הנתונים, והתמחות בפיתוח מערכות תוכנה." },
        new() { Category = "Admissions, Computer Science", FactText = "קבלה למדעי המחשב דורשת ממוצע משוקלל של 105 ומעלה וציון מתמטיקה 90+ ב-5 יחידות לימוד." },
        new() { Category = "Admissions, Engineering", FactText = "קבלה להנדסה (חשמל/תעשייה) דורשת ממוצע משוקלל של 100 ומעלה וציון מתמטיקה 80+ ב-4 או 5 יחידות לימוד." },
        // Management & Economics Faculty
        new() { Category = "Business Administration", FactText = "תואר ראשון במנהל עסקים נמשך 3 שנים אקדמיות, 120 נקודות זכות, 3 ימי לימוד בשבוע עם אפשרות ימי לימוד מרוכזים." },
        new() { Category = "Business Administration, Specializations", FactText = "תוכנית מנהל עסקים מוצעת בשלושה מסלולים: ניהול השיווק, מערכות מידע, וניהול משאבי אנוש ופיתוח ארגוני." },
        new() { Category = "Economics Management", FactText = "תואר ראשון בכלכלה ומנהל נמשך 3 שנים אקדמיות, 120 נקודות זכות, 3-4 ימי לימוד בשבוע." },
        new() { Category = "Economics Management, Specializations", FactText = "תוכנית כלכלה ומנהל מוצעת בשלוש התמחויות: מימון וייעוץ פיננסי, מדעי הנתונים, וכלכלת נדל\"ן ושמאות מקרקעין." },
        new() { Category = "Economics Accounting", FactText = "תואר ראשון בכלכלה וחשבונאות נמשך 3.5 שנים אקדמיות, 160.5 נקודות זכות, 4-5 ימי לימוד בשבועיים." },
        new() { Category = "Economics Accounting, Focus", FactText = "תוכנית כלכלה וחשבונאות מתמקדת בשלושה תחומים עיקריים: חשבונאות פיננסית, ביקורת חשבונות, ומסים וכלכלה." },
        new() { Category = "Economics Accounting, CPA Exemptions", FactText = "בוגרי תוכנית כלכלה וחשבונאות מקבלים פטור מהבחינות של מועצת רואי חשבון למעט שתי בחינות ההסמכה הסופיות." },
        new() { Category = "Economics Accounting, Success Rate", FactText = "בוגרי רופין בשנת 2024 דורגו במקום הראשון הארצי במבחני ההסמכה מטעם מועצת רואי החשבון עם 91% הצלחה." },
        new() { Category = "Psychology Management", FactText = "תואר ראשון בפסיכולוגיה ומנהל עסקים (דו-חוגי) נמשך 3 שנים אקדמיות, 120 נקודות זכות, 3 ימי לימוד בשבוע." },
        new() { Category = "Psychology Economics", FactText = "תואר ראשון בפסיכולוגיה וכלכלה (דו-חוגי) נמשך 3 שנים אקדמיות, 121 נקודות זכות, 4 ימי לימוד בשבוע." },
        // Social Sciences Faculty
        new() { Category = "Behavioral Sciences", FactText = "תואר ראשון במדעי ההתנהגות נמשך 3 שנים אקדמיות, 3 ימי לימוד בשבועיים, 120 נקודות זכות." },
        new() { Category = "Behavioral Sciences, Approach", FactText = "תוכנית מדעי ההתנהגות היא רב-תחומית בשלושה תחומי ידע עיקריים: פסיכולוגיה, סוציולוגיה ואנתרופולוגיה." },
        new() { Category = "Behavioral Sciences, Specializations", FactText = "תוכנית מדעי ההתנהגות מציעה התמחויות בפסיכולוגיה וניהול משאבי אנוש ופיתוח ארגוני." },
        new() { Category = "Nursing", FactText = "תואר ראשון בסיעוד (BSN) הוא תוכנית 4-שנתית, 167 שעות אקדמיות." },
        new() { Category = "Nursing, Study Format", FactText = "תוכנית סיעוד משלבת למידה תיאורטית וקלינית: שנה 1 - 4-5 ימים הוראה תיאורטית, שנה 2 - תערובת תיאוריה וקליניקה, שנה 3 - 3 ימים קליניים שבועיים, שנה 4 - 4 ימים הכשרה קלינית מתקדמת." },
        new() { Category = "Nursing, Licensing Success", FactText = "בוגרי תוכנית הסיעוד משיגים 97-100% הצלחה בבחינת הרישוי הממשלתית כדי להיות אחות רשום (R.N.)." },
        new() { Category = "Admissions, Nursing", FactText = "קבלה לסיעוד דורשת ציון פסיכומטרי 550 ומעלה ומעבר ראיון." },
        new() { Category = "Nursing, Clinical Facilities", FactText = "הכשרה קלינית מתקיימת בבתי חולים כולל מרכז רפואי הלל יפה, בית חולים מאיר, בית חולים לניאדו, ובית חולים רמבם." },
        new() { Category = "Social Work", FactText = "תואר ראשון בעבודה סוציאלית (BSW) נמשך 3 שנים אקדמיות, 121 נקודות זכות, 4 ימי לימוד בשבוע." },
        new() { Category = "Social Work, Licensing", FactText = "בוגרי תוכנית עבודה סוציאלית רשאים להירשם בפנקס העובדים הסוציאליים במשרד הרווחה, בהתאם לחוק העובדים הסוציאליים (1996)." },
        // Marine Sciences Faculty
        new() { Category = "Marine Science Environment", FactText = "תואר ראשון במדעי הים והסביבה הימית נמשך 3 שנים אקדמיות, 136 נקודות זכות, 4-5 ימי לימוד בשבוע." },
        new() { Category = "Marine Science Environment, Curriculum", FactText = "תוכנית מדעי הים משלבת מחקר אקדמי עם עבודה מעשית כולל קורסי צלילה מדעית וקורס משיטי ספינה, סיורים ופרויקטים מחקריים לאורך החופים הישראליים." },
        new() { Category = "Marine Science Environment, Uniqueness", FactText = "רופין הוא המוסד האקדמי היחיד בישראל המעניק תואר ראשון במדעי הים." },
        new() { Category = "Biotechnology", FactText = "תואר ראשון בביוטכנולוגיה נמשך 3 שנים אקדמיות, 4-5 ימי לימוד בשבוע, 136 נקודות זכות, בקמפוס מכמורת." },
        new() { Category = "Biotechnology, Specializations", FactText = "תוכנית ביוטכנולוגיה מציעה שני מסלולי התמחות: בסביבה הימית או בחקלאות מים וים." },
        // Master's Programs
        new() { Category = "MBA", FactText = "תוכנית MBA נמשכת 3 סמסטרים רצופים כולל סמסטר קיץ, היברידית: ימי רביעי 16:00-22:00 מרחוק וימי שישי 08:00-14:00 בקמפוס." },
        new() { Category = "MBA, Specializations", FactText = "סטודנטי ה-MBA בוחרים בהתמחות בין ארבע אפשרויות: ניהול פיננסי; שיווק ואסטרטגיה גלובלית; מנהיגות ואסטרטגיה; וייעוץ ארגוני." },
        new() { Category = "Logistics MA", FactText = "תואר שני בלוגיסטיקה ושרשרת האספקה הגלובלית נמשך שנה וחצי בלבד, 40 נקודות זכות, היברידי: ימי רביעי מקוון וימי שישי פרונטלי." },
        new() { Category = "Clinical Psychology MA", FactText = "תואר שני בפסיכולוגיה קלינית נמשך שנתיים אקדמיות, ארבעה סמסטרים, עם פרקטיקום רציף של שנתיים במרכזי בריאות נפש." },
        new() { Category = "Adulthood Clinical Psychology MA", FactText = "תואר שני בפסיכולוגיה קלינית של הבגרות והזיקנה נמשך שנתיים אקדמיות, ארבעה סמסטרים, 4-5 ימי לימודים בשבוע." },
        new() { Category = "Organizational Psychology MA", FactText = "תואר שני בפסיכולוגיה ארגונית נמשך שנתיים, לימוד בימי חמישי 08:00-18:00 בנוסף להתנסות בפרקטיקום. מציע שתי התמחויות: ייעוץ ארגוני והערכה ארגונית." },
        new() { Category = "Social Work MSW", FactText = "תואר שני בעבודה סוציאלית (MSW) בהתמחות טראומה וחוסן נמשך שנתיים אקדמיות, ארבעה סמסטרים, 40 נקודות זכות." },
        new() { Category = "Marine Resources Management MA", FactText = "תואר שני בניהול משאבי ים נמשך שנה וחצי, ארבעה סמסטרים רצופים (כולל סמסטר קיץ), 38 נקודות זכות. מתקיים בימי רביעי וישי בקמפוס מכמורת." },
        new() { Category = "Marine Sciences MSc", FactText = "תואר שני במדעי הים עם תזה (MSc) נמשך שנתיים, יומיים בשבוע בפקולטה למדעי הים. התוכנית היחידה והראשונה בתחום בישראל." },
        new() { Category = "Marine Sciences MSc, Curriculum", FactText = "קורסי תוכנית מדעי הים כוללים אוקיינוגרפיה ביולוגית, אקולוגיה ימית, אוקיינוגרפיה כימית ופיזיקלית, גאולוגיה ימית, חקלאות ימית ושימור הסביבה הימית." },
        // Preparatory Programs
        new() { Category = "Admissions, Mechina", FactText = "סטודנטים שחסרים להם ציונים נדרשים יכולים להירשם לתוכנית המכינה הקדם-אקדמית כדי לשפר את סיכויי הקבלה שלהם." },
        new() { Category = "Science Engineering Prep", FactText = "מכינה קדם אקדמית להנדסה ומדעים נמשכת תשעה חודשים, 4-5 ימי לימוד בשבוע. כוללת מתמטיקה (13 שע'), פיזיקה (13 שע'), אנגלית (12 שע') וכתיבה מדעית." },
        new() { Category = "Science Engineering Prep, Requirements", FactText = "דרישות קבלה למכינה להנדסה ומדעים: בוגרי 12 שנות לימוד, ציון 500 לפחות במבחן מימ\"ד או בחינה פסיכומטרית." },
        new() { Category = "Economics Admin Prep", FactText = "מכינה קדם אקדמית לניהול, כלכלה ומדעי החברה נמשכת תשעה חודשים. מיועדת למועמדים ללימודי מנהל עסקים, כלכלה, פסיכולוגיה ועבודה סוציאלית." },
        new() { Category = "Economics Admin Prep, Requirements", FactText = "דרישות קבלה למכינה קדם אקדמית לניהול: סיום 12 שנות לימוד, ציון 400 לפחות במבחן מימ\"ד או בבחינה הפסיכומטרית." },
        new() { Category = "Semesterly Prep, Engineering", FactText = "מכינה סמסטריאלית להנדסה ומדעים - 21 שבועות, שלושה ימים בשבוע בקמפוס. בוגריה יכולים להתקבל להנדסה, מחשבים, מדעי הים וביוטכנולוגיה." },
        new() { Category = "Academy Preparation", FactText = "תוכנית הכנה בדרך לאקדמיה נפתחת מדי שנה בשני מועדים: מאי ודצמבר, עם שני מסלולים - הנדסה וים, וניהול כלכלה ומדעי החברה." },
        new() { Category = "Academy Preparation, May Requirements", FactText = "דרישות קבלה לתוכנית הכנה במאי: ממוצע בגרות משוקלל 85+ או פסיכומטרי 550+, מתמטיקה 85+ בשלוש יחידות." },
        // Admissions & Registration
        new() { Category = "Admissions, General", FactText = "קבלה לתואר ראשון דורשת תעודת בגרות. קבלה לתואר שני דורשת תואר ראשון. ספי קבלה ספציפיים חלים לפי תוכנית." },
        new() { Category = "Admissions, Contact", FactText = "שירות לקוחות למתעניינים בלימודים זמין בטלפון 1-800-800-830, דרך WhatsApp, ודרך דוא\"ל meda@ruppin.ac.il." },
        new() { Category = "Registration, Period", FactText = "תקופת הרשמה רגילה משתרעת מ-1.1.2026 עד ה-31.5.2026 למרבית התוכניות." },
        new() { Category = "Registration, Steps", FactText = "תהליך ההרשמה כרוך בשישה שלבים: שיחה ראשונית עם שירות הלקוחות, פגישת יעוץ, בדיקת תנאי קבלה, הבנת מועדים ושכר לימוד, הרשמה מקוונת, והמתנה להודעת קבלה." },
        new() { Category = "Registration, Discount", FactText = "קיימת הנחה בדמי רישום של 50% לנרשמים לתואר ראשון (למעט מדעי הים וביוטכנולוגיה), בתוקף עד 16.5.26." },
        new() { Category = "Registration, Office Hours", FactText = "שעות פעילות משרד ההרשמה: ראשון-חמישי 08:00-19:00, שישי 09:00-13:00 (ינואר-יוני)." },
        // Housing & Student Life
        new() { Category = "Housing, Dormitories", FactText = "מתחם המעונות של המרכז האקדמי רופין כולל 146 דירות, כ-300 חדרים פרטיים עם גינות ירוקות ופינות פנאי." },
        new() { Category = "Housing, Apartment Types", FactText = "ההרשמה למעונות היא לדירות משותפות בעלות 2 או 3 שותפים. דירות ליחידים זמינות רק לדיירים בשנת המגורים השנייה ויותר." },
        new() { Category = "Housing, Facilities", FactText = "מתחם המעונות כולל חדר כביסה בשירות עצמי, תשתיות אינטרנט וכבלים, ומועדון סטודנטים עם מתחם לימודים שיתופי WeLearn." },
        new() { Category = "Housing, Contact", FactText = "משרד מעונות: טלפון 09-8983060, דוא\"ל meonot@ruppin.ac.il, פתוח ראשון-חמישי 08:00-10:00 ו-13:00-15:00." },
        // Student Services
        new() { Category = "Student Services, Accessibility", FactText = "מרכז נגישות מספק סיוע לימודי, התאמות בבחינות וחוות דעת אבחונות לסטודנטים עם צרכים מיוחדים." },
        new() { Category = "Student Services, Arab Students", FactText = "יחידת מרסאה (יחידה לסטודנטים מהחברה הערבית) מספקת שירותים ייעודיים לסטודנטים ערבים." },
        new() { Category = "Student Services, Career", FactText = "יחידת פיתוח קריירה עוזרת להשתלבות תעסוקתית, כוללת פורטל מקום עבודה לבוגרים וירידת קריירה עם תעשיינים." },
        new() { Category = "Student Services, Military", FactText = "הקלות למילואימים זמינות דרך משרד דקאנט הסטודנטים." },
        new() { Category = "Student Services, Emotional Support", FactText = "תמיכה רגשית וחיזוק חוסן זמינים לסטודנטים דרך שירותי דקאנט הסטודנטים." },
        // Library
        new() { Category = "Library, Hours", FactText = "שעות פתיחת הספרייה: ימים א'-ה' 08:00-20:00, יום ו' 08:00-12:00." },
        new() { Category = "Library, Resources", FactText = "הספרייה מציעה חיפוש ספרים, מאמרים, מאגרי מידע אקדמיים, קטלוג מקוון, הדרכה לכתיבת ביבליוגרפיה, וכלים AI לשימוש אקדמי." },
        new() { Category = "Library, Contact", FactText = "הספרייה ניתנת להשגה בטלפון 09-8983086, WhatsApp 054-3095217, ודוא\"ל library@ruppin.ac.il." },
        // Contact
        new() { Category = "Contact, Main Hotline", FactText = "קו טלפוני כללי: 1-800-800-830 לשאלות הרשמה ויעוץ אקדמי." },
        new() { Category = "Contact, Email", FactText = "כתובת דוא\"ל עיקרית: meda@ruppin.ac.il לשאלות כלליות." },
        new() { Category = "Learning Platforms", FactText = "מערכות הלמידה של המרכז כוללות RuppiNet (אזור אישי), Moodle (מודל), ותוכנות ייעודיות כמו Matlab ו-Simulink." },
    };
}
