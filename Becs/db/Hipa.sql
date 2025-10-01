CREATE TABLE Users (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       username TEXT UNIQUE NOT NULL,
                       password_hash TEXT NOT NULL,
                       role TEXT CHECK(role IN ('admin', 'user', 'researcher')) NOT NULL,
                       created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                       last_login TIMESTAMP
);

-- View לסטודנטים (ללא PHI)
CREATE VIEW ResearcherView AS
SELECT blood_type, donation_date
FROM Donations;
