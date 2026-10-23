# Pull request CI

The local workflow `workflows/pr-build-test.yml` restores and builds the application and runs the
existing regression suites on GitHub-hosted Windows 2025. The fork's PR #8 run passed on
20 October 2026. Logs and test results are retained for seven days.
Official checkout, setup-dotnet, and upload-artifact actions are pinned to immutable commit hashes.

It uses only `pull_request`, `contents: read`, and checkout with persisted credentials disabled.
There is no publishing, deployment, signing, secret input, or privileged self-hosted runner.
Interactive desktop testing is separate; passing these jobs does not certify a user's full window,
keyboard, screen reader, or hardware.

The engine console runner runs on both net48 and net10.0. The speed-test console runner runs on
net48 with local fixtures only, without --worker or --internet. They use dotnet run because they
are executable regression suites. Isolated WPF checks are distinct from interactive UI automation.
The build SDK is .NET 10; the application and desktop runner continue targeting net48.

## Release-channel verification

`workflows/release-sync.yml` checks the upstream publisher's GitHub release, Chocolatey and
WinGet channels. Its `verify` job runs only in `SteveTheKiller/KillerScan`, for both scheduled
and manual invocations. Development forks intentionally skip this job, even if they have
their own releases: the configured marketplace identifiers belong to the upstream publisher.
This does not disable `pr-build-test.yml` or change any publishing workflow.

On 23 October 2026, investigation found eleven scheduled failures in the fork after the
upstream sync: the fork has no published releases, so `/releases/latest` returned HTTP 404.
The script previously continued to an empty release tag and obscured the original error.
The job guard prevents the inapplicable fork check; failed release API calls now stop with
an explicit metadata-fetch error. Missing releases in the publisher's repository remain failures.

Validation: actionlint and PowerShell syntax checks, plus isolated release-lookup cases
(latest, explicit version, API failure and invalid tag). The patched workflow must reach
the fork's default branch before scheduled runs use the guard. Old failed runs remain in
history; rerunning their old revisions does not test this fix. No package was published.
