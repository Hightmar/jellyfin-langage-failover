# Security

## Reporting a vulnerability

Please don't open a public issue. Use **Report a vulnerability** in the repository's
[Security tab](https://github.com/Hightmar/jellyfin-langage-failover/security/advisories/new).

## Supported versions

Only the latest release for Jellyfin 12.x gets fixes. The 10.11 line (1.2.0.1) is no longer
maintained.

## Verifying a release

Release ZIPs carry a build provenance attestation:

```bash
gh attestation verify language-failover_<version>.zip --repo Hightmar/jellyfin-langage-failover
```
