# BECS – Part 1

This project is Part 1 of the BECS assignment. It implements the initial functionality of a blood establishment computer system using ASP.NET Core MVC with SQLite as the database.

## Features Implemented in Part 1

* Intake of donated blood units.
* Issuing of blood units for routine and emergency use.
* Validation of input fields.
* Blood compatibility rules service.
* Unit tests to verify core functionality.

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
docker build -t becs-pt1 .
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
  becs-pt1
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
