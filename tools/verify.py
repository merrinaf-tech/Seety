# -*- coding: utf-8 -*-
"""Regression sweep over the whole mod: keys, icons, bindings, writers, wiring."""
import glob, io, os, re, sys

ROOT = r"C:\Users\merri\Documents\Cities Skylines mods\Seety"
GAMEUI = r"C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Content\Game\UI"
fails = []


def check(ok, label, detail=""):
    print(("  OK   " if ok else "  FAIL ") + label + ("  " + detail if detail and not ok else ""))
    if not ok:
        fails.append(label)


def read(rel):
    return io.open(os.path.join(ROOT, rel), encoding="utf-8-sig").read()


# ---- 1. every language table carries the same keys ------------------------------------------
tables = {}
for f in sorted(glob.glob(os.path.join(ROOT, "Seety", "Localization", "Strings", "Strings*.cs"))):
    body = io.open(f, encoding="utf-8").read()
    tables[os.path.basename(f)] = set(re.findall(r'\n\s*\{ "([^"]+)",', body))

ref_name = "StringsEn.cs"
ref = tables[ref_name]
check(len(tables) == 12, "dodici lingue registrate", str(len(tables)))
for name, keys in tables.items():
    check(keys == ref, "chiavi identiche in " + name,
          "mancanti=%s extra=%s" % (sorted(ref - keys)[:3], sorted(keys - ref)[:3]))

# ---- 2. every vital has a title and a label in the reference table ---------------------------
cat = read(os.path.join("Seety", "Vitals", "VitalCatalog.cs"))
scope = cat[cat.index("public static IReadOnlyList<Vital> All()"):cat.index("private static Vital Service(")]
ids = set(re.findall(r'new Vital\("([a-z]+)"', scope))
ids |= set(re.findall(r'\b(?:Service|Container|Hazard|Plain|Flow|Charge)\("([a-z]+)"', scope))
missing = sorted(i for i in ids if "Seety.VITAL[%s]" % i not in ref or "Seety.LABEL[%s]" % i not in ref)
check(not missing, "ogni vital ha titolo ed etichetta tradotti (%d vital)" % len(ids), str(missing))

# ---- 3. no key asked for by the UI is absent from the tables ---------------------------------
tsx = read(os.path.join("UI", "src", "mods", "vitals-strip.tsx"))
asked = set(re.findall(r't\(\s*"(Seety\.[A-Z_]+)"', tsx))
asked |= set(re.findall(r'key: "(Seety\.[A-Z_]+)"', tsx))
asked |= set(re.findall(r'"(Seety\.AGE_[A-Z]+)"', read(os.path.join("Seety", "Localization", "Strings", "StringsEn.cs"))))
absent = sorted(k for k in asked if k not in ref)
check(not absent, "ogni chiave chiesta dalla UI esiste (%d chieste)" % len(asked), str(absent))

# ---- 4. every translate() call carries an English fallback -----------------------------------
bare = re.findall(r't\(\s*"Seety\.[A-Z_]+"\s*\)', tsx)
check(not bare, "ogni translate ha un fallback inglese", str(bare[:3]))

# ---- 5. one writer for seety.BreakdownRow ----------------------------------------------------
sys_cs = read(os.path.join("Seety", "Systems", "SeetyUISystem.cs"))
check(sys_cs.count('TypeBegin("seety.BreakdownRow")') == 1,
      "un solo scrittore per seety.BreakdownRow",
      str(sys_cs.count('TypeBegin("seety.BreakdownRow")')))

# ---- 6. icons referenced actually ship -------------------------------------------------------
icons = set(re.findall(r'"(Media/[A-Za-z0-9/_.-]+\.svg)"', cat + sys_cs + tsx))
for m in re.finditer(r'"Media/Game/" \+ icon', cat):
    pass
icons |= set("Media/Game/" + p for p in re.findall(r'"((?:Icons|Notifications)/[A-Za-z0-9_.-]+\.svg)"', cat))
gone = sorted(p for p in icons if not os.path.isfile(os.path.join(GAMEUI, p.replace("/", os.sep))))
check(not gone, "ogni icona referenziata esiste (%d)" % len(icons), str(gone))

# ---- 7. vanilla binding names still exist ----------------------------------------------------
# Against Game.dll's own string heap, which is where the binding names are declared. The UI
# bundle binds several of them through variables, so it is not a reliable index.
_raw = open(r"C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2_Data\Managed\Game.dll", "rb").read()
dll = _raw.decode("utf-16-le", "ignore") + _raw[1:].decode("utf-16-le", "ignore")
binds = set(re.findall(r'new VanillaBinding\("(\w+)", *(?:string\.Empty|"(\w*)")\s*,\s*"(\w+)"', cat))
names = set()
for group, supply, demand in binds:
    names.add((group, demand))
    if supply:
        names.add((group, supply))
for extra in re.findall(r'"(waterInfo|electricityInfo|roadsInfo|bikesInfo)",\s*"(\w+)"', cat + tsx):
    names.add(extra)
bad = sorted("%s.%s" % n for n in names if n[0] not in dll or n[1] not in dll)
check(not bad, "ogni binding vanilla usato esiste (%d)" % len(names), str(bad[:5]))

# ---- 8. every expandable row is reachable ----------------------------------------------------
for const in ("TRAFFIC_ID", "CEMETERY_ID", "WORKFORCE_ID", "DEMOGRAPHICS_ID", "PARKING_ID", "POLLUTION_ID"):
    check(("vital.id === " + const) in tsx, "riga espandibile raggiungibile: " + const)

# ---- 9. the deployed package ------------------------------------------------------------------
mods = r"C:\Users\merri\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\Seety"
files = sorted(os.listdir(mods)) if os.path.isdir(mods) else []
check(len(files) == 7, "pacchetto installato completo", "%d file: %s" % (len(files), files))

# ---- 10. the .mjs banner version matches the other three ---------------------------------------
# The banner is generated by webpack from UI/mod.json, so bumping the version without rebuilding
# the UI ships a module that registers under the previous one. That happened on 1.0.6: the code
# was correct and the banner said 1.0.5.
import json
mod_json = json.loads(io.open(os.path.join(ROOT, "UI", "mod.json"), encoding="utf-8-sig").read())
declared = mod_json["version"]
versions = {
    "UI/mod.json": declared,
    "Mod.cs": re.search(r'Version = "([\d.]+)"', read(os.path.join("Seety", "Mod.cs"))).group(1),
    "csproj": re.search(r"<Version>([\d.]+)</Version>", read(os.path.join("Seety", "Seety.csproj"))).group(1),
    "PublishConfiguration": re.search(r'<ModVersion Value="([\d.]+)"',
        read(os.path.join("Seety", "Properties", "PublishConfiguration.xml"))).group(1),
}
built = os.path.join(ROOT, "UI", "dist", "Seety.mjs")
if os.path.isfile(built):
    head = io.open(built, encoding="utf-8").read(400)
    m = re.search(r"Version: ([\d.]+)", head)
    versions["dist banner"] = m.group(1) if m else "(assente)"
check(len(set(versions.values())) == 1, "una sola versione ovunque", str(versions))

print()
print("FALLITI: %d" % len(fails))
sys.exit(1 if fails else 0)
