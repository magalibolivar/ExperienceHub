# -*- coding: utf-8 -*-
"""
Audita Servicios/Multiidioma/traducciones.tsv (corpus i18n ES/EN/PT/RU): duplicados exactos,
valores vacios, claves sin los 4 idiomas, placeholders {0}/{1} inconsistentes entre idiomas de
una misma clave. Pensado para correr local (antes de commitear) o en CI.

Uso: python Scripts/validar_traducciones.py
Exit 0 = corpus limpio. Exit 1 = se encontro al menos un problema (detalle impreso a stdout).
"""
import io
import os
import re
import sys
import collections

LANGS = ["ES", "EN", "PT", "RU"]
TSV = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Servicios", "Multiidioma", "traducciones.tsv")


def main():
    with io.open(TSV, "r", encoding="utf-8") as f:
        raw = f.read()
    lines = raw.split("\n")

    rows = []  # (lang, key, value, lineno_start)
    cur = None
    for i, ln in enumerate(lines, 1):
        m = re.match(r"^(ES|EN|PT|RU)\t([^\t]+)\t(.*)$", ln)
        if m:
            if cur:
                rows.append(cur)
            cur = [m.group(1), m.group(2), m.group(3), i]
        elif cur is not None:
            cur[2] += "\n" + ln
    if cur:
        rows.append(cur)

    print("Total filas reconstruidas: {}".format(len(rows)))
    fail = False

    by_langkey = collections.defaultdict(list)
    for lang, key, val, ln in rows:
        by_langkey[(lang, key)].append((val, ln))
    dups = {k: v for k, v in by_langkey.items() if len(v) > 1}
    print("\n=== DUPLICADOS (mismo idioma+clave repetido): {} ===".format(len(dups)))
    if dups:
        fail = True
        for (lang, key), entries in dups.items():
            print("  {}\t{}  x{}".format(lang, key, len(entries)))

    vacias = [(lang, key, ln) for lang, key, val, ln in rows if val.strip() == ""]
    print("\n=== VACIAS (valor en blanco): {} ===".format(len(vacias)))
    if vacias:
        fail = True
        for lang, key, ln in vacias:
            print("  {}\t{}\t(linea {})".format(lang, key, ln))

    keys_por_idioma = collections.defaultdict(set)
    for lang, key, val, ln in rows:
        keys_por_idioma[lang].add(key)
    todas_claves = set()
    for s in keys_por_idioma.values():
        todas_claves |= s
    faltantes = {}
    for key in sorted(todas_claves):
        presentes = [l for l in LANGS if key in keys_por_idioma[l]]
        if len(presentes) < 4:
            faltantes[key] = [l for l in LANGS if l not in presentes]
    print("\n=== CLAVES SIN LOS 4 IDIOMAS: {} ===".format(len(faltantes)))
    if faltantes:
        fail = True
        for key, faltan in faltantes.items():
            print("  {}  -- faltan: {}".format(key, faltan))

    def placeholders(s):
        return sorted(set(re.findall(r"\{(\d+)\}", s)))

    valores_por_clave = collections.defaultdict(dict)
    for lang, key, val, ln in rows:
        valores_por_clave[key][lang] = val

    mismatches = []
    for key, porlang in valores_por_clave.items():
        phs = {l: placeholders(v) for l, v in porlang.items()}
        distintos = set(tuple(p) for p in phs.values())
        if len(distintos) > 1:
            mismatches.append((key, phs))
    print("\n=== PLACEHOLDERS INCONSISTENTES ENTRE IDIOMAS: {} ===".format(len(mismatches)))
    if mismatches:
        fail = True
        for key, phs in mismatches:
            print("  {}: {}".format(key, phs))

    print("\nTotal claves unicas: {}".format(len(todas_claves)))

    if fail:
        print("\nValidacion de traducciones FALLO -- ver detalle arriba.", file=sys.stderr)
        return 1
    print("\nValidacion de traducciones OK.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
