# Feature: real deployments (Docker mode)

In Phase 1 a deployment was pretend: the worker waited a few seconds per stage and wrote scripted log lines. This feature makes it real. With the worker in **Docker mode**, pressing Deploy downloads your repository, builds a Docker image from it, runs its tests, and starts the application as a container you can open in your browser.

This document explains what happens, where things are put on your machine, and where each piece lives in the code. It assumes you have read [How DevForge works](HOW-IT-WORKS.md).

## Contents

1. [The short answers](#1-the-short-answers)
2. [What changed from Phase 1](#2-what-changed-from-phase-1)
3. [Turning it on](#3-turning-it-on)
4. [What your repository needs](#4-what-your-repository-needs)
5. [A deployment, step by step](#5-a-deployment-step-by-step)
6. [Where everything ends up](#6-where-everything-ends-up)
7. [See it for yourself](#7-see-it-for-yourself)
8. [When a deployment fails](#8-when-a-deployment-fails)
9. [Settings](#9-settings)
10. [How the code is organised](#10-how-the-code-is-organised)
11. [Safety: what this is and is not](#11-safety-what-this-is-and-is-not)
12. [What is not built yet](#12-what-is-not-built-yet)

---

## 1. The short answers

**Where does it download my repository?**

Into a folder on the machine where the worker runs:

```text
<system temp folder>/devforge/workspaces/<deployment id>/
```

On this Mac the system temp folder is `/var/folders/zv/…/T/` (run `echo $TMPDIR` to see yours), so one deployment's source is at, for example:

```text
/var/folders/zv/…/T/devforge/workspaces/01a0fde3dbc17a55ac…/
```

Every deployment gets its own folder, named after the deployment's id with the dashes removed. Only the latest commit of the branch you chose is downloaded, not the whole history. The exact path is printed in the deployment's log, on the `git clone` line.

**How does it deploy my application?**

It builds a Docker image from the `Dockerfile` in your repository, then starts that image as a container on your machine:

```text
your repo ──git clone──▶ folder ──docker build──▶ image ──docker run──▶ container ──▶ http://localhost:<port>
```

Docker picks a free port. DevForge waits until the application answers, then removes the container of the previous version. The address is shown on the application page as **Open application**.

"Deployed" in Phase 2 therefore means: **running as a Docker container on this machine, reachable only from this machine.**

---

## 2. What changed from Phase 1

The waiting line, the worker loop, the API and the pages are the same. One thing was swapped: the class that does the work of each stage.

| Stage | Simulated mode (Phase 1) | Docker mode (this feature) |
|---|---|---|
| Preparing | Waits | Checks the tools, runs `git clone`, records the commit |
| Building | Waits | Runs `docker build`, records the image |
| Testing | Waits | Runs your tests inside Docker |
| Deploying | Waits | Starts the container, health-checks it, replaces the old one |

Three facts are now recorded for a deployment that were empty before, and shown on its page:

- **Commit** — the exact commit that was built.
- **Image** — the Docker image that was produced.
- **URL** — where the running application can be opened.

Simulated mode still exists and is still the default. It is what the automated tests and the Compose stack use.

---

## 3. Turning it on

Docker mode needs `git` and Docker on the machine where the worker runs. The worker *container* has neither, so run the worker directly on your machine:

```bash
# 1. The rest of the stack (database, API, pages)
docker compose up -d

# 2. Stop the simulated worker, or it will take your deployments and pretend
docker compose stop worker

# 3. Run a real worker
Execution__Mode=Docker Execution__HealthCheckPath=/health dotnet run --project worker/DevForge.Worker
```

The worker confirms the mode when it starts:

```text
Worker Vijays-Mac-mini-401c2ef1 started in Docker mode, polling every 00:00:02
```

To go back: stop it with `Ctrl+C` and run `docker compose start worker`.

`Execution__HealthCheckPath=/health` is optional. Without it the health check requests `/`.

---

## 4. What your repository needs

| Requirement | Why | If it is missing |
|---|---|---|
| A **public** `http(s)` Git URL | DevForge has no way to supply a password or token yet | Preparing fails: "Could not clone …" |
| A `Dockerfile` at the top level | It is the recipe for building the application. DevForge does not guess how | Building fails: "The repository has no Dockerfile at its root" |
| An `EXPOSE <port>` line in the Dockerfile | This is how DevForge learns which port the application listens on | Deploying fails: "Add an EXPOSE instruction" |
| A web server that answers HTTP on that port | The health check is an HTTP request | Deploying fails after the timeout |
| *Optional:* a Dockerfile stage named `test` | Building that stage is how tests are run | A warning is logged and no tests run |

The `devforge-sample` repository meets all of these. Its Dockerfile is worth reading alongside this document:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build      # restore + compile
...
FROM build AS test                                   # ← the stage DevForge runs for Testing
RUN dotnet test --configuration Release --no-build

FROM build AS publish
...
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime # ← the image that gets deployed
EXPOSE 8080                                          # ← how DevForge finds the port
```

Note that restore, compile, test and publish all happen **inside Docker**. The worker never runs `dotnet` itself, so the worker's machine does not need the .NET SDK to deploy a .NET application.

---

## 5. A deployment, step by step

This follows one real deployment of `devforge-sample`: application "Sample API", version `v2`. Lines starting with `$` are the commands DevForge ran, exactly as they appear in the deployment log.

### Before the stages

You press Deploy. The API saves a `Queued` deployment and returns. The worker claims it within two seconds and marks it `Running`. This part is unchanged from Phase 1.

### Stage 1 — Preparing: get the source

**1. Check the tools.**

```text
$ git --version
git version 2.47.0
$ docker version --format Docker {{.Server.Version}}
Docker 23.0.5
```

If `git` is not installed, or Docker is installed but not running, the deployment fails here with a message saying exactly that, rather than with a confusing error later.

**2. Empty the workspace folder** for this deployment, in case an earlier interrupted run left something there.

**3. Download the repository.**

```text
$ git clone --depth 1 --single-branch --branch=main -- https://github.com/vjjangid/devforge-sample <workspace>
```

| Part | Meaning |
|---|---|
| `--depth 1` | Only the latest commit. A build does not need history, and this is much faster |
| `--single-branch --branch=main` | Only the branch configured on the application |
| `--` | Everything after this is a value, never an option. A safety measure for user-supplied text |
| `<workspace>` | The folder from section 1 |

Git is told not to ask questions (`GIT_TERMINAL_PROMPT=0`). Without that, a private or mistyped repository would make git wait for a username forever; with it, the clone fails within seconds.

**4. Read which commit was downloaded.**

```text
$ git -C <workspace> rev-parse HEAD
c2a3a06e029c5462735fc892c3d203bebbd6d67b
Checked out commit c2a3a06
```

**Saved:** the commit id, on the deployment.

### Stage 2 — Building: make the image

**1. Check a `Dockerfile` exists** at the top of the workspace.

**2. Work out the image name.**

```text
devforge/sample-api-da6e3fe4:v2
   │         │         │      └─ the deployment's version
   │         │         └─ last 8 characters of the application's id
   │         └─ the application's name, made safe for Docker ("Sample API" → "sample-api")
   └─ everything DevForge builds is under "devforge/"
```

The id fragment is there so that two applications with similar names ("Todo API" and "todo-api") cannot overwrite each other's images. The version is the tag, so every deployment gets its own image and `latest` is never used.

**3. Build it.**

```text
$ docker build --progress plain --tag devforge/sample-api-da6e3fe4:v2
    --label devforge.application-id=… --label devforge.deployment-id=… --label devforge.version=v2
    --file <workspace>/Dockerfile <workspace>
```

Every line Docker prints goes into the deployment log as it appears. `--progress plain` makes Docker print one readable line per step.

The three **labels** are name tags stuck on the image. They let DevForge (and you) later find everything belonging to an application without keeping a separate list.

**Saved:** the image name, on the deployment's build record. A `Build` row is created when this stage starts and marked `Succeeded` or `Failed` when it ends.

### Stage 3 — Testing: run the tests

**1. Look in the Dockerfile for a stage named `test`** (a line like `FROM build AS test`).

- **Not there:** a warning is logged — "no tests were run" — and the pipeline continues.
- **There:** it is built.

```text
$ docker build --progress plain --target test --file <workspace>/Dockerfile <workspace>
...
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6
Tests passed
```

`--target test` means "build up to the stage called `test` and stop". That stage runs `dotnet test`. If a test fails, `dotnet test` reports an error, the Docker build fails, and so does the stage. Because the build stage ran a moment ago, Docker reuses its work and only the test step actually executes.

Why this is a separate command: a normal `docker build` skips the `test` stage entirely, because the final image does not depend on it.

**Saved:** nothing. The stage either passes or stops the pipeline.

### Stage 4 — Deploying: start the application

This is the part that makes your application reachable. The order matters: the new version is proven to work **before** the old one is touched.

**1. Find the port the application listens on**, by reading the image's `EXPOSE`:

```text
The image listens on port 8080
```

**2. Start a container.**

```text
$ docker run --detach --name devforge-sample-api-da6e3fe4-v2 --restart unless-stopped
    --publish 127.0.0.1::8080 --env APP_VERSION=v2
    --label devforge.application-id=… --label devforge.deployment-id=… --label devforge.version=v2
    devforge/sample-api-da6e3fe4:v2
```

| Part | Meaning |
|---|---|
| `--detach` | Run in the background |
| `--name …-v2` | The version is in the name, so v2 can start while v1 is still running |
| `--restart unless-stopped` | Come back after Docker or the machine restarts, unless someone stopped it on purpose |
| `--publish 127.0.0.1::8080` | Connect the container's port 8080 to **a free port chosen by Docker**, on this machine only |
| `--env APP_VERSION=v2` | Tells the application which version it is. The sample shows it on its home page |

Letting Docker choose the port means two applications can never fight over the same one.

**3. Ask Docker which port it chose**, and build the address:

```text
http://localhost:62101
```

**4. Wait for the application to answer.** Once a second, DevForge sends an HTTP request to the health check path:

```text
Waiting for http://localhost:62101/health to respond
The application is responding
```

Any answer with a status below 500 counts as "up". Between attempts it also checks the container is still running, so a crashed application fails immediately instead of after a minute.

**5. Remove the previous version's container.** DevForge lists the containers carrying this application's label and removes all except the new one:

```text
Removing previous container 32b8f660dec1
```

**6. Done.**

```text
v2 is live at http://localhost:62101/
Deployment completed successfully
```

**Saved:** the address, on the deployment. The application page reads it from the most recent *successful* deployment and shows it as **Open application**.

### The same thing as a picture

```text
              Preparing              Building             Testing              Deploying
            ┌────────────┐       ┌──────────────┐     ┌──────────────┐     ┌──────────────────────┐
 GitHub ──▶ │ git clone  │ ────▶ │ docker build │ ──▶ │ docker build │ ──▶ │ docker run           │
            │            │       │              │     │ --target test│     │ wait for /health     │
            └─────┬──────┘       └──────┬───────┘     └──────────────┘     │ remove old container │
                  │                     │                                  └──────────┬───────────┘
                  ▼                     ▼                                             ▼
        workspace folder          image :v2                              container on localhost:<port>
        + commit id saved         + image name saved                     + address saved
```

---

## 6. Where everything ends up

| Thing | Where | How long it stays |
|---|---|---|
| Downloaded source | `<temp>/devforge/workspaces/<deployment id>/` on the worker's machine | Until you delete it. Nothing cleans it up yet |
| Docker image | Your local Docker, named `devforge/<app>-<id>:<version>` | Until you delete it. One per deployment |
| Running container | Your local Docker, named `devforge-<app>-<id>-<version>` | Until the next successful deployment replaces it |
| Commit id, address, status, timings | `deployments` table | Permanently |
| Image name | `builds` table (`artifact_reference`) | Permanently |
| Every output line | `deployment_logs` table | Permanently |

Nothing is uploaded anywhere. There is no registry and no server involved: the image and the container exist only in the Docker on your machine.

---

## 7. See it for yourself

After a Docker-mode deployment, each of these shows one piece of the above.

```bash
# The downloaded source of every deployment
ls $TMPDIR/devforge/workspaces/

# Every image DevForge has built
docker images --filter label=devforge.version

# Every application container DevForge is running, with its port
docker ps --filter label=devforge.version

# What one application is printing
docker logs devforge-sample-api-da6e3fe4-v3

# The labels on a container
docker inspect --format '{{json .Config.Labels}}' devforge-sample-api-da6e3fe4-v3

# Talk to the deployed application (use the port shown by `docker ps`)
curl http://localhost:62133/
curl http://localhost:62133/health
```

And in the database:

```bash
docker compose exec postgres psql -U devforge -d devforge
```

```sql
SELECT d.version, d.status, left(d.commit_sha, 7) AS commit, b.artifact_reference AS image, d.url
FROM deployments d
LEFT JOIN builds b ON b.id = d.build_id
ORDER BY d.created_at DESC
LIMIT 5;
```

A good experiment: deploy the sample twice and run `docker ps --filter label=devforge.version` during the second deployment. For a moment you will see **two** containers, the old one still serving and the new one being checked. Then only the new one.

---

## 8. When a deployment fails

Whatever fails, the deployment ends as `Failed`, the pipeline shows a red cross on the stage that failed, the reason is saved as the error message, and the tool's own output is in the log.

| What went wrong | Fails at | What you see | What is left behind |
|---|---|---|---|
| `git` or Docker missing, or Docker not running | Preparing | "Could not start 'docker'…" or "Is the Docker daemon running?" | Nothing |
| Repository private, misspelled, or branch missing | Preparing | "Could not clone … at branch '…'", with git's own explanation in the log | Nothing |
| No `Dockerfile` | Building | "The repository has no Dockerfile at its root" | The source folder |
| The code does not compile | Building | "The image could not be built", with the compiler errors in the log | The source folder |
| A test fails | Testing | "The tests failed", with the failing test named in the log | The source folder and the image |
| No `EXPOSE` in the Dockerfile | Deploying | "Add an EXPOSE instruction" | The source folder and the image |
| The application crashes on start | Deploying | "The application stopped right after starting", plus its last 50 lines of output | The image. **The new container is removed** |
| The application never answers | Deploying | "did not respond … within 60 seconds" plus its last 50 lines of output | The image. **The new container is removed** |
| A command runs longer than the timeout | Whichever stage | "'docker' did not finish within 00:10:00 and was stopped" | Depends on the stage |

The two rows in bold are the important design point: **a failed deployment never takes your application down.** The previous version's container is only removed after the new one has answered, so if the new one is broken, the old one keeps serving. The application page then shows status `Failed` (the latest deployment failed) while **Open application** still points at the version that is actually running.

---

## 9. Settings

All of these are worker settings. Set them as environment variables with `__` in place of `:` (for example `Execution__Mode=Docker`), or in `worker/DevForge.Worker/appsettings.json`.

| Setting | Default | Meaning |
|---|---|---|
| `Execution:Mode` | `Simulated` | `Simulated` or `Docker` |
| `Execution:WorkspaceRoot` | `<temp>/devforge/workspaces` | Where repositories are downloaded |
| `Execution:CommandTimeout` | `00:10:00` | Longest a single clone, build or test run may take |
| `Execution:HealthCheckPath` | `/` | Path requested to decide the application is up |
| `Execution:HealthCheckTimeout` | `00:01:00` | How long a new container has to start answering |
| `Execution:HealthCheckInterval` | `00:00:01` | Pause between health check attempts |

To keep the downloaded source somewhere easier to look at:

```bash
Execution__Mode=Docker Execution__WorkspaceRoot=$HOME/devforge-workspaces dotnet run --project worker/DevForge.Worker
```

---

## 10. How the code is organised

Everything new is in `backend/DevForge.Infrastructure/Execution/`.

```text
DeploymentRunner            (unchanged from Phase 1: walks the stages, saves every change)
      │  "do the work of stage X"
      ▼
DockerStageExecutor         Preparing, Building, Testing
      │                 ╲
      │                  ╲──▶ ContainerDeployer      Deploying
      ▼                              │
StageCommands  ◀─────────────────────┘     runs a command, copies its output to the deployment log
      │
      ▼
ProcessRunner               starts a program, reads its output line by line, enforces the timeout
      │
      ▼
   git, docker
```

| File | What it is responsible for |
|---|---|
| `ExecutionOptions.cs` | The settings above, and `WorkspacePathFor(deploymentId)`: the one place that decides where source is downloaded |
| `DockerStageExecutor.cs` | The tool check, `git clone`, `docker build`, and the test stage |
| `ContainerDeployer.cs` | Finding the port, `docker run`, the health-check loop, removing the old container, cleaning up on failure |
| `StageCommands.cs` | Running one command for a stage: echo it, stream the output to the log, turn a failure into a failed stage |
| `ImageNaming.cs` | Turning an application name and version into an image name and a container name |
| `DevForgeLabels.cs` | The label names, and the filter used to find an application's containers |
| `IHealthProbe.cs`, `HttpHealthProbe.cs` | "Is something answering HTTP at this address?" |
| `Processes/ProcessRunner.cs` | Starting an external program safely: no shell, a required timeout, the whole process tree killed on cancel |
| `SimulatedStageExecutor.cs` | The Phase 1 pretend executor, still used in Simulated mode |

Elsewhere:

| File | Change |
|---|---|
| `DevForge.Infrastructure/DependencyInjection.cs` | `AddStageExecution` picks the executor from `Execution:Mode` when the worker starts |
| `DevForge.Application/Pipeline/StageOutcome.cs` | What a stage hands back: commit id, image name, address |
| `DevForge.Application/Pipeline/DeploymentRunner.cs` | Saves those three onto the deployment |
| `DevForge.Domain/Deployments/Deployment.cs` | `RecordCommit` and `RecordUrl` |
| `Persistence/Migrations/…_AddDeploymentUrl.cs` | Adds the `url` column |

A good reading order: `DockerStageExecutor.cs` top to bottom, then `ContainerDeployer.cs`. Together they are the whole of section 5.

### How it is tested

| Kind | What it uses | Example |
|---|---|---|
| Fake git and Docker | A scripted stand-in that records the commands | "the old container is removed only after the new one responds" |
| Real git, no network | A small repository created on disk | "clones the requested branch and reports its commit" |
| Real Docker (opt-in) | Your Docker; run with `DEVFORGE_DOCKER_TESTS=1` | "builds a labelled image from the workspace" |
| Whole worker path | A real database, fake tools | "a failed redeployment leaves the previous version as the live one" |

---

## 11. Safety: what this is and is not

**Phase 2 is for a trusted local development machine. It is not a secure build service.**

What is protected against:

- **Command injection.** Commands are never passed through a shell. A repository address or branch name is handed to `git` as a single value and cannot smuggle in extra commands. Branch names starting with `-` are refused, and `--` separates options from values.
- **Path tricks.** The download folder is built from the deployment's id, never from anything a user typed.
- **Exposure to the network.** Deployed applications are published on `127.0.0.1` only.

What is **not** protected against:

- **The repository's own code.** A `Dockerfile` can run any command during the build, and the built application runs as a container on your machine. Deploying a repository means running its code. Only deploy repositories you trust.
- **Resource use.** A build may use as much disk, memory and CPU as it wants, up to the timeout.

No secrets are involved yet: only public repositories are supported, and no environment variables other than `APP_VERSION` are passed to the application.

---

## 12. What is not built yet

| Gap | What it means today |
|---|---|
| No cleanup of source folders | Each deployment leaves its download in the temp folder. Delete `…/devforge/workspaces/` by hand |
| No cleanup of old images | One image per deployment accumulates. Remove with `docker image rm` |
| Deleting an application does not stop its container | It keeps running. Stop it with `docker rm -f $(docker ps -aq --filter label=devforge.application-id=<id>)` |
| No cancel | A running deployment can only finish, fail, or hit its timeout |
| The worker container cannot use Docker mode | It has no `git` or Docker inside, so the real worker must be run with `dotnet run` |
| One database write per log line | Fine for the sample's ~150 lines; a build printing thousands will be slow |
| "Simulate failure" is ignored in Docker mode | To see a real failure, deploy a branch with a broken test |
| One timeout for clone, build and test | Not configurable per stage |
| A crashed worker strands its deployment | Unchanged from Phase 1 |
