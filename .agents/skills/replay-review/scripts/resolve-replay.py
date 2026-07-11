#!/usr/bin/env python3
"""Resolve a KOTGH replay ID or URL into its UI and API URLs."""

import argparse
import re
from urllib.parse import parse_qs, urlencode, urlparse


REPLAY_ID = re.compile(r"^[A-Za-z0-9_-]+$")


def resolve(value: str) -> tuple[str, str, str]:
    value = value.strip()
    parsed = urlparse(value if "://" in value else "")
    query: dict[str, list[str]] = {}

    if parsed.scheme:
        match = re.search(r"/(?:api/game/)?replay/([^/?#]+)", parsed.path)
        if not match:
            raise ValueError("expected a KOTGH replay UI or API URL")
        replay_id = match.group(1)
        query = parse_qs(parsed.query)
    else:
        replay_id = value

    if not REPLAY_ID.fullmatch(replay_id):
        raise ValueError("invalid replay ID")

    preserved = {
        key: values[-1]
        for key, values in query.items()
        if key in {"round", "player", "fight"} and values
    }
    ui = f"https://kotgh.ozvmusic.com/replay/{replay_id}"
    if preserved:
        ui += "?" + urlencode(preserved)
    api = f"https://kotgh.ozvmusic.com/api/game/replay/{replay_id}"
    return replay_id, ui, api


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("replay", help="replay ID, UI URL, or API URL")
    args = parser.parse_args()
    try:
        replay_id, ui, api = resolve(args.replay)
    except ValueError as exc:
        parser.error(str(exc))
    print(f"id={replay_id}")
    print(f"ui={ui}")
    print(f"api={api}")


if __name__ == "__main__":
    main()
