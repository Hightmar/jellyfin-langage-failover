#!/usr/bin/env python3
"""Refuse a release version that already exists or breaks the ABI ordering of the manifest.

For a new entry (V, A) and each existing entry E:
    E.targetAbi <= A  ->  V must be greater than E.version
    E.targetAbi >  A  ->  V must be lower than E.version

Environment:
    VERSION        required, four-segment version
    MANIFEST_PATH  required, the manifest as it is on main
    META_PATH      optional, defaults to the plugin's meta.json
"""

import json
import os
import sys

DEFAULT_META_PATH = "Jellyfin.Plugin.LanguageFailover/meta.json"


def parse(version: str) -> tuple[int, ...]:
    return tuple(int(part) for part in version.split("."))


def main() -> int:
    version = os.environ["VERSION"]
    manifest_path = os.environ["MANIFEST_PATH"]
    meta_path = os.environ.get("META_PATH", DEFAULT_META_PATH)

    with open(meta_path, encoding="utf-8") as f:
        meta = json.load(f)
    with open(manifest_path, encoding="utf-8") as f:
        manifest = json.load(f)

    new_version = parse(version)
    new_abi = parse(meta["targetAbi"])

    plugin = next((p for p in manifest if p.get("guid") == meta["guid"]), None)
    existing = plugin["versions"] if plugin else []

    errors = []
    for entry in existing:
        their_version = parse(entry["version"])
        their_abi = parse(entry["targetAbi"])

        if their_version == new_version:
            errors.append(f"{version} is already in the manifest; bump the version.")
        elif their_abi <= new_abi and their_version > new_version:
            errors.append(
                f"{version} (targetAbi {meta['targetAbi']}) does not outrank "
                f"{entry['version']} (targetAbi {entry['targetAbi']})."
            )
        elif their_abi > new_abi and their_version < new_version:
            errors.append(
                f"{version} (targetAbi {meta['targetAbi']}) outranks "
                f"{entry['version']} (targetAbi {entry['targetAbi']}); newer servers would install it."
            )

    if errors:
        for error in errors:
            print(f"error: {error}", file=sys.stderr)
        return 1

    print(f"{version} (targetAbi {meta['targetAbi']}) fits the manifest")
    return 0


if __name__ == "__main__":
    sys.exit(main())
