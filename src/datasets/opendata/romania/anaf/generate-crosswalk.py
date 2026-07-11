#!/usr/bin/env python3
"""
Generate the slim ANAF(MFP) -> SIRUTA crosswalk embedded by ES.FX.OpenData.Romania.Anaf.

Source of truth = the MFP `nomLocalitati_*.xml` files (COD_SIRUTA extracted VERBATIM).
Output = deterministic JSON  { "<cod_Judet>": { "<cod_Localitate>": <sirutaCode:int> } }.

Usage:
    1. Download the MFP nomenclature XMLs (localities only) into  <repo>/ANAF_EXPORT/  (see README.md).
    2. python generate-crosswalk.py

Hard validation gates (any failure => non-zero exit, nothing written) and the drop rules are documented in
README.md. Coverage against the shipped SIRUTA snapshot is reported only — it never filters the map.
"""
import glob
import gzip
import json
import os
import sys
import xml.etree.ElementTree as ET

EDITION = "2026-07"
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))  # anaf -> romania -> opendata -> datasets -> src -> repo
SRC = os.path.join(REPO, "ANAF_EXPORT")
SIR = os.path.join(REPO, "src", "ES.FX.OpenData.Romania.TerritorialUnits", "Resources", "siruta.2025-12.csv.gz")
OUT = os.path.join(HERE, f"anaf-siruta.{EDITION}.json")

errors = []


def gate(cond, msg):
    if not cond:
        errors.append(msg)


def txt(el, tag):
    c = el.find(tag)
    return (c.text or "").strip() if c is not None and c.text else ""


def as_int(s):
    try:
        return int(s)
    except (TypeError, ValueError):
        return None


def main():
    if not os.path.isdir(SRC):
        sys.exit(f"source folder not found: {SRC}\nDownload the MFP nomenclature XMLs first (see README.md).")

    county_codes = {txt(r, "COD") for r in ET.parse(os.path.join(SRC, "nomJudete.xml")).getroot()}
    gate(len(county_codes) == 42, f"expected 42 counties, got {len(county_codes)}")

    county_map, seen = {}, {}
    kept = dropped_pseudo = dropped_nosir = 0
    files = sorted(glob.glob(os.path.join(SRC, "nomLocalitati_*.xml")),
                   key=lambda p: int(os.path.basename(p).split("_")[1]))
    gate(len(files) == 42, f"expected 42 locality files, got {len(files)}")

    for f in files:
        for r in ET.parse(f).getroot():
            jud, cod, tpl, sir = txt(r, "JUD_COD"), txt(r, "COD"), txt(r, "TPL_COD"), txt(r, "COD_SIRUTA")
            gate(jud != "" and cod != "", f"{os.path.basename(f)}: row missing JUD_COD/COD")
            gate(jud in county_codes, f"{os.path.basename(f)}: JUD_COD {jud!r} not a known county")
            gate(as_int(jud) is not None and as_int(cod) is not None, f"non-int JUD/COD {jud}/{cod}")
            key = (jud, cod)
            if key in seen:
                errors.append(f"DUPLICATE key {key} in {os.path.basename(f)} and {seen[key]}")
            seen[key] = os.path.basename(f)
            if tpl == "?":
                dropped_pseudo += 1
                continue
            siruta = as_int(sir)
            if siruta is None or siruta <= 0:
                dropped_nosir += 1
                continue
            county_map.setdefault(jud, {})[cod] = siruta
            kept += 1

    gate(county_map.get("12", {}).get("103") == 54984,
         f"Cluj-Napoca spot-check failed: 12/103 -> {county_map.get('12', {}).get('103')}")

    if errors:
        print("VALIDATION FAILED - nothing written:")
        for e in errors[:50]:
            print("  !", e)
        sys.exit(1)

    jud_sorted = sorted(county_map, key=int)
    lines = ["{"]
    for i, jud in enumerate(jud_sorted):
        inner = county_map[jud]
        body = ",".join(f'"{c}":{inner[c]}' for c in sorted(inner, key=int))
        comma = "," if i < len(jud_sorted) - 1 else ""
        lines.append(f'"{jud}":{{{body}}}{comma}')
    lines.append("}")
    text = "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)

    reloaded = json.loads(text)
    gate(sum(len(v) for v in reloaded.values()) == kept, "round-trip count mismatch")
    gate(reloaded["12"]["103"] == 54984, "round-trip spot-check failed")
    if errors:
        print("POST-WRITE VALIDATION FAILED:")
        for e in errors:
            print("  !", e)
        sys.exit(1)

    # Coverage report (informational only).
    sir_codes = set()
    if os.path.exists(SIR):
        with gzip.open(SIR, "rt", encoding="utf-8") as fh:
            fh.readline()
            for line in fh:
                p = line.rstrip("\n").split(";")
                if p and p[0]:
                    sir_codes.add(p[0])
    emitted = [s for m in county_map.values() for s in m.values()]
    resolves = sum(1 for s in emitted if str(s) in sir_codes) if sir_codes else 0

    print(f"ANAF->SIRUTA crosswalk {EDITION} written: {OUT}")
    print(f"  rows={len(seen):,}  kept={kept:,}  dropped_pseudo={dropped_pseudo}  dropped_no_siruta={dropped_nosir}")
    print(f"  (JUD,COD) unique: YES  size={os.path.getsize(OUT):,} bytes")
    if sir_codes:
        print(f"  resolves in siruta.2025-12: {resolves:,}/{len(emitted):,} ({resolves / len(emitted) * 100:.2f}%)")


if __name__ == "__main__":
    main()
