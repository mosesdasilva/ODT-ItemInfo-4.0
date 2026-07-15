# Issue tracker: GitHub

Issues and specifications for this repository live as GitHub issues in `mosesdasilva/ODT-ItemInfo-4.0`. Use the GitHub connector when available and the `gh` CLI for unsupported operations.

## Conventions

- Create specifications and work tickets as GitHub issues.
- Read the full issue body, labels, and comments before acting.
- Apply and remove labels using the vocabulary in `triage-labels.md`.
- Close an issue only when its acceptance criteria have been satisfied or the maintainer explicitly declines it.
- Infer the repository from the local `origin` remote when operating inside the clone.

## Pull requests as a triage surface

External pull requests are not a request surface. Issues hold planned work; pull requests are implementation submissions.

## Skill publication rule

When an engineering skill says to publish to the issue tracker, create a GitHub issue in this repository.

## Wayfinding operations

- A Wayfinder map is a GitHub issue labelled `wayfinder:map`.
- Map tickets use exactly one type label: `wayfinder:research`, `wayfinder:prototype`, `wayfinder:grilling`, or `wayfinder:task`.
- Attach tickets to their map with GitHub's native sub-issue relationship.
- Express ordering with GitHub's native issue dependency relationship; the dependent issue is "blocked by" the prerequisite issue.
- Claim a ticket by assigning it to the developer driving the session before beginning work.
- The frontier is the map's open, unassigned child issues that have no open `blocked by` dependencies.
- Use the GitHub connector for supported issue operations and the `gh api` REST endpoints for native sub-issue and issue-dependency relationships.
