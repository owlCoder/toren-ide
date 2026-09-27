# Security Policy

## Supported versions

Toren IDE is currently in pre-release development. Security fixes are applied to the actively developed default branch and to supported releases once public releases begin.

## Reporting a vulnerability

Please do not open a public issue for a vulnerability that could put users, source code, credentials, signing material, private package feeds, or local development environments at risk.

Until a dedicated private reporting channel is configured, contact the repository owner privately through an available GitHub contact method and include:

- affected component/version or commit;
- reproduction steps;
- expected and observed behavior;
- potential impact;
- any proposed mitigation.

A dedicated GitHub private vulnerability reporting workflow should be enabled before the first public release.

## Security principles

Toren is local-first and should minimize unnecessary network access. Features that execute project code, invoke external tools, read credentials, interact with Git/NuGet feeds, or launch processes must treat repository contents as potentially untrusted.

Do not commit secrets, signing certificates, access tokens, private package credentials, or user-specific environment files to this repository.
