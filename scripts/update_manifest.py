#!/usr/bin/env python3
"""Add (or replace) a version entry in the repository plugin manifest.

The plugin's identity — guid, name, description, overview, category, owner and
targetAbi — is read from meta.json rather than restated here, so meta.json stays
the single source of truth and the manifest cannot drift away from the DLL that
ships beside it.

Inputs come from the environment so that nothing is interpolated into this
source at runtime; a tag message containing quotes or backslashes is just data.

Environment:
    VERSION       required, four-segment version (e.g. 1.1.0.3)
    CHANGELOG     required, free text
    SOURCE_URL    required, download URL of the release zip
    CHECKSUM      required, MD5 of the release zip
    TIMESTAMP     required, ISO-8601 UTC
    META_PATH     optional, defaults to the plugin's meta.json
    MANIFEST_PATH optional, defaults to manifest.json
"""

import json
import os
import sys

DEFAULT_META_PATH = "Jellyfin.Plugin.LanguageFailover/meta.json"
DEFAULT_MANIFEST_PATH = "manifest.json"


def main() -> int:
    version = os.environ["VERSION"]
    meta_path = os.environ.get("META_PATH", DEFAULT_META_PATH)
    manifest_path = os.environ.get("MANIFEST_PATH", DEFAULT_MANIFEST_PATH)

    with open(meta_path, encoding="utf-8") as f:
        meta = json.load(f)

    guid = meta["guid"]

    if os.path.exists(manifest_path):
        with open(manifest_path, encoding="utf-8") as f:
            manifest = json.load(f)
    else:
        manifest = []

    plugin = next((p for p in manifest if p.get("guid") == guid), None)
    if plugin is None:
        plugin = {"guid": guid, "versions": []}
        manifest.append(plugin)

    # Keep the catalogue entry in step with meta.json on every release.
    plugin["name"] = meta["name"]
    plugin["description"] = meta["description"]
    plugin["overview"] = meta["overview"]
    plugin["category"] = meta["category"]
    plugin["owner"] = meta["owner"]

    entry = {
        "version": version,
        "changelog": os.environ["CHANGELOG"].strip(),
        "targetAbi": meta["targetAbi"],
        "sourceUrl": os.environ["SOURCE_URL"],
        "checksum": os.environ["CHECKSUM"],
        "timestamp": os.environ["TIMESTAMP"],
    }

    plugin["versions"] = [v for v in plugin["versions"] if v.get("version") != version]
    plugin["versions"].insert(0, entry)

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")

    print(f"{manifest_path}: added {version} ({len(plugin['versions'])} versions total)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
