#!/usr/bin/env python3

import argparse
import json
import subprocess
import sys


def runtime_version(identifier: str) -> tuple[int, ...]:
    marker = ".iOS-"
    if marker not in identifier:
        return ()
    try:
        return tuple(int(component) for component in identifier.split(marker, 1)[1].split("-"))
    except ValueError:
        return ()


def select_iphone(inventory: dict, preferred_name: str) -> tuple[str, str, tuple[int, ...]]:
    candidates = []
    for runtime, devices in inventory.get("devices", {}).items():
        version = runtime_version(runtime)
        if not version:
            continue
        for device in devices:
            name = device.get("name", "")
            if name.startswith("iPhone") and device.get("isAvailable", True):
                candidates.append((device["udid"], name, version))

    if not candidates:
        raise RuntimeError("No available iPhone simulator was found")

    preferred = [candidate for candidate in candidates if candidate[1] == preferred_name]
    return max(preferred or candidates, key=lambda candidate: candidate[2])


def main() -> int:
    parser = argparse.ArgumentParser(description="Select an available iPhone simulator")
    parser.add_argument("--preferred-name", default="iPhone 16e")
    arguments = parser.parse_args()

    inventory = json.loads(subprocess.check_output(
        ["xcrun", "simctl", "list", "devices", "available", "--json"],
        text=True,
    ))
    try:
        udid, name, version = select_iphone(inventory, arguments.preferred_name)
    except RuntimeError as error:
        print(error, file=sys.stderr)
        return 1

    print(f"Selected {name} running iOS {'.'.join(map(str, version))}", file=sys.stderr)
    print(udid)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
