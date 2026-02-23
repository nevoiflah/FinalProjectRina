-- Script to create the Ruppin Knowledge Base table and seed it with accurate facts

CREATE TABLE NLA_RuppinKnowledge (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Category NVARCHAR(100) NOT NULL,
    FactText NVARCHAR(MAX) NOT NULL
);

-- Seed Data (Based on scraped knowledge)
INSERT INTO NLA_RuppinKnowledge (Category, FactText) VALUES
-- Faculties
('Faculties', 'Ruppin Academic Center offers Bachelor''s and Master''s degrees across four main faculties: Faculty of Management & Economics, Faculty of Engineering, Faculty of Social & Community Sciences, and Faculty of Marine Sciences.'),
('Faculties, Marine', 'The Faculty of Marine Sciences is located at the Mikhmoret campus. It provides BSc degrees in Marine Biotechnology and Marine Sciences/Environment, and MSc/MA degrees. Ruppin is the only academic institution in Israel granting Bachelor''s degrees in marine sciences.'),

-- Admissions
('Admissions, General', 'General admission requires a high school Bagrut diploma for Bachelor''s programs and a Bachelor''s degree for Master''s programs. Specific thresholds apply per degree.'),
('Admissions, Computer Science', 'Admission to Computer Science requires a Weighted Average of 105+ and a Math score of 90+ in 5 units.'),
('Admissions, Engineering', 'Admission to Engineering (Electrical/Industrial) requires a Weighted Average of 100+ and a Math score of 80+ in 4 or 5 units.'),
('Admissions, Nursing', 'Admission to the Nursing (BSN) program specifically requires a Psychometric exam score of 550+ and passing an interview.'),
('Admissions, Mechina', 'Students missing required grades can enroll in the Pre-Academic Preparatory Program (Mechina) to replace their missing grades and improve their chances of acceptance.'),

-- Scholarships
('Scholarships', 'Ruppin offers various merit-based scholarships. Eligible local students can receive up to $1,200 per year.'),
('Scholarships, Marine', 'The Faculty of Marine Sciences provides a full-tuition (100%) scholarship for the first year of BSc studies for students entering with a psychometric exam score of 690 or higher.'),
('Scholarships, Logistics', 'Scholarships are granted in collaboration with the Sachish family and the Shipping Administration for outstanding projects and Master''s theses in Logistics and Global Supply Chain.'),

-- Logistics & Campus Life
('Dorms, Housing', 'The Ruppin Educational Center provides 400 dorm rooms for students. While on-campus housing is limited, the center offers assistance in finding off-campus housing in Emek Hefer.'),
('Tuition', 'Estimated annual tuition is subsidized for specific BSc programs. Note that rates vary based on exact degree choices.');
