# Sandy Rails server

The Rails server is Sandy's control plane. It provides the parent web app, the
JSON API used by Windows agents, and the Action Cable endpoint used for live
updates. It stores families, parent profiles, devices, time grants, audit
events, and timer state in SQLite.

## Requirements

- Ruby version from [`.ruby-version`](.ruby-version)
- SQLite
- Bundler

## Setup

From this directory, install dependencies and prepare the database:

```sh
bin/setup
```

Start the development server:

```sh
bin/rails server
```

Open `http://127.0.0.1:3000` in a browser. Use `bin/demo` for a local demo with
sample data and an enrolled mock agent. The demo credentials are
`demo@sandy.test` and `password`. Do not use them outside the local demo.

## Tests and checks

Run the Rails test suite:

```sh
bin/rails test
```

Run the project checks:

```sh
bin/ci
```

## Configuration

Local development uses SQLite under `storage/`. Production uses the
configuration in [`../deploy/`](../deploy/) and the `sandy_storage` Docker
volume. See [`../docs/deployment.md`](../docs/deployment.md) for installation
and upgrade steps.

Do not commit `config/master.key`, `config/credentials/*.yml.enc`, deployment
environment files, join codes, or device tokens.

The server exposes `GET /up` as its health check. See
[`../docs/protocol.md`](../docs/protocol.md) for the device API and WebSocket
contract.
