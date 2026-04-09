---
id: versioning-docs
title: Docs Versioning Workflow
---

# Docs Versioning Workflow

This project uses Docusaurus versioned docs so contributors can keep `current` documentation accurate while maintainers preserve release-specific snapshots.

## Core rule

Contributors should update documentation in the same pull request as the feature or behavior change.

That means:

- Add or update the relevant page under `docs/`
- Treat `docs/` as the source of truth for the next release
- Do not create a new docs version in feature PRs

## What contributors do

When a pull request changes behavior, adds a feature, or changes recommended usage:

1. Update the relevant page in `docs/`
2. If no page exists, create one and add it to `sidebars.js`
3. Keep examples aligned with the code being merged
4. Mention the docs change in the pull request summary when useful

Contributors are writing docs for the unreleased `current` version of the product.

## What maintainers do at release time

The maintainer cutting the release snapshots the current docs into a new version.

Typical release flow:

1. Ensure the release-ready docs are already merged in `docs/`
2. Create the docs snapshot for the release line:

```bash
npm install
npm run version-docs -- 4.2
```

3. Commit the generated versioned docs files
4. Tag and publish the GitHub release
5. Continue editing `docs/` for unreleased work after the release

## Version naming

Prefer minor release versions such as:

- `4.1`
- `4.2`
- `5.0`

Avoid creating a new docs version for every patch release unless the docs for that patch need to be preserved exactly.

## When to create a new docs version

Create a new version when:

- a new minor release ships with user-visible changes
- a new major release ships
- the release includes docs-worthy feature changes that users may need to browse by version

Usually do not create a new version for:

- every pull request
- every patch release
- internal-only changes with no docs impact

## Release notes vs docs

GitHub release notes and docs serve different purposes:

- Release notes summarize what changed in a release
- Docs explain how the feature works and how to use it

Release notes can point into the docs site, but they should not replace contributor-written documentation in `docs/`.

## Local docs commands

Run the docs site locally:

```bash
npm install
npm start
```

Build the static site:

```bash
npm run build
```

Create a new version snapshot:

```bash
npm run version-docs -- 4.2
```
