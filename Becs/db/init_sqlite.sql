PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS Donors (
                                      DonorId TEXT PRIMARY KEY,
                                      DonorName TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS BloodUnits (
                                          Id TEXT PRIMARY KEY,               -- GUID as text
                                          ABO TEXT NOT NULL CHECK (ABO IN ('O','A','B','AB')),
    Rh  TEXT NOT NULL CHECK (Rh IN ('+','-')),
    DonationDate TEXT NOT NULL,        -- ISO 8601 string (yyyy-MM-dd or full timestamp)
    DonorId TEXT REFERENCES Donors(DonorId) ON DELETE CASCADE,
    Status TEXT NOT NULL DEFAULT 'Available' CHECK (Status IN ('Available','Reserved','Issued','Discarded')),
    DonationSource TEXT
    );

CREATE TABLE IF NOT EXISTS Issues (
                                      IssueId TEXT PRIMARY KEY,          -- GUID as text
                                      BloodUnitId TEXT REFERENCES BloodUnits(Id) ON DELETE SET NULL,
    ABO TEXT NOT NULL CHECK (ABO IN ('O','A','B','AB')),
    Rh  TEXT NOT NULL CHECK (Rh IN ('+','-')),
    IssueDate TEXT NOT NULL,           -- ISO 8601
    IssueType TEXT NOT NULL CHECK (IssueType IN ('Routine','Emergency','Transfer'))
    );

CREATE INDEX IF NOT EXISTS idx_donors_name        ON Donors(DonorName);
CREATE INDEX IF NOT EXISTS idx_blood_units_type   ON BloodUnits(ABO, Rh);
CREATE INDEX IF NOT EXISTS idx_blood_units_status ON BloodUnits(Status);
CREATE INDEX IF NOT EXISTS idx_issues_type        ON Issues(IssueType);
