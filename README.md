# BECS (Blood Establishment Computer Software) — Part 1

> **Course:** Intro to Biomedical Software • **Tech:** .NET 8, ASP.NET Core MVC, SQLite

This document describes **Part 1** of the BECS assignment: what’s implemented, how to run it, and how to validate the functionality during grading.

---

## ✨ What’s implemented in Part 1

- **Donation Intake UI**
  - Record a donated blood unit with: **ABO** (O/A/B/AB), **Rh** (+/−), **Donation Date**, **Donor ID**, **Donor Name**.
  - Server‑side validation of inputs and clear error messages.
  - Data persisted to a local **SQLite** database (`becs.db`).

- **Issue (Routine) UI**
  - Request X units of a specific **ABO + Rh**.
  - If exact type is low/out‑of‑stock, show **alternative compatible types** (via `BloodCompatibilityService`).

- **Issue (Emergency) UI** (basic)
  - Emergency issuing flow (e.g., prioritizes universal donor logic and/or compatible alternatives).

- **Business Logic**
  - `BloodCompatibilityService` maps recipient type → compatible donor types and orders alternatives (e.g., prefer **O−** when applicable).

- **Data Layer**
  - `SqliteIntakeRepository`, `SqliteIssueRepository` using `Microsoft.Data.Sqlite` and parameterized SQL.

- **Unit Tests** (xUnit)
  - Model validation and business‑logic tests (currently **6 tests** passing).

> Note: This part does **not** include inventory expiration logic, advanced auditing, or user authentication; those belong to later parts.

---

## 🧱 Project structure (high level)

```
WebApplication1/
├─ Becs/                      # ASP.NET Core MVC app
│  ├─ Controllers/
│  │  ├─ DonationsController.cs
│  │  ├─ IssueController.cs   # /Issue/Routine, /Issue/Emergency
│  │  └─ HomeController.cs
│  ├─ Data/
│  │  ├─ SqliteIntakeRepository.cs
│  │  └─ SqliteIssueRepository.cs
│  ├─ Models/
│  │  ├─ BloodModels.cs       # BloodUnit, etc.
│  │  ├─ DonationInput.cs
│  │  └─ AltSuggestion.cs
│  ├─ Services/
│  │  └─ BloodCompatibilityService.cs
│  ├─ Views/
│  │  ├─ Donations/Index.cshtml
│  │  ├─ Issue/Routine.cshtml
│  │  ├─ Issue/Emergency.cshtml
│  │  ├─ Home/Index.cshtml
│  │  └─ Shared/*.cshtml
│  ├─ becs.db                 # created at first run if missing
│  └─ Program.cs
└─ Becs.Tests/                # xUnit tests
   ├─ BloodCompatibilityServiceTests.cs
   └─ DonationInputValidationTests.cs
```

---

## 🖥️ Prerequisites

- **.NET SDK 8.0** (tested on macOS; also works on Windows/Linux)
- **SQLite** is bundled via `Microsoft.Data.Sqlite` (no server install needed)

Check versions:
```bash
dotnet --info
```

---

## ▶️ How to run (lecturer quick‑start)

From the solution root (the folder that contains both `Becs/` and `Becs.Tests/`):

```bash
# restore & build
dotnet restore
dotnet build

# run the web app
cd Becs
dotnet run
```

The console will show the listening URLs, e.g.:
```
Now listening on: http://localhost:5xxx
```
Open the printed URL in a browser.

> The app creates `becs.db` in the **Becs/** folder if the file doesn’t exist.

---

## 🧪 How to run tests

From the solution root:
```bash
dotnet test Becs.Tests/Becs.Tests.csproj
```
Expected output for Part 1: **All tests pass** (e.g., `Passed: 6`).

(Optional) Collect code coverage:
```bash
dotnet test Becs.Tests/Becs.Tests.csproj --collect:"XPlat Code Coverage"
```

---

## 🔎 Manual grading script (suggested steps)

1) **Donation Intake**  
   - Navigate to **/Donations**.  
   - Enter: ABO = `A`, Rh = `+`, a valid date (e.g., today), Donor ID = `123456789`, Donor Name = `John Doe`.  
   - Submit and verify success feedback.

2) **Data persisted**  
   - Try adding another unit (e.g., `O−`).  
   - If desired, confirm the records exist by checking the UI flow or with any provided list/confirmation (Part 1 stores to SQLite).

3) **Issue (Routine)**  
   - Navigate to **/Issue/Routine**.  
   - Request 1–2 units for `A+`.  
   - If exact stock is limited, the UI should show **alternatives** (compatible donor types) as suggestions.

4) **Issue (Emergency)**  
   - Navigate to **/Issue/Emergency**.  
   - Follow the flow to issue units for an emergency case and observe the fallback/compatibility logic.

5) **Validation checks**  
   - Try invalid ABO (e.g., `X`) or missing fields and verify error messages appear.

> If you want to reset the state for re‑testing, delete `Becs/becs.db` and restart the app.

---

## ⚙️ Configuration & Notes

- **Database file:** `Becs/becs.db` (created automatically).  
  Deleting it resets the demo data.
- **Compatibility matrix:** Served by `BloodCompatibilityService`, which also orders alternatives (e.g., prefer O− when applicable).
- **Security:** Part 1 is a local demo (no auth). Later parts will handle roles/auditing.

---

## 🧰 Troubleshooting (common student pitfalls we solved)

- **“Duplicate assembly attributes”**: remove `Properties/AssemblyInfo.cs` **or** set `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` in the app `.csproj` (not both). Clean `bin/obj` and rebuild.
- **Tests compiled into app**: ensure `Becs.Tests/` is a **sibling** of `Becs/` (not inside it). If needed, exclude test files in `Becs.csproj` with `<Compile Remove="Becs.Tests/**/*.cs" />`.
- **Running tests picks the app DLL**: always run `dotnet test Becs.Tests/Becs.Tests.csproj` or the solution, not from inside `Becs/`.
- **SQLite reset**: delete `becs.db` and run again.

---

## ✅ What’s next (beyond Part 1)

- Inventory expiration and FIFO issuing
- Detailed stock dashboards & reports
- Authentication/authorization (roles for staff)
- Audit trail and error logging

---

## 🇮🇱 הוראות ריצה (תקציר בעברית)

1. מתקינים **.NET 8**.
2. בטרמינל נכנסים לתיקיית הפרויקט הראשית ומריצים:
   ```bash
   dotnet restore
   dotnet build
   cd Becs
   dotnet run
   ```
3. נכנסים לכתובת שמודפסת במסך (למשל `http://localhost:5xxx`).
4. מסכי בדיקה:
   - **Donations** – הוספת תרומה עם ולידציה ושמירה ל‑SQLite (`becs.db`).
   - **Issue/Routine** – בקשת מנות, כולל הצעות חלופיות מתאימות.
   - **Issue/Emergency** – זרימת חירום בסיסית.
5. בדיקות יחידה:
   ```bash
   dotnet test Becs.Tests/Becs.Tests.csproj
   ```

---

**Contact (for grading):** Please run the quick script above. If anything fails to build/run, let me know and I’ll provide a minimal zipped build with instructions.

