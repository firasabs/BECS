-- 1) MonthlyUsage: historical issued units (for training & dashboards)
CREATE TABLE IF NOT EXISTS MonthlyUsage (
                                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                                            year INTEGER NOT NULL,
                                            month INTEGER NOT NULL CHECK(month BETWEEN 1 AND 12),
    blood_type TEXT NOT NULL CHECK(blood_type IN ('O','A','B','AB')),
    rh TEXT NOT NULL CHECK(rh IN ('+','-')),
    units_issued INTEGER NOT NULL DEFAULT 0,
    UNIQUE(year, month, blood_type, rh)
    );

-- 2) Forecasts: store model outputs to show in UI
CREATE TABLE IF NOT EXISTS Forecasts (
                                         id INTEGER PRIMARY KEY AUTOINCREMENT,
                                         year INTEGER NOT NULL,
                                         month INTEGER NOT NULL CHECK(month BETWEEN 1 AND 12),
    blood_type TEXT NOT NULL CHECK(blood_type IN ('O','A','B','AB')),
    rh TEXT NOT NULL CHECK(rh IN ('+','-')),
    predicted_units INTEGER NOT NULL,
    model_version TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(year, month, blood_type, rh, model_version)
    );

-- 3) DonorHealth: signals for eligibility model
CREATE TABLE IF NOT EXISTS DonorHealth (
                                           id INTEGER PRIMARY KEY AUTOINCREMENT,
                                           donor_id INTEGER NOT NULL,        -- FK -> Donors(id) in your schema
                                           hb_g_dl REAL,                     -- hemoglobin
                                           bp_systolic INTEGER,
                                           bp_diastolic INTEGER,
                                           age INTEGER,
                                           last_donation_date DATE,
                                           conditions_json TEXT,             -- JSON array of conditions
                                           eligible_label TEXT               -- optional: label snapshots for training ('Eligible'/'Not')
);

-- 4) EligibilityPredictions: log model decisions (auditability)
CREATE TABLE IF NOT EXISTS EligibilityPredictions (
                                                      id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                      donor_id INTEGER NOT NULL,
                                                      eligible_pred INTEGER NOT NULL CHECK(eligible_pred IN (0,1)),
    probability REAL NOT NULL,
    model_version TEXT NOT NULL,
    explanation TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
    );

-- 5) CompatibilityMatrix: rule table, seeded below
CREATE TABLE IF NOT EXISTS CompatibilityMatrix (
                                                   id INTEGER PRIMARY KEY AUTOINCREMENT,
                                                   recipient_blood_type TEXT NOT NULL CHECK(recipient_blood_type IN ('O','A','B','AB')),
    recipient_rh TEXT NOT NULL CHECK(recipient_rh IN ('+','-')),
    donor_blood_type TEXT NOT NULL CHECK(donor_blood_type IN ('O','A','B','AB')),
    donor_rh TEXT NOT NULL CHECK(donor_rh IN ('+','-')),
    is_compatible INTEGER NOT NULL CHECK(is_compatible IN (0,1)),
    rationale TEXT NOT NULL
    );

-- Seed ABO/Rh compatibility for RBC transfusion (basic hospital rules)
DELETE FROM CompatibilityMatrix;

-- O- to everyone
INSERT INTO CompatibilityMatrix(recipient_blood_type,recipient_rh,donor_blood_type,donor_rh,is_compatible,rationale)
SELECT r_bt, r_rh, 'O','-', 1, 'O- universal RBC donor'
FROM (SELECT 'O' AS r_bt UNION SELECT 'A' UNION SELECT 'B' UNION SELECT 'AB'),
     (SELECT '-' AS r_rh UNION SELECT '+');

-- O+ to O+, A+, B+, AB+
INSERT INTO CompatibilityMatrix
(recipient_blood_type, recipient_rh, donor_blood_type, donor_rh, is_compatible, rationale)
SELECT bt.r_bt, '+', 'O', '+', 1, 'O+ to Rh+ recipients'
FROM (SELECT 'O' AS r_bt UNION SELECT 'A' UNION SELECT 'B' UNION SELECT 'AB') AS bt;


-- A- to A-, AB-
INSERT INTO CompatibilityMatrix VALUES
                                    (NULL,'A','-','A','-',1,'A- to A-, AB-'),
                                    (NULL,'AB','-','A','-',1,'A- to AB-');

-- A+ to A+, AB+
INSERT INTO CompatibilityMatrix VALUES
                                    (NULL,'A','+','A','+',1,'A+ to A+'),
                                    (NULL,'AB','+','A','+',1,'A+ to AB+');

-- B- to B-, AB-
INSERT INTO CompatibilityMatrix VALUES
                                    (NULL,'B','-','B','-',1,'B- to B-, AB-'),
                                    (NULL,'AB','-','B','-',1,'B- to AB-');

-- B+ to B+, AB+
INSERT INTO CompatibilityMatrix VALUES
                                    (NULL,'B','+','B','+',1,'B+ to B+'),
                                    (NULL,'AB','+','B','+',1,'B+ to AB+');

-- AB- to AB-
INSERT INTO CompatibilityMatrix VALUES
    (NULL,'AB','-','AB','-',1,'AB- to AB-');

-- AB+ to AB+ (universal recipient)
INSERT INTO CompatibilityMatrix VALUES
    (NULL,'AB','+','AB','+',1,'AB+ universal RBC recipient');

-- Everything else: mark incompatible (optional prefill)
-- (You can enforce by query instead of storing all negatives.)
-- Use real donors if available
INSERT INTO DonorHealth (donor_id, hb_g_dl, bp_systolic, bp_diastolic, age, last_donation_date, conditions_json, eligible_label)
SELECT
    d.DonorId AS donor_id,
    -- Hb around 13.8 ±1.2 (bounded 11.0..17.0)
    CAST(
            MAX(11.0, MIN(17.0, 13.8 + ((ABS(RANDOM()) % 240) - 120) / 100.0))
        AS REAL) AS hb_g_dl,
    -- Systolic ~ 110..145
    110 + (ABS(RANDOM()) % 36) AS bp_systolic,
    -- Diastolic ~ 65..95
    65 + (ABS(RANDOM()) % 31) AS bp_diastolic,
    -- Age 18..65
    18 + (ABS(RANDOM()) % 48) AS age,
    -- Last donation between today-0 and today-180 days
    DATE('now', printf('-%d days', ABS(RANDOM()) % 181)) AS last_donation_date,
    -- 15% chance to have a condition
    CASE WHEN (ABS(RANDOM()) % 100) < 15
    THEN '["hypertension"]'
    ELSE '[]'
END AS conditions_json,
  -- Label using clinical-like thresholds
  CASE
    WHEN
      (MAX(11.0, MIN(17.0, 13.8 + ((ABS(RANDOM()) % 240) - 120) / 100.0))) >= 12.5
      AND (110 + (ABS(RANDOM()) % 36))    < 140
      AND (65 + (ABS(RANDOM()) % 31))     < 90
      AND CAST((julianday('now') - julianday(DATE('now', printf('-%d days', ABS(RANDOM()) % 181)))) AS INT) >= 56
      AND NOT ((ABS(RANDOM()) % 100) < 15)
    THEN 'Eligible' ELSE 'Not Eligible'
END AS eligible_label
FROM Donors d
ORDER BY d.DonorId
LIMIT 250;
