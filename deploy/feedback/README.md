# The reports from the app

The Help menu's "Report an issue" posts to `https://lapin.ciuntucbimstudio.ro/api/feedback`. The desktop app posts through its engine (`engine/src/Host/Lapin.Host/Support.cs`); the browser build posts from the page. The `lapin-feedback` container on the server keeps each report as a file, `/opt/lapin/feedback-data/open/<n>.json`, with its screenshot beside it. Closed reports go under `closed/`. Nothing goes to GitHub.

A sender who ticks "I'd like a reply" can be answered: the reply appears in their Help → Messages (decision 177). Anonymous reports stay as they were.

```sh
./reports.sh              # the open ones (✉ <nickname>: replies accepted; ●: an unread answer from the sender)
./reports.sh list all     # all of them
./reports.sh show 18      # the whole report and its thread; its screenshot lands in feedback-screenshots/
./reports.sh reply 18 "Fixed in the next update."
./reports.sh close 18     # or reopen 18
./reports.sh prune        # anonymise threads idle for more than 12 months
```

The server is named in `.env` (see `.env.example`, git-ignored). The service itself (`server/`: the server, the CLI, their tests, the API, the storage format, the nginx locations and how to deploy) is maintained here, in [`server/README.md`](server/README.md).
