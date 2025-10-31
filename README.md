# 🩸 BECS
**Blood Establishment Computer Software (BECS) with HIPAA & Part 11 Compliance**  


**BECS** (Blood Establishment Computer Software) is a .NET-based web application designed to manage and automate critical workflows within blood banks while maintaining compliance with **HIPAA** and **FDA 21 CFR Part 11** standards.

The system enables hospitals, laboratories, and research institutions to:
- Record blood donations and issue blood units.
- Maintain an auditable, tamper-evident record of every action.
- Enforce role-based access to protect patient and donor privacy.
- Support researchers with **de-identified datasets**.
- Integrate **AI models** to forecast blood demand and evaluate donor eligibility.

---

## 🧩 Main Features

### 🔐 **Secure Authentication & Role Management**
- SHA-256 + per-user salted password encryption.
- Roles:
  - **Admin** – Full system control: manage users, donations, issues, and audit trails.  
  - **Staff** – Authorized to record donations and issue blood units.  
  - **Researcher** – Read-only access to de-identified data for research purposes.

### 🧾 **FDA Part 11 Audit Trail**
- All user actions (login, logout, data insert, issue, etc.) are logged.
- Each log record contains:
  - Timestamp, user, action type, and request metadata.
  - A **cryptographic chain hash** using `prev_hash + pepper` → tamper-evident structure.
- Enables complete traceability and accountability.

### 🩸 **HIPAA Compliance**
- Protects **PHI** (Protected Health Information) using strict access control.
- Researchers can only view **de-identified data** such as:
  - Blood type (ABO/Rh)
  - Donation date
  - Status and source  
  _(Names, IDs, and addresses are hidden.)_

### 🤖 **AI-Powered Decision Support**
Two machine learning modules integrated via **ML.NET**:

#### 1. **Donor Eligibility Model**
Predicts whether a donor is eligible based on:
- Hemoglobin level  
- Age  
- Blood pressure (Systolic/Diastolic)  
- Days since last donation  
- Medical conditions
#### 2. **Demand Forecasting**
Predicts the expected monthly demand for blood units by type and Rh factor.- Hemoglobin level  
- Forecast view grouped by month and blood type.  
- Displays predicted units and total per group. 
- Color coded indicators for high/low demand months.
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
docker run --name becs \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -p 5080:8080 \
  -v "$(pwd)/becs.db:/data/becs.db" \
  -v "$(pwd)/MLModels:/MLModels:ro" \
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
