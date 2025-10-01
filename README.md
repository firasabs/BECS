# 🩸 BECS – Part 3  
**Blood Establishment Computer Software (BECS) with HIPAA & Part 11 Compliance**  

This is **Part 3** of the BECS project. In this phase, the system is extended to include:  

- **User authentication & roles** (Admin, User, Researcher).  
- **HIPAA compliance** for Protected Health Information (PHI).  
- **De-identified data views** for researchers.  
- **Full support for FDA Part 11 audit trail** (tamper-evident logs with chained hashes).  
- **Dockerized deployment** for portability.  

---

## 🚀 Features

### ✅ Authentication & Roles
- **Login/Logout** with secure password storage (SHA-256 + unique salt).  
- **Admin**:  
  - Manage all users (create new accounts).  
  - Full access to donations, issues, audit metadata.  
- **User (Blood Bank Staff)**:  
  - Record blood donations.  
  - Issue blood units (routine/emergency).  
- **Researcher (Student)**:  
  - Read-only access to de-identified inventory via `ResearcherView`.  
  - Cannot view personal donor info (name, ID, address).

### ✅ HIPAA Compliance
- All PHI fields (name, ID, etc.) restricted to **admin/staff only**.  
- Researchers only see anonymized data (`ABO`, `Rh`, `DonationDate`, `Status`, `Source`).  

### ✅ Part 11 Audit Trail
- Every action (login, logout, donation, issue, query) logged in `audit_logs`.  
- Logs include timestamp, user, action, request metadata, and cryptographic chain hash.  
- Tamper-evident: each log stores `prev_hash + pepper` to form a verifiable chain.  

---
## Prerequisites

* [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running.
* Git (if cloning the repository).

## Running the Project with Docker

The project has been containerized for easy execution without requiring the lecturer to set up the .NET environment manually.

### 1. Clone the repository

```bash
git clone <repo-url>
cd Becs
```

### 2. Build the Docker image

```bash
docker build -t becs .
```

### 3. Prepare the database file (host-persisted)

Create an empty SQLite database file on the host machine. This file will be mounted into the container so that all inserted data is saved and persists between runs.

```bash
touch becs.db
```

### 4. Run the container

Run the container while mapping the host database file into the container’s writable `/data` directory:

```bash
docker run -e ASPNETCORE_ENVIRONMENT=Development \
  -p 5080:8080 \
  -v "$(pwd)/becs.db:/data/becs.db" \
  becs
```

* The application will now be available at: **[http://127.0.0.1:5080](http://127.0.0.1:5080)**
* Inserted records will be saved in `becs.db` on your host machine.

### 5. Health check

You can verify the app is running with:

```
http://127.0.0.1:5080/healthz
```

## Running Tests

Unit tests are included and can be run with:

```bash
dotnet test Becs.Tests/Becs.Tests.csproj
```

## Notes

* If port `5080` is busy, you can change the left side of the port mapping (e.g., `-p 5090:8080`) and then access the app at `http://127.0.0.1:5090`.
* The mounted `becs.db` file ensures persistence between container runs and allows the lecturer to inspect the DB if needed.
