# The reports from the app

The Help menu's "Report an issue" posts to `https://lapin.ciuntucbimstudio.ro/api/feedback` (`engine/src/Host/Lapin.Host/Support.cs`). The `lapin-feedback` container on the server keeps each report as a file, `/opt/lapin/feedback-data/open/<n>.json` with its screenshot beside it, closed ones under `closed/`. Nothing goes to GitHub.

```sh
./reports.sh              # the open ones
./reports.sh list all     # all of them
./reports.sh show 18      # the whole report; its screenshot lands in feedback-screenshots/
./reports.sh close 18     # or reopen 18
```

The server is named in `.env` (see `.env.example`, git-ignored). The container itself (its compose file and nginx) lives with the old Lapin's deployment for now.
