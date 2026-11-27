#!/usr/bin/env python3
"""Lists every stock part that can be staged, with the staging chips ModernUI gives it.

Reads the game's part data (Content/*/*.xml) and applies Staging.cs's rules: which icon each
stageable module gets (EngineController: liquid or solid; Decoupler: stack or radial;
Parachute: deploy and cut), and the caption name for a part with no display name (from its
editor tags). One line per part, sorted, so a new game build's list can be diffed against
docs/staged-parts.txt:

    scripts/staged-parts.py | diff docs/staged-parts.txt -

A new or renamed part, a part that changes icon, or a part ModernUI can't name shows up in
the diff. "?" marks a stageable part that falls back to a two-letter code: give it an icon
in Staging.cs (KindOf / DrawIcon). Usage: staged-parts.py [Content folder]
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET

DEFAULT = os.path.expanduser("~/Applications/Kitten Space Agency.app/Contents/Resources/Content")
# The part-data elements that become ISequenced modules in the game (KSA 2026.11.25.5949):
# RocketEngineController -> EngineController, Decoupler -> Decoupler, Parachute ->
# ParachuteDeploy and ParachuteCut. A new kind of stageable module needs adding here and in
# Staging.cs.
STAGED = {"RocketEngineController", "Decoupler", "Parachute"}


def scan(content):
    parts = {}
    for path in sorted(glob.glob(os.path.join(content, "**", "*.xml"), recursive=True)):
        if "defaultvehicles" in path:
            continue
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for p in root.iter():
            if p.tag not in ("Part", "SubPart") or not p.get("Id"):
                continue
            info = parts.setdefault(p.get("Id"), {"top": False, "mods": set(), "tags": set(), "solid": False, "named": False, "subs": set()})
            info["top"] |= p.tag == "Part"
            for el in p.iter():
                if el.tag in STAGED:
                    info["mods"].add(el.tag)
                elif el.tag == "SolidRocket":
                    info["solid"] = True
                elif el.tag == "EditorTag" and el.get("Ref"):
                    info["tags"].add(el.get("Ref"))
                elif el.tag == "DisplayName" and (el.text or el.get("Value")):
                    info["named"] = True
                ref = el.get("Ref") or el.get("InstanceOf")
                if ref and el.tag.endswith("SubPart"):
                    info["subs"].add(ref)
            if p.get("DisplayName"):
                info["named"] = True
    # A prefab carries its sub-parts' modules.
    for _ in range(4):
        for info in parts.values():
            for s in info["subs"]:
                if s in parts:
                    info["mods"] |= parts[s]["mods"]
                    info["solid"] |= parts[s]["solid"]
    return parts


def chips(info):
    out = []
    if "RocketEngineController" in info["mods"]:
        out.append("solid-motor" if info["solid"] else "engine")
    if "Decoupler" in info["mods"]:
        out.append("radial-decoupler" if "Radial" in info["tags"] else "stack-decoupler")
    if "Parachute" in info["mods"]:
        out.append("parachute")
    return out or ["?"]


# Staging.Describe, for parts without a display name.
def caption(info, first_chip):
    tags = info["tags"]
    if "Booster" in tags and "Decoupler" in info["mods"]:
        return "ESCAPE TOWER"
    for tag, name in (("Interstellar", "INTERSTELLAR DRIVE"), ("Thermal", "HEATSHIELD"),
                      ("Interstage", "INTERSTAGE"), ("Cargo", "SERVICE MODULE")):
        if tag in tags:
            return name
    return {"engine": "ENGINE", "solid-motor": "SOLID MOTOR", "stack-decoupler": "STACK DECOUPLER",
            "radial-decoupler": "RADIAL DECOUPLER", "parachute": "PARACHUTE"}.get(first_chip, "PART")


def main():
    content = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    if not os.path.isdir(content):
        sys.exit(f"no Content folder at {content}")
    for pid, info in sorted(scan(content).items()):
        if not info["top"] or not info["mods"] or pid.startswith("Test"):
            continue
        c = chips(info)
        name = "(named)" if info["named"] else caption(info, c[0])
        print(f"{pid:52s} {' + '.join(c):34s} {name}")


if __name__ == "__main__":
    main()
