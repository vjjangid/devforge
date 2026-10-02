# How DevForge works

A plain-language tour of DevForge Phase 1: what each part is, why it exists, how the parts work together, and how to run it and see it for yourself.

The [README](../README.md) is the reference (every endpoint, every setting). This document is the explanation. Read this one first.

## Contents

1. [What DevForge is](#1-what-devforge-is)
2. [The big picture](#2-the-big-picture)
3. [Follow one click: what happens when you press Deploy](#3-follow-one-click-what-happens-when-you-press-deploy)
4. [The parts, one by one](#4-the-parts-one-by-one)
5. [Inside the backend: the four layers](#5-inside-the-backend-the-four-layers)
6. [The ideas worth understanding](#6-the-ideas-worth-understanding)
7. [Run it](#7-run-it)
8. [A guided tour: ten things to try](#8-a-guided-tour-ten-things-to-try)
9. [Looking inside while it runs](#9-looking-inside-while-it-runs)
10. [When something goes wrong](#10-when-something-goes-wrong)
11. [What is real and what is pretend](#11-what-is-real-and-what-is-pretend)
12. [Where to find things in the code](#12-where-to-find-things-in-the-code)
13. [Glossary](#13-glossary)

---

## 1. What DevForge is

Services like Heroku and Render let a developer say "here is my code, please run it" and take care of the rest: fetch the code, build it, test it, put it on a server, and show whether it worked.

DevForge is a small version of that idea that you run on your own machine, built to learn how such a platform works on the inside.

In Phase 1 you can:

- register an application (a name, a Git repository address, a branch);
- press **Deploy**;
- watch the deployment move through its steps, with logs appearing as it goes;
- see it finish as **Succeeded** or **Failed**;
- see totals and recent activity on a dashboard.

One thing to know up front: **the deployment steps are pretend in Phase 1.** No code is downloaded or built. The worker waits a few seconds per step and writes the log lines a real build would write. Everything around that pretend work is real: the records, the waiting line, the status changes, the screens. Section 11 spells out exactly which is which.

---

## 2. The big picture

DevForge is four programs that run at the same time.

```text
   You
    │
    ▼
┌──────────┐    asks for data,     ┌──────────┐    saves and reads    ┌────────────┐
│ Frontend │ ───────────────────▶  │   API    │ ───────────────────▶  │  Database  │
│ (Angular)│   sends your clicks   │  (.NET)  │                       │(PostgreSQL)│
└──────────┘                       └──────────┘                       └────────────┘
                                                                            ▲
                                                    picks up waiting jobs,  │
                                                    writes progress         │
                                                                      ┌──────────┐
                                                                      │  Worker  │
                                                                      │  (.NET)  │
                                                                      └──────────┘
```

A restaurant is a fair comparison:

| DevForge part | Restaurant | Its job |
|---|---|---|
| Frontend | The menu and the table | What you look at and click |
| API | The waiter | Takes your request, checks it makes sense, writes it down, answers straight away |
| Database | The order rail in the kitchen | The one place where every order and its current state is written |
| Worker | The cook | Takes the next waiting order, does the slow work, updates the ticket as it goes |

The detail that matters most: **the waiter never talks to the cook.** The waiter pins a ticket on the rail and walks away. The cook looks at the rail and takes the next ticket. In DevForge, the API writes a row into the database and returns. The Worker finds that row by itself. The two programs share nothing except the database.

That separation is why the screen stays quick while a deployment takes twelve seconds, and why you can later replace the cook (pretend work today, Docker or Kubernetes tomorrow) without touching the waiter.

---

## 3. Follow one click: what happens when you press Deploy

This is the heart of the system. Here is every step, in order.

**Step 1 — You click Deploy.**
The frontend sends a request to the API: "create a deployment for application X".

**Step 2 — The API checks the request.**
Does application X exist? Is another deployment for it already in progress? If so, the API says no (`409 Conflict`).

**Step 3 — The API writes one row and answers.**
It saves a new deployment with status `Queued` and a first log line ("Deployment v1 queued"), then replies with the deployment's details. This takes a few milliseconds. The API's part is finished.

**Step 4 — The frontend opens the deployment page and starts asking.**
Every two seconds it asks the API: "what is the status now, and are there new log lines?"

**Step 5 — The Worker notices the row.**
The Worker checks the database every two seconds for the oldest `Queued` deployment. It finds yours and *claims* it: it sets the status to `Running`, records its own name on the row, and stamps the start time. Claiming is done in a way that makes it impossible for two workers to take the same one (see [section 6](#claiming-safely)).

**Step 6 — The Worker walks through the pipeline.**
Four stages, always in this order:

```text
Preparing  →  Building  →  Testing  →  Deploying
```

For each stage the Worker:

1. saves "the current stage is now X" to the database;
2. does the stage's work (in Phase 1: waits about three seconds and writes log lines);
3. moves to the next stage.

**Step 7 — The Worker records the ending.**
If all four stages finish, the status becomes `Succeeded`. If a stage fails, the status becomes `Failed`, the reason is saved, and the stage that failed stays recorded.

**Step 8 — The frontend sees the final status and stops asking.**

Because every change is saved the moment it happens, each time the frontend asks, it gets a slightly newer picture. That is what makes the pipeline appear to move on your screen.

```text
 time ─────────────────────────────────────────────────────────────────▶

 API      writes "Queued" ─ done
 Worker            claims ─ Preparing ─ Building ─ Testing ─ Deploying ─ Succeeded
 Frontend    ask    ask     ask    ask     ask    ask    ask     ask    ask (stop)
```

---

## 4. The parts, one by one

### 4.1 Frontend — what you see

**Location:** `frontend/devforge-web/`  **Built with:** Angular

**What it does.** Draws the screens in your browser and turns your clicks into requests to the API. It holds no data of its own; everything it shows was fetched from the API a moment ago.

**The screens:**

| Screen | Address | What it shows |
|---|---|---|
| Dashboard | `/dashboard` | Four totals, recent applications, recent deployments |
| Applications | `/applications` | A table of every application, with Edit and Delete |
| New / Edit application | `/applications/new`, `/applications/{id}/edit` | The form |
| Application | `/applications/{id}` | Details, the **Deploy** button, deployment history |
| Deployment | `/deployments/{id}` | Status, pipeline steps, timings, live logs |

**How it is organised inside** (`src/app/`):

- `core/` — the plumbing every screen needs: the shapes of the data (`models/`), the code that talks to the API (`services/`), and the code that turns any API failure into one readable error (`interceptors/`).
- `shared/` — small reusable pieces: the coloured status badge, the loading spinner, the error box, the confirmation dialog.
- `features/` — one folder per area of the app: `dashboard/`, `applications/`, `deployments/`.

**What it helps with.** It is the only part a user ever touches. It also makes waiting visible: loading states, empty states ("No applications yet"), error messages with a Retry button, and the live pipeline.

### 4.2 API — the front desk

**Location:** `backend/DevForge.Api/` (plus the three layers behind it)  **Built with:** ASP.NET Core

**What it does.** Listens for requests from the frontend and answers each one quickly. For every request it:

1. checks the input is acceptable;
2. reads from or writes to the database;
3. sends back a tidy answer, or a clear error.

**What it deliberately does not do.** It never runs a deployment. If it did, your browser would hang for twelve seconds per click, and a restart of the API would kill deployments in flight.

**Examples of what you can ask it:**

| You ask | It does |
|---|---|
| "List applications" | Reads them, works out each one's status from its latest deployment |
| "Create this application" | Validates, tidies the repository address, saves it |
| "Deploy application X" | Saves a `Queued` deployment and returns |
| "How is deployment Y?" | Reads the row and turns it into the list of pipeline steps |
| "Logs for deployment Y after line 12" | Returns only the newer lines |
| "Are you healthy?" | Answers yes, and separately whether it can reach the database |

**What it helps with.** It is the single gatekeeper for the data. The frontend cannot reach the database directly, so every rule ("names must be unique", "one deployment at a time") is enforced in one place.

### 4.3 Database — the shared memory

**Location:** runs as a container; its structure is defined in `backend/DevForge.Infrastructure/Persistence/`  **Built with:** PostgreSQL

**What it does.** Stores everything, permanently. If you stop every program and start them again tomorrow, your applications and their history are still there.

**The four tables:**

| Table | One row is… | Why it exists |
|---|---|---|
| `applications` | One registered application | The list of things you can deploy |
| `deployments` | One press of the Deploy button | The history, the current status, **and the waiting line** for the Worker |
| `builds` | The build step of one deployment | Kept separate so that a later phase can redeploy an old build (a rollback) without rebuilding |
| `deployment_logs` | One line of output | What you read on the deployment page |

How they relate:

```text
applications
   ├── deployments ──── deployment_logs
   │        │
   └── builds ◀─────────┘   (each deployment points to the build it produced)
```

**What it helps with.** Three things at once:

- **Memory** — nothing is lost on restart.
- **The meeting point** — it is how the API and the Worker cooperate without knowing about each other.
- **The last line of defence** — some rules are enforced by the database itself, so they hold even if two requests arrive in the same instant (see [section 6](#rules-the-database-enforces)).

### 4.4 Worker — the one that does the slow work

**Location:** `worker/DevForge.Worker/`  **Built with:** .NET Worker Service

**What it does.** Runs forever in a loop:

```text
repeat forever:
    is there a queued deployment?
        yes → claim it, run it to the end, then look again immediately
        no  → wait two seconds, then look again
```

It has no web address and no screen. You only know it is alive from its log output and from deployments moving forward.

**What it helps with.**

- **Keeps the API fast.** Slow work happens somewhere else.
- **Survives trouble.** If the database is briefly unreachable, the Worker logs a warning and tries again rather than crashing.
- **Scales by copying.** Need more deployments at once? Start more Workers. Each takes a different job.
- **Shuts down politely.** If you stop it in the middle of a deployment, it first marks that deployment `Failed` with the reason "The worker shut down before the deployment finished."

### 4.5 Docker Compose — the stage manager

**Location:** `docker-compose.yml`, `docker/`, `.env`

**What it does.** Starts all four programs with one command, in the right order, each in its own container:

1. `postgres` starts and waits until it accepts connections;
2. `api` starts, creates or updates the database tables, and waits until it reports healthy;
3. `worker` and `frontend` start.

**What it helps with.** You do not need .NET or Node installed to run DevForge, and the setup is the same on every machine. It also keeps the database password out of the code: it lives in `.env`, which Git ignores.

---

## 5. Inside the backend: the four layers

The API and the Worker share most of their code. That shared code is split into layers, each with one kind of responsibility. Think of them as rings, with the rules in the middle and the outside world at the edge.

```text
        ┌─────────────────────────────────────────────┐
        │  Api  /  Worker        (the two programs)   │
        │   ┌─────────────────────────────────────┐   │
        │   │  Infrastructure  (database, queue)  │   │
        │   │   ┌─────────────────────────────┐   │   │
        │   │   │  Application  (use cases)   │   │   │
        │   │   │   ┌─────────────────────┐   │   │   │
        │   │   │   │  Domain  (rules)    │   │   │   │
        │   │   │   └─────────────────────┘   │   │   │
        │   │   └─────────────────────────────┘   │   │
        │   └─────────────────────────────────────┘   │
        └─────────────────────────────────────────────┘
```

| Layer | In plain words | Example of what lives there |
|---|---|---|
| **Domain** | The rules of the business, with no technology in them | "A deployment can only succeed after all four stages have run." "A branch name cannot contain spaces." |
| **Application** | The things a user can do, step by step | "To create a deployment: check the application exists, check none is in progress, pick the next number, save it." |
| **Infrastructure** | How the outside world is reached | The PostgreSQL connection, the table definitions, the code that claims a job, the pretend stage runner |
| **Api / Worker** | The two programs that plug the layers together | Web addresses and error formatting (Api); the polling loop (Worker) |

**Why bother.** The inner layers do not know the outer ones exist. The Domain has no idea there is a database. The Application layer says "I need *something* that can run a stage" and "I need *something* that hands me the next job" without caring what. Today those are a pretend runner and a PostgreSQL table. In a later phase they can be Docker and a message queue, and the rules in the middle do not change.

**A naming oddity you will notice.** The class for an application is called `App`, not `Application`. That is because one of the layers is itself named `DevForge.Application`, and the two names clash in C#. The table, the web addresses and everything you see on screen still say "application".

---

## 6. The ideas worth understanding

These are the concepts DevForge exists to teach. Each is small on its own.

### The queue

A queue is a waiting line. DevForge does not use a separate queueing product. **The `deployments` table is the line**: any row whose status is `Queued` is waiting, and the oldest one goes first. It is the simplest thing that works, and the code is arranged so that a real queueing system can replace it later.

### Claiming safely

Suppose three Workers all look at the line in the same instant and all see the same oldest job. Without care, all three would run it.

DevForge prevents this with a PostgreSQL feature. In everyday terms, each Worker says to the database:

> "Give me the oldest waiting job **and lock it for me**. If someone else already has a lock on a job, skip that one and give me the next."

So three Workers looking at the same moment each walk away with a different job, and none of them waits for the others. In SQL this is `FOR UPDATE SKIP LOCKED`; the code is in `PostgresDeploymentQueue.cs`. A test runs twenty Workers against five jobs and checks each job is taken exactly once.

### Status and stage are two different things

- **Status** answers "where is this deployment in its life?" — `Queued`, `Running`, `Succeeded`, `Failed` (and `Cancelled`, reserved for later).
- **Stage** answers "which step of the pipeline is it on?" — `Preparing`, `Building`, `Testing`, `Deploying`.

A deployment that is `Running` is at some stage. A deployment that `Failed` remembers the stage it failed at, which is how the screen can put the red cross on the right step.

### The state machine

A deployment cannot jump around. The allowed moves are fixed:

```text
Queued ──▶ Running ──▶ Succeeded
              │
              └──────▶ Failed
```

The code refuses anything else: you cannot succeed a deployment that never started, skip a stage, or change one that has already finished. These rules live in `Deployment.cs`, so no part of the system can break them by accident.

### An application's status is worked out, not stored

There is no "status" column on an application. It is derived from the latest deployment each time you ask:

| Latest deployment | Application shows |
|---|---|
| none | Never deployed |
| Queued or Running | Deploying |
| Succeeded | Running |
| Failed | Failed |

Storing it separately would create two copies of the truth that could disagree.

### Rules the database enforces

Checking a rule in code is not enough when two requests arrive together: both can pass the check before either saves. So the most important rules are also built into the database:

- **Application names are unique.**
- **Only one deployment per application may be `Queued` or `Running`.** Press Deploy eight times at once and exactly one deployment is created; the other seven get a polite refusal.
- **Status and stage columns only accept known values.** A typo can never be stored.

### Polling

The browser does not get told when something changes. It asks again every two seconds, and stops once the deployment has finished. This is the simplest way to show live progress. A later phase can replace it with a connection where the server pushes updates.

To keep it light, the logs request says "only lines after number 12", so each poll fetches just what is new.

### Migrations

A migration is a recorded change to the database's structure ("create these four tables"). They are files in the code, applied in order, and the database remembers which ones it has had. The benefit: your database structure is versioned like your code, and any machine can be brought to the same structure automatically. The API applies pending migrations when it starts.

### Health checks

Two web addresses exist purely so other software can ask "are you okay?":

- `/health` — "Is the program running?" (liveness)
- `/health/ready` — "Can you actually do your job right now, including reaching the database?" (readiness)

Docker Compose uses the second to decide when the API is ready, and only then starts the Worker and frontend. Kubernetes uses the same two questions, which is why they are separate.

### The proxy (why the browser only talks to one address)

The frontend always calls `/api/...` on *its own* address. Something in front quietly forwards those calls to the API:

- during development, the Angular dev server forwards them (`proxy.conf.json`);
- in Docker, nginx forwards them (`docker/nginx.conf`).

The benefit is that the browser never deals with two different origins, which avoids a whole class of browser security configuration.

### One error format

Whatever goes wrong, the API answers in the same shape: a status number, a short title, and a human-readable detail. Validation problems also list which field is wrong. Internal details and stack traces are never sent to the browser; they go to the API's log. The frontend relies on this to show the message under the right form field or in a banner.

### Configuration lives outside the code

Things that differ between machines — the database address, how often the Worker looks for jobs, how long a pretend stage takes — are settings, not code. They are read from settings files and can be overridden by environment variables. The full table is in the README.

---

## 7. Run it

You need **Docker Desktop running**. Option B also needs the **.NET 10 SDK** and **Node.js 22.12 or newer**.

All commands are run from the `devforge/` folder.

### Before either option, once

```bash
cp .env.example .env
```

This creates your local settings file (database name, user, password, ports). It is already present on the machine this was built on.

### Option A — everything in Docker (simplest)

```bash
docker compose up --build
```

The first run takes a few minutes while images download and build. The command does not return to the prompt: it stays attached and keeps printing the containers' logs. There is no "ready" banner. The stack is up once you see the worker print `Worker … started, polling every 00:00:02`, which happens roughly 20 seconds after the database starts. Then open:

| What | Address |
|---|---|
| **DevForge** | <http://localhost:8080> |
| API directly | <http://localhost:5080/health> |

To stop: press `Ctrl+C`, or run `docker compose down`. Your data is kept. To also erase the data: `docker compose down -v`.

### Option B — run the programs yourself (best for learning)

Here only the database runs in Docker, and you run the three programs in three terminals. You see each program's output separately, and changes to the code take effect when you restart that program.

If the Option A stack is running, free the ports first:

```bash
docker compose stop api worker frontend
```

**Terminal 1 — database and API**

```bash
docker compose up -d postgres
dotnet run --project backend/DevForge.Api
```

Wait for:

```text
Now listening on: http://localhost:5080
```

On the first run it also prints that it applied a database migration. Check it: <http://localhost:5080/health/ready> should say `Healthy`.

**Terminal 2 — Worker**

```bash
dotnet run --project worker/DevForge.Worker
```

Wait for:

```text
Worker <your-machine-name>-xxxxxxxx started, polling every 00:00:02
```

**Terminal 3 — frontend**

```bash
cd frontend/devforge-web
npm install      # first time only
npm start
```

Open <http://localhost:4200>.

### Which ports and why

| Port | Used by | Note |
|---|---|---|
| 8080 | Frontend in Docker | |
| 4200 | Frontend when you run `npm start` | |
| 5080 | API | Not 5000, which macOS uses for AirPlay |
| 5440 | PostgreSQL | Not 5432, to avoid a PostgreSQL already installed on the machine |

All are changeable in `.env`.

---

## 8. A guided tour: ten things to try

Do these in order. Each one shows a specific idea from section 6. Where a command uses the API address, `http://localhost:5080` works in both run options.

**1. See the empty state.**
Open the dashboard on a fresh database. Every number is 0 and the lists say there is nothing yet. Those numbers come from the database, not from the page.

**2. Trip the validation.**
Go to *New application*. Submit the empty form, then try a branch with a space in it, then a repository like `not a url`. Messages appear under the fields without the page reloading.

**3. Create an application.**
Name `Todo API`, repository `github.com/you/todo-api`, branch `main`. Notice that you typed no `https://` and DevForge added it. Try creating a second one with the same name: you get "already exists".

**4. Deploy and watch.**
On the application page press **Deploy**. You land on the deployment page. Watch the steps fill in one by one and the log lines appear. It takes about twelve seconds (four stages of three seconds).

**5. Watch the Worker at the same time.**
Deploy again with the Worker's output visible (Terminal 2, or `docker compose logs -f worker`). You will see it claim the deployment and enter each stage. This is the "cook" working independently of the "waiter".

**6. Make one fail.**
Tick **Simulate failure**, then Deploy. It fails at the Testing step: a red banner, the reason, an error line at the end of the logs, and the Deploying step never starts. The dashboard's "Failed deployments" goes up by one, and the application shows `Failed`.

**7. Stop the cook.**
Stop the Worker (`Ctrl+C` in Terminal 2, or `docker compose stop worker`). Press Deploy. The deployment sits at **Queued** — the API accepted it, but nobody is taking jobs. Start the Worker again and within two seconds it moves. This is the queue doing its job: the work waited safely.

**8. Stop the cook mid-job.**
Deploy, and a few seconds in, stop the Worker. Refresh the deployment page: it is `Failed` with "The worker shut down before the deployment finished." The Worker cleaned up after itself on the way out.

**9. Ask for two deployments at once.**
The Deploy button disables itself while one is in progress, so use the command line. Replace `<id>` with your application's id (it is in the page address):

```bash
curl -X POST http://localhost:5080/api/applications/<id>/deployments
curl -X POST http://localhost:5080/api/applications/<id>/deployments
```

The first prints a deployment. The second prints an error with `"status":409` and "already has a deployment in progress".

**10. Run three cooks.** (Option A only)

```bash
docker compose up -d --scale worker=3
```

Create three applications and deploy them all quickly. They run at the same time, and section 9 shows how to see that three different Workers took them.

**Bonus — change the speed.** In `.env`, set `SIMULATION_STAGE_DELAY=00:00:01`, then `docker compose up -d worker`. Deployments now take four seconds. No code changed; only a setting did.

---

## 9. Looking inside while it runs

### Read the database directly

Open a database prompt:

```bash
docker compose exec postgres psql -U devforge -d devforge
```

Then try (type `\q` to leave):

```sql
-- Every table
\dt

-- Your applications
SELECT name, repository_url, branch, runtime FROM applications;

-- Recent deployments: which worker took each, and how it ended
SELECT version, status, current_stage, worker_id, started_at, completed_at
FROM deployments
ORDER BY created_at DESC
LIMIT 10;

-- The log lines of the most recent deployment
SELECT id, stage, level, message
FROM deployment_logs
WHERE deployment_id = (SELECT id FROM deployments ORDER BY created_at DESC LIMIT 1)
ORDER BY id;

-- The waiting line right now
SELECT version, created_at FROM deployments WHERE status = 'Queued' ORDER BY created_at;
```

Run the second-to-last query repeatedly during a deployment and you will see rows being added. That is exactly what the frontend is reading, two seconds at a time.

### Read each program's output

| Run option | Command |
|---|---|
| A (Docker) | `docker compose logs -f api` / `worker` / `frontend` / `postgres` |
| B (local) | Look at the terminal the program is running in |

In Docker the API and Worker print one JSON object per line, which is the format log-collecting tools expect. Run locally, they print short readable lines.

### Ask the API yourself

```bash
curl http://localhost:5080/api/applications
curl http://localhost:5080/api/dashboard/summary
curl http://localhost:5080/api/platform
```

When the API runs locally (Option B), a machine-readable description of every endpoint is at <http://localhost:5080/openapi/v1.json>.

### See which containers are up

```bash
docker compose ps
```

---

## 10. When something goes wrong

| What you see | Likely cause | What to do |
|---|---|---|
| `Cannot connect to the Docker daemon` | Docker Desktop is not running | Start Docker Desktop and wait for it to finish starting |
| `no configuration file provided: not found` | The command was run outside the `devforge/` folder | `cd` into `devforge/` (the folder containing `docker-compose.yml`) and run it again |
| `docker compose up` prints logs and never returns to the prompt | Normal. It stays attached and shows every container's output | Leave it running and open <http://localhost:8080>. Use `docker compose up --build -d` to get the prompt back |
| On first start, postgres prints `ERROR: relation "__EFMigrationsHistory" does not exist` | Harmless. On an empty database the API looks for its migration record before creating it | Nothing. It appears once per fresh database |
| `port is already allocated` / `address already in use` | Something else is using 8080, 5080 or 5440 — often the Docker stack still running while you try Option B | `docker compose stop api worker frontend`, or change the port in `.env` |
| `Set POSTGRES_DB in .env` | No `.env` file | `cp .env.example .env` |
| API exits with "Connection string 'DevForge' is not configured" | The API was started outside Development mode without a connection string | Start it with `dotnet run --project backend/DevForge.Api`, or set `ConnectionStrings__DevForge` |
| API fails at startup with a connection error | The database container is not running | `docker compose up -d postgres`, then `docker compose ps` |
| Worker prints "could not reach the deployment queue … retrying" | Database not up yet, or the API has not created the tables yet | Start the database and the API; the Worker recovers by itself |
| A deployment stays **Queued** forever | No Worker is running | Start the Worker |
| A deployment stays **Running** forever, and Deploy says one is "in progress" | The Worker was killed abruptly mid-deployment (not stopped normally) | Known Phase 1 limitation. Fix the row: `UPDATE deployments SET status='Failed', error_message='Worker crashed', completed_at=now() WHERE status='Running';` |
| The page shows an error box with **Retry** | The API is not reachable from the frontend | Check the API is running, then press Retry |
| `npm install` fails with `Cannot read properties of null (reading 'edgesOut')` | An npm bug triggered by a newer `vitest` | Leave `vitest` pinned at `~4.0.8` in `package.json` (it already is) |
| No "Simulate failure" tick box | The feature is switched off for that environment | It is on in Development and in Compose; see `Features:FailureSimulation` |
| You want a clean slate | | `docker compose down -v`, then start again |

---

## 11. What is real and what is pretend

| Real in Phase 1 | Pretend in Phase 1 |
|---|---|
| Applications are saved, validated, edited and deleted | The repository is never contacted or downloaded |
| Deployments are queued and claimed by a separate program | Nothing is compiled |
| Every status and stage change is saved as it happens | No tests are run |
| Log lines are saved one by one and shown live | Nothing is deployed anywhere |
| Failure is recorded with its reason and stage | The log text is a fixed script |
| Multiple Workers share the work safely | A stage's "work" is a timed wait |
| The dashboard counts real rows | The "failure" is requested by you, not discovered |

The pretend part is confined to a single file, `SimulatedStageExecutor.cs`. Phase 2 replaces that file's job with real cloning and building. Everything in the left column stays as it is.

Not present at all yet: logins, cancelling or retrying a deployment, rollbacks, GitHub integration, Kubernetes.

---

## 12. Where to find things in the code

| If you want to see… | Open |
|---|---|
| The rules of a deployment's life | `backend/DevForge.Domain/Deployments/Deployment.cs` |
| The order of the stages | `backend/DevForge.Domain/Deployments/DeploymentStage.cs` |
| The rules for an application and its repository address | `backend/DevForge.Domain/Applications/App.cs`, `RepositoryUrl.cs` |
| The list of supported runtimes | `backend/DevForge.Domain/Applications/RuntimeCatalog.cs` |
| What happens when you press Deploy (API side) | `backend/DevForge.Application/Deployments/DeploymentService.cs` |
| How a deployment is driven through its stages | `backend/DevForge.Application/Pipeline/DeploymentRunner.cs` |
| How status becomes the list of steps on screen | `backend/DevForge.Application/Deployments/PipelineView.cs` |
| How the dashboard numbers are counted | `backend/DevForge.Application/Dashboard/DashboardService.cs` |
| How a job is claimed safely | `backend/DevForge.Infrastructure/Queue/PostgresDeploymentQueue.cs` |
| The pretend stage work and its log lines | `backend/DevForge.Infrastructure/Execution/SimulatedStageExecutor.cs` |
| The table definitions and indexes | `backend/DevForge.Infrastructure/Persistence/Configurations/` |
| The web addresses | `backend/DevForge.Api/Controllers/` |
| How errors are turned into answers | `backend/DevForge.Api/ErrorHandling/GlobalExceptionHandler.cs` |
| How the API is assembled | `backend/DevForge.Api/Program.cs` |
| The Worker's loop | `worker/DevForge.Worker/DeploymentWorker.cs` |
| How the frontend talks to the API | `frontend/devforge-web/src/app/core/services/` |
| The live-updating logic of the deployment page | `frontend/devforge-web/src/app/features/deployments/deployment-tracker.ts` |
| The "ask again every two seconds" helper | `frontend/devforge-web/src/app/core/utils/poll-while.ts` |
| How the containers are wired together | `docker-compose.yml`, `docker/` |

A good reading order for a first pass: `Deployment.cs` → `DeploymentService.cs` → `PostgresDeploymentQueue.cs` → `DeploymentWorker.cs` → `DeploymentRunner.cs` → `SimulatedStageExecutor.cs`. That is the Deploy click from start to finish.

---

## 13. Glossary

| Term | Meaning here |
|---|---|
| **API** | The program the frontend sends requests to. It answers with data. |
| **Application** | Something you registered to deploy: a name, a repository, a branch. |
| **Background service** | A program with no screen that runs continuously doing work. The Worker is one. |
| **Build** | The step that turns source code into something runnable. Recorded as its own row. |
| **Claim** | A Worker taking a waiting deployment so that no other Worker can. |
| **Container** | A packaged program with everything it needs, run by Docker in isolation. |
| **Deployment** | One attempt to release an application. One press of the Deploy button. |
| **Docker Compose** | The tool that starts several containers together from one file. |
| **Endpoint** | One web address the API answers, such as `/api/applications`. |
| **Environment variable** | A setting given to a program from outside, without changing its code. |
| **Frontend** | The part that runs in your browser. |
| **Health check** | An address a program exposes so other software can ask if it is okay. |
| **Migration** | A recorded, repeatable change to the database's structure. |
| **Pipeline** | The fixed series of stages a deployment goes through. |
| **Polling** | Asking repeatedly at an interval, rather than being notified. |
| **Proxy** | Something that receives a request and passes it on to another program. |
| **Queue** | A waiting line of work. Here, deployments whose status is `Queued`. |
| **Runtime** | The technology an application runs on. Only `.NET 10` for now. |
| **Stage** | One step of the pipeline: Preparing, Building, Testing or Deploying. |
| **Status** | Where a deployment is in its life: Queued, Running, Succeeded, Failed. |
| **Worker** | The program that takes queued deployments and runs them. |
