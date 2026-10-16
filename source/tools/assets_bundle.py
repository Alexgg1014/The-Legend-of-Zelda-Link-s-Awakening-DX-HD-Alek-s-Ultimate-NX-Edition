#!/usr/bin/env python3
"""Herramientas del instalador in-game (v1.1).

  python assets_bundle.py bundle   -> genera ProjectZ.Switch/switch-assets.zip (parches de bighead
                                      + extras) que se embebe en el NRO.
  python assets_bundle.py v100test -> reconstruye una carpeta v1.0.0 de prueba en
                                      _switchbuild/v100_test/ a partir de Data/Backup de la
                                      instalacion v2.0.8 de Windows (para probar en PC).

Fuentes: upstream204/ladxhd_patcher_source_code/Resources/ (patches_android_gl.7z,
patches_allplatforms.7z, achievements.7z, d3map, d3mapdata).
"""
import os, sys, io, subprocess, zipfile, shutil, tempfile, re

HERE = os.path.dirname(os.path.abspath(__file__))
SB = os.path.dirname(HERE)                       # _switchbuild
UP = os.path.join(SB, "upstream204")
RES = os.path.join(UP, "ladxhd_patcher_source_code", "Resources")
SWITCH = os.path.join(UP, "ladxhd_game_source_code", "ProjectZ.Switch")
REF = os.path.join(SB, "Links Awakening DX HD")  # instalacion v2.0.8 Windows+GL (referencia)
V100 = os.path.join(SB, "v100_test")
SEVENZ = shutil.which("7z") or r"C:\Program Files\7-Zip\7z.exe"

ASSET_VERSION = "2.0.9"


def fnv64(path):
    h = 0xcbf29ce484222325
    with open(path, "rb") as f:
        while True:
            b = f.read(1 << 20)
            if not b:
                break
            for x in b:
                h ^= x
                h = (h * 0x100000001b3) & 0xFFFFFFFFFFFFFFFF
    return h


def extract7z(archive, dest):
    os.makedirs(dest, exist_ok=True)
    subprocess.check_call([SEVENZ, "x", "-y", "-o" + dest, archive], stdout=subprocess.DEVNULL)


def parse_targets():
    """file_targets_xnb.txt -> (dict master->[derivados], set derivados)."""
    masters, derived = {}, set()
    with open(os.path.join(RES, "file_targets_xnb.txt"), encoding="utf-8") as f:
        for line in f:
            line = line.rstrip("\n")
            if not line or line.startswith("#"):
                continue
            src, rest = line.split("\t", 1)
            names = rest.split(" ")
            masters[src] = names
            derived.update(n.lower() for n in names)
    return masters, derived


def walk(root):
    for dp, dn, fn in os.walk(root):
        for n in fn:
            yield os.path.join(dp, n)


def cmd_v100test():
    masters, derived = parse_targets()
    backup = os.path.join(REF, "Data", "Backup")
    if os.path.isdir(V100):
        shutil.rmtree(V100)
    n_copy = n_backup = n_skip = 0
    for sub in ("Content", "Data"):
        for p in walk(os.path.join(REF, sub)):
            rel = os.path.relpath(p, REF)
            parts = rel.replace("\\", "/").split("/")
            if parts[:2] in (["Data", "Backup"], ["Data", "Achievements"], ["Data", "Icon"], ["Data", "Buttons"]):
                continue
            name = os.path.basename(p)
            if name.lower() in derived:
                n_skip += 1
                continue
            dst = os.path.join(V100, rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            b = os.path.join(backup, name)
            if os.path.isfile(b):
                shutil.copy2(b, dst)
                n_backup += 1
            else:
                shutil.copy2(p, dst)
                n_copy += 1
    # v1.0.0 llevaba dungeon3_1.map(.data); el patcher los reescribe desde recursos.
    maps = os.path.join(V100, "Data", "Maps")
    shutil.copy2(os.path.join(RES, "d3map"), os.path.join(maps, "dungeon3_1.map"))
    shutil.copy2(os.path.join(RES, "d3mapdata"), os.path.join(maps, "dungeon3_1.map.data"))
    shutil.copy2(os.path.join(backup, "Link's Awakening DX HD.exe"), os.path.join(V100, "Link's Awakening DX HD.exe"))
    print(f"v100_test: {n_copy} copiados, {n_backup} desde Backup, {n_skip} derivados omitidos")


def cmd_bundle():
    masters, derived = parse_targets()
    tmp = tempfile.mkdtemp(prefix="ladx_bundle_")
    try:
        patches = os.path.join(tmp, "patches")
        extract7z(os.path.join(RES, "patches_android_gl.7z"), patches)
        extract7z(os.path.join(RES, "patches_allplatforms.7z"), patches)
        ach = os.path.join(tmp, "ach")
        extract7z(os.path.join(RES, "achievements.7z"), ach)

        # Manifiesto: hash de cada fuente v1.0.0 que se parchea (desde Data/Backup de la
        # instalacion de referencia) -> el instalador comprueba que original/ es v1.0.0 limpio.
        backup = os.path.join(REF, "Data", "Backup")
        patch_names = {n[:-len(".vcdiff")] for n in os.listdir(patches) if n.endswith(".vcdiff")}
        lines = []
        for name in sorted(os.listdir(backup), key=str.lower):
            if name.lower() in derived:
                continue
            if name.lower().endswith(".exe"):
                continue
            if name not in patch_names and name not in masters:
                print("  aviso: Backup tiene", name, "sin parche ni targets; se omite del manifiesto")
                continue
            p = os.path.join(backup, name)
            lines.append(f"{name}\t{os.path.getsize(p)}\t{fnv64(p):016x}")
        # dungeon3_1 se sustituye por el recurso, no se verifica.

        out = os.path.join(SWITCH, "switch-assets.zip")
        with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            z.writestr("version.txt", ASSET_VERSION + "\n")
            z.writestr("manifest.txt", "\n".join(lines) + "\n")
            for n in sorted(os.listdir(patches)):
                z.write(os.path.join(patches, n), "patches/" + n)
            for p in walk(ach):
                z.write(p, "extra/Data/Achievements/" + os.path.relpath(p, ach).replace("\\", "/"))
            z.write(os.path.join(RES, "d3map"), "extra/Data/Maps/dungeon3_1.map")
            z.write(os.path.join(RES, "d3mapdata"), "extra/Data/Maps/dungeon3_1.map.data")
            z.write(os.path.join(RES, "file_targets_xnb.txt"), "file_targets_xnb.txt")
        print(f"bundle: {out} ({os.path.getsize(out)//1024} KB), {len(patch_names)} parches, "
              f"{len(lines)} entradas de manifiesto")
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    {"bundle": cmd_bundle, "v100test": cmd_v100test}.get(cmd, lambda: print(__doc__))()
