# <img src="frontend/public/favicon.svg" width="32" alt="" align="top" /> dashy

A dashboard application built with .NET 10 (ASP.NET Core + SQLite) and React + TypeScript + Vite.

## Stack

| Layer    | Tech                              | Dev port |
|----------|-----------------------------------|----------|
| API      | .NET 10, EF Core, SQLite          | 8080     |
| Frontend | React 19, Vite, Tailwind, shadcn  | 5173     |

---

## Option 1 — Docker (full stack)

**Prerequisites:** Docker with Compose

```bash
# 1. Create the env file
cp .env.example .env

# 2. Generate an encryption key and paste it into .env
openssl rand -base64 32

# 3. Build and start
docker compose up --build
```

| Service  | URL                    |
|----------|------------------------|
| Frontend | http://localhost:3000  |
| API      | http://localhost:8080  |

To stop: `docker compose down`  
To wipe the SQLite volume: `docker compose down -v`

---

## Option 2 — Local dev (Rider / VS Code)

Run the API and frontend separately for hot-reload on both sides.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 24+ and npm

### 1 — Start the API

`appsettings.Development.json` already provides a dev encryption key and a local SQLite database (`dashy-dev.db`), so no extra configuration is needed.

**Rider**

1. Open `backend/Dashy.sln`
2. Select the `Dashy.Api` run configuration
3. Click **Run** (or **Debug**)

**VS Code**

1. Install the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension
2. Open the repository root
3. Open the Command Palette → **.NET: Run** and select `Dashy.Api`

Or from the terminal:

```bash
cd backend
dotnet run --project Dashy.Api
```

API is available at http://localhost:8080.  
Scalar API docs: http://localhost:8080/scalar/v1

### 2 — Start the frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend is available at http://localhost:5173.  
Vite proxies all `/api` requests to `http://localhost:8080` automatically.

---

## Running tests

```bash
cd backend
dotnet test
```
