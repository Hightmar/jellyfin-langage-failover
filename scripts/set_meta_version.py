#!/usr/bin/env python3
"""Stamp a version and timestamp into the plugin's meta.json.

Inputs come from the environment so that nothing is interpolated into this
source at runtime; a tag message containing quotes or backslashes is just data.

Environment:
    VERSION    required, four-segment version (e.g. 1.1.0.3)
    TIMESTAMP  required, ISO-8601 UTC
    CHANGELOG  optional, free text
    META_PATH  optional, defaults to the plugin's meta.json
"""

import json
import os
import re
import sys

DEFAULT_META_PATH = "Jellyfin.Plugin.LanguageFailover/meta.json"
VERSION_RE = re.compile(r"^\d+\.\d+\.\d+\.\d+$")


def main() -> int:
    version = os.environ["VERSION"]
    timestamp = os.environ["TIMESTAMP"]
    changelog = os.environ.get("CHANGELOG")
    path = os.environ.get("META_PATH", DEFAULT_META_PATH)

    if not VERSION_RE.match(version):
        print(
            f"error: version {version!r} must have four segments (major.minor.patch.build); "
            "Jellyfin rejects anything else",
            file=sys.stderr,
        )
        return 1

    with open(path, encoding="utf-8") as f:
        meta = json.load(f)

    meta["version"] = version
    meta["timestamp"] = timestamp
    if changelog is not None:
        meta["changelog"] = changelog.strip()

    with open(path, "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=4, ensure_ascii=False)
        f.write("\n")

    print(f"{path}: version={version} timestamp={timestamp}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
