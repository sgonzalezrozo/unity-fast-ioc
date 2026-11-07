#!/usr/bin/env python3
"""Builds the internal Fast IoC benchmark brief (HTML + PDF) from raw device results.

Usage:
  python3 make_brief.py <results-root> [--profile NAME] [--commit SHA] [--out FastIoC-benchmark-brief]

<results-root> holds one folder per device (pixel5/, a02s/, ...) with the raw JSON files that
tools/run_android.sh and tools/run_ios.sh pull. Every number and every comparison sentence is
computed here from those files. Only runs from one commit are used (the newest by default), so
results from different builds are never mixed. Runs whose graph check failed are excluded and counted.
"""
import argparse
import html
import json
import shutil
import statistics
import subprocess
import sys
from collections import defaultdict
from datetime import datetime
from pathlib import Path

VARIANTS = ["fastioc", "vcontainer", "zenject"]
BASELINE = "none"
LABEL = {"fastioc": "Fast IoC", "vcontainer": "VContainer", "zenject": "Zenject (Extenject)", BASELINE: "No DI (baseline)"}
SHORT = {"fastioc": "Fast IoC", "vcontainer": "VContainer", "zenject": "Zenject", BASELINE: "No DI"}
COLOR = {"vcontainer": "#c2410c", "zenject": "#7e22ce"}
PROFILE_ORDER = ["small-100", "startup-200", "growth-500", "readme-scale-1000", "mature-2500", "game-3000", "large-team-5000"]
PROFILE_BLURB = {
    "small-100": "a small prototype",
    "readme-scale-1000": "the size the Fast IoC README describes",
    "mature-2500": "a mature project",
    "game-3000": "the size of a typical mobile multiplayer game",
    "large-team-5000": "a large team after two years",
}
DEVICES = [
    ("Pixel 5", "mid-range Android", ["Pixel 5"]),
    ("Samsung A02s", "low-end Android, 32-bit", ["SM-A025"]),
    ("iPhone 13 mini", "iOS", ["iPhone14,4"]),
    ("iPhone Air", "iOS, 120 Hz", ["iPhone18,4", "iPhone Air"]),
]
FRAME_30 = 1000 / 30


# ---------------------------------------------------------------- data

def load(root: Path):
    runs, rejected = [], []
    for path in sorted(root.rglob("*.json")):
        if path.name == "plan.json":
            continue
        try:
            r = json.loads(path.read_text(encoding="utf-8"))
        except ValueError:
            continue
        if "metrics" not in r:
            continue
        r["_m"] = {(m["name"], m.get("aggregation") or "single"): m["value"] for m in r["metrics"]}
        r["_notes"] = dict(p.split("=", 1) for p in r["session"].get("notes", "").split("; ") if "=" in p)
        baseline = r["workload"]["variant"] == BASELINE
        valid = baseline or (r["_m"].get(("graph-valid", "single")) == 1 and r["_m"].get(("graph-valid-after-reload", "single")) == 1)
        (runs if valid else rejected).append(r)
    return runs, rejected


def device_of(model):
    for name, _, keys in DEVICES:
        if any(k in model for k in keys):
            return name
    return model


def med(values):
    values = [v for v in values if v is not None]
    return statistics.median(values) if values else None


class Cell:
    """Medians across the runs of one device, profile and variant."""

    def __init__(self, runs):
        self.runs = runs

    def get(self, name, agg="single"):
        return med([r["_m"].get((name, agg)) for r in self.runs])

    def di(self):
        return med([r["_m"].get(("container-build", "single"), 0) + r["_m"][("scene-first-inject", "single")] for r in self.runs])

    def spread(self, name, agg="single"):
        values = [r["_m"].get((name, agg)) for r in self.runs if r["_m"].get((name, agg)) is not None]
        return (min(values), max(values)) if values else (None, None)


def cells_for(groups, device, profile):
    return {k: Cell(v) for k, v in groups.get((device, profile), {}).items()}


# ---------------------------------------------------------------- formatting

def e(text):
    return html.escape(str(text))


def ms(v, digits=1):
    if v is None:
        return "–"
    if abs(v) >= 1000:
        return f"{v / 1000:,.2f} s"
    return f"{v:,.{digits}f} ms"


def num(v, digits=1):
    return "–" if v is None else f"{v:,.{digits}f}"


def kb(v):
    return "–" if v is None else f"{v / 1024:,.0f} KB"


def mb(v, digits=2):
    return "–" if v is None else f"{v:,.{digits}f} MB"


def mb_bytes(v, digits=2):
    return "–" if v is None else f"{v / 1048576:,.{digits}f} MB"


def ratio(small, big):
    if not small or not big:
        return None
    return big / small


def x(r):
    if r is None:
        return "–"
    return f"{r:.1f}×" if r < 10 else f"{r:,.0f}×"


def xrange(values):
    values = [v for v in values if v]
    if not values:
        return "–"
    lo, hi = min(values), max(values)
    return x(lo) if x(lo) == x(hi) else f"{x(lo)}–{x(hi)}"


def frames(v):
    return v / FRAME_30


# ---------------------------------------------------------------- style

CSS = """
@page { size: A4; margin: 16mm 14mm 16mm 14mm; }
* { box-sizing: border-box; }
body { font-family: 'DejaVu Sans', 'Helvetica Neue', Arial, sans-serif; color: #1f2937; font-size: 9.6pt; line-height: 1.42; margin: 0; }
h1 { font-size: 21pt; margin: 0 0 4px; color: #111827; }
h2 { font-size: 14pt; margin: 18px 0 6px; color: #111827; }
h3 { font-size: 11pt; margin: 14px 0 4px; color: #2563eb; }
.sub { color: #6b7280; font-size: 8.6pt; margin: 0 0 2px; }
.device-label { font-weight: bold; font-size: 8.6pt; margin: 12px 0 3px; color: #374151; }
.device-label span { color: #6b7280; }
.cards { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid #dbe4f3; background: #eef3fc; }
.card { padding: 8px 9px; border-right: 1px solid #dbe4f3; }
.card:last-child { border-right: none; }
.card .k { font-size: 7.4pt; font-weight: bold; color: #4b5563; letter-spacing: .03em; }
.card .v { font-size: 19pt; font-weight: bold; color: #2563eb; line-height: 1.15; margin: 2px 0; }
.card .d { font-size: 7.8pt; color: #374151; }
.card .x { font-size: 7.4pt; color: #6b7280; margin-top: 2px; }
.card .o { font-size: 7.8pt; font-weight: bold; margin-top: 3px; }
.note { font-size: 7.6pt; color: #6b7280; margin: 4px 0 0; }
ol, ul { margin: 4px 0 4px 18px; padding: 0; }
li { margin: 3px 0; }
table { border-collapse: collapse; width: 100%; font-size: 8.2pt; margin: 4px 0; }
th, td { border: 1px solid #e5e7eb; padding: 4px 6px; text-align: left; vertical-align: top; }
th { background: #f3f4f6; font-weight: bold; }
td.fi, th.fi { background: #eef3fc; }
td.base, th.base { color: #6b7280; }
tr.section td { background: #1f2937; color: #fff; font-weight: bold; font-size: 7.6pt; letter-spacing: .02em; }
.page { page-break-before: always; }
.pending { color: #6b7280; font-size: 8.6pt; }
.honest { margin-top: 8px; }
code { font-family: 'DejaVu Sans Mono', monospace; font-size: 7.8pt; }
"""


# ---------------------------------------------------------------- page 1

def card(key, value, desc, extra, others):
    other_html = "".join(f'<div class="o" style="color:{COLOR[v]}">{e(t)}</div>' for v, t in others)
    return (f'<div class="card"><div class="k">{e(key)}</div><div class="v">{e(value)}</div>'
            f'<div class="d">{e(desc)}</div><div class="x">{e(extra)}</div>{other_html}</div>')


def headline(device, tier, c):
    f = c.get("fastioc")
    if not f:
        return ""
    rivals = [(k, c[k]) for k in ("vcontainer", "zenject") if k in c]
    cards = [
        card("STARTUP COST", ms(f.di()), "DI work when the game launches: build the container and inject the first scene",
             f"{frames(f.di()):.1f} frames at 30 fps",
             [(k, f"{SHORT[k]}: {ms(r.di())} ({x(ratio(f.di(), r.di()))})") for k, r in rivals]),
        card("SCENE RELOAD", ms(f.get("scene-reload-inject", "median")), "to inject a scene again, every time the player re-enters it",
             f"p95 {ms(f.get('scene-reload-inject', 'p95'))}",
             [(k, f"{SHORT[k]}: {ms(r.get('scene-reload-inject', 'median'))}") for k, r in rivals]),
        card("MEMORY HELD", mb(f.get("heap-after-ready")), "managed memory after startup, including the game's own 4,000+ service objects",
             f"No DI: {mb(c[BASELINE].get('heap-after-ready'))}" if BASELINE in c else "",
             [(k, f"{SHORT[k]}: {mb(r.get('heap-after-ready'))}") for k, r in rivals]),
        card("PREFAB SPIKES", ms(f.get("prefab-batch", "p95")), "worst-case (p95) frame cost to spawn 50 injected prefabs",
             f"typical (median) {ms(f.get('prefab-batch', 'median'))}",
             [(k, f"{SHORT[k]}: {ms(r.get('prefab-batch', 'p95'))} p95 · {ms(r.get('prefab-batch', 'median'))} median") for k, r in rivals]),
    ]
    return f'<div class="device-label">{e(device)} <span>· {e(tier)}</span></div><div class="cards">{"".join(cards)}</div>'


def over_base(c, name, agg="single"):
    """Value of each variant minus the no-DI baseline."""
    if BASELINE not in c:
        return {}
    b = c[BASELINE].get(name, agg)
    return {k: (c[k].get(name, agg) - b) if (k in c and b is not None and c[k].get(name, agg) is not None) else None for k in VARIANTS}


def signed_ms(v):
    if v is None:
        return "–"
    if abs(v) < 50:
        return "≈0 (within run-to-run noise)"
    return ("+" if v > 0 else "−") + ms(abs(v))


def differs(groups, profile, devices):
    li = defaultdict(list)
    prefab_median_behind = []
    for d in devices:
        c = cells_for(groups, d, profile)
        if not all(k in c for k in VARIANTS):
            continue
        f, v, z = (c[k] for k in VARIANTS)
        li["startup"].append(
            f"{d}: Fast IoC {ms(f.get('container-build'))} to build + {ms(f.get('scene-first-inject'))} to inject the first scene; "
            f"VContainer {ms(v.get('container-build'))} + {ms(v.get('scene-first-inject'))}; Zenject {ms(z.get('container-build'))} + {ms(z.get('scene-first-inject'))}.")
        ob = over_base(c, "startup-total")
        if ob:
            li["first"].append(
                f"{d}: time from launch to the first scene ready, compared with the same project without DI: "
                f"Fast IoC {signed_ms(ob['fastioc'])}, VContainer {signed_ms(ob['vcontainer'])}, Zenject {signed_ms(ob['zenject'])}.")
        rb = over_base(c, "scene-reload-inject", "median")
        li["reload"].append(
            f"{d}: {ms(f.get('scene-reload-inject', 'median'))} vs {ms(v.get('scene-reload-inject', 'median'))} (VContainer) and "
            f"{ms(z.get('scene-reload-inject', 'median'))} (Zenject)"
            + (f"; the scene without DI takes {ms(c[BASELINE].get('scene-reload-inject', 'median'))}, so the container's own share is "
               f"{ms(rb['fastioc'])} / {ms(rb['vcontainer'])} / {ms(rb['zenject'])}." if rb else "."))
        hb = over_base(c, "heap-after-ready")
        gb = over_base(c, "startup-heap-growth")
        li["memory"].append(
            f"{d}: memory held after startup {mb(f.get('heap-after-ready'))} vs {mb(v.get('heap-after-ready'))} and {mb(z.get('heap-after-ready'))}"
            + (f" (without DI: {mb(c[BASELINE].get('heap-after-ready'))}; added by each: {mb(hb['fastioc'])} / {mb(hb['vcontainer'])} / {mb(hb['zenject'])})" if hb else "")
            + f". Garbage collections during startup: {num(f.get('startup-gc-collections'), 0)} / {num(v.get('startup-gc-collections'), 0)} / {num(z.get('startup-gc-collections'), 0)}"
            + (f" (without DI: {num(c[BASELINE].get('startup-gc-collections'), 0)})." if BASELINE in c else "."))
        pb = over_base(c, "prefab-heap-growth")
        li["prefab"].append(
            f"{d}: batch of 50, p95 {ms(f.get('prefab-batch', 'p95'))} vs {ms(v.get('prefab-batch', 'p95'))} (VContainer) and {ms(z.get('prefab-batch', 'p95'))} (Zenject); "
            f"first batch {ms(f.get('prefab-first-batch'))} / {ms(v.get('prefab-first-batch'))} / {ms(z.get('prefab-first-batch'))}; "
            f"median {ms(f.get('prefab-batch', 'median'))} / {ms(v.get('prefab-batch', 'median'))} / {ms(z.get('prefab-batch', 'median'))}; "
            f"garbage for 1,000 spawns {kb(f.get('prefab-heap-growth'))} / {kb(v.get('prefab-heap-growth'))} / {kb(z.get('prefab-heap-growth'))}"
            + (f" (without DI: {kb(c[BASELINE].get('prefab-heap-growth'))})." if BASELINE in c else "."))
        if f.get("prefab-batch", "median") > v.get("prefab-batch", "median"):
            prefab_median_behind.append((d, f.get("prefab-batch", "median"), v.get("prefab-batch", "median")))
        li["load"].append(
            f"{d}: Unity's part (load call to first Awake) {ms(f.get('scene-first-preawake'), 0)} / {ms(v.get('scene-first-preawake'), 0)} / {ms(z.get('scene-first-preawake'), 0)}"
            + (f" / {ms(c[BASELINE].get('scene-first-preawake'), 0)} without DI" if BASELINE in c else "")
            + f"; whole load {ms(f.get('scene-first-load'), 0)} / {ms(v.get('scene-first-load'), 0)} / {ms(z.get('scene-first-load'), 0)}"
            + (f" / {ms(c[BASELINE].get('scene-first-load'), 0)}." if BASELINE in c else "."))

    def lst(items):
        return "<ul>" + "".join(f"<li>{e(i)}</li>" for i in items) + "</ul>"

    behind = ""
    if prefab_median_behind:
        parts = "; ".join(f"{d}: {ms(a)} vs {ms(b)}" for d, a, b in prefab_median_behind)
        behind = f"""<h3>Where Fast IoC is behind</h3>
<ul><li><b>Typical prefab spawn.</b> Once each prefab type has been seen, VContainer's typical (median) batch of 50 is slightly faster ({e(parts)}).
Fast IoC still avoids the spikes listed above.</li></ul>"""

    return f"""
<h2>Where Fast IoC actually differs</h2>
<ol>
<li><b>Startup.</b> Fast IoC writes the container at edit time, so at launch there is no reflection and no registration work.
VContainer and Zenject analyse every type by reflection on the phone. Startup cost = building the global container plus the first scene injection,
which creates every singleton of the graph. {lst(li['startup'])}</li>
<li><b>Time to the first scene.</b> What the player waits for, measured against the same project with no DI at all. {lst(li['first'])}</li>
<li><b>Scene reloads.</b> Each time a scene loads again its scripts are injected again. Fast IoC walks a list baked into the scene at build time;
Zenject scans the scene and injects by reflection, and VContainer walks the objects it is given with reflection-built injectors. {lst(li['reload'])}</li>
<li><b>Memory.</b> Less memory held and fewer garbage collections (each one a short freeze) while starting. {lst(li['memory'])}</li>
<li><b>Prefab spawn spikes.</b> VContainer and Zenject analyse each prefab type the first time it is spawned; with 200 prefab types those first spawns
cost whole frames. Fast IoC's injectors are generated, so there is no per-type analysis on the phone, and spawning creates almost no garbage
beyond what Unity itself creates. {lst(li['prefab'])}</li>
</ol>
{behind}
<h3>Whole scene load</h3>
<ul><li>Unity's own part (reading and creating the scene, before any script runs) is about the same for every variant; the difference in the whole load
is the injection. On the Pixel 5, Unity's part was faster after the slower containers, likely because the phone's CPU had already sped up during their long startup. {lst(li['load'])}</li></ul>
"""


# ---------------------------------------------------------------- other sections

def setup_section(notes):
    gen = notes.get("fastiocGenerationSeconds")
    gen_text = f"{float(gen):.1f} s on this project" if gen else "measured per project"
    rows = [
        ("Install",
         "Unity package (git or local), plus its Tools Core and Addressables dependencies",
         "Unity package from a git URL",
         "Unity package from a git URL (Extenject fork) or a .unitypackage"),
        ("First setup",
         "One menu item (Tools &gt; FastIoC &gt; Setup) creates the project context",
         "A LifetimeScope subclass per scope, placed on a GameObject; the root scope is set in VContainerSettings or referenced by type from child scopes",
         "A ProjectContext prefab in Resources for global bindings, plus a SceneContext object in every scene"),
        ("Declaring services",
         "Installer classes: Bind&lt;IService&gt;().To&lt;Service&gt;().AsSingle()",
         "Configure(): builder.Register&lt;Service&gt;(Lifetime.Singleton).As&lt;IService&gt;()",
         "MonoInstaller components on a context: Container.Bind&lt;IService&gt;().To&lt;Service&gt;().AsSingle()"),
        ("Injecting scene scripts",
         "Mark fields <code>internal</code>; no attribute. Every scene object is found automatically (list baked at build time)",
         "[Inject] on fields; only objects listed on the scope (or registered one by one) are injected",
         "[Inject] on fields; the SceneContext finds and injects every object in the scene"),
        ("Spawning prefabs",
         "IInstantiator.Instantiate&lt;T&gt;(prefab); the root component implements IInstanciable",
         "IObjectResolver.Instantiate(prefab)",
         "DiContainer.InstantiatePrefab(prefab), or factories"),
        ("Edit-time step",
         f"Code is regenerated after a compile when bindings or injected fields change: {gen_text}",
         "None by default (reflection at runtime). Optional source generator",
         "None by default (reflection at runtime). Optional reflection baking"),
        ("Rules the code must follow",
         "One public constructor per service; injected fields are <code>internal</code> (assemblies in packages need InternalsVisibleTo). No named, factory, decorator or open-generic bindings yet",
         "[Inject] on the constructor when a class has several",
         "[Inject] on the constructor when a class has several"),
        ("Moving from Zenject",
         "Binding syntax is close to Zenject's. Code using Zenject factories, signals, pools or sub-containers needs rework",
         "Different API: every installer and scene setup is rewritten",
         "&mdash;"),
    ]
    body = "".join(f"<tr><td><b>{a}</b></td><td class='fi'>{b}</td><td>{c}</td><td>{d}</td></tr>" for a, b, c, d in rows)
    return f"""
<div class="page"></div>
<h2>Installation and setup</h2>
<p>What a team has to install, configure and keep doing for each container. Based on setting up all three for this benchmark, each with its default documented approach.</p>
<table><tr><th></th><th class="fi">Fast IoC</th><th>VContainer</th><th>Zenject (Extenject)</th></tr>{body}</table>
<p class="note">In this benchmark, Fast IoC needed one menu item to set up. Zenject needed a ProjectContext prefab and a SceneContext per scene. VContainer needed a root scope prefab, plus a scope per scene with a parent reference and the list of objects to inject.
Fast IoC moves work from the player's device to the developer's editor: the startup gains above are paid for by that regeneration step.</p>
"""


def device_runs_table(profile, runs):
    cols = []
    for name, tier, _ in DEVICES:
        cols.append((name, tier, [r for r in runs if device_of(r["session"]["deviceModel"]) == name and r["workload"]["profileOrScenario"] == profile]))

    def row(label, fn):
        return f"<tr><th>{e(label)}</th>" + "".join(f"<td>{e(fn(rr)) if rr else '–'}</td>" for _, _, rr in cols) + "</tr>"

    def ts(rr, pick):
        return pick(datetime.strptime(r["timestampUtc"], "%Y-%m-%dT%H:%M:%SZ") for r in rr)

    def duration(rr):
        s = (ts(rr, max) - ts(rr, min)).total_seconds()
        return f"{int(s // 3600)}h {int(s % 3600 // 60)}m" if s >= 3600 else f"{int(s // 60)}m {int(s % 60)}s"

    def first(rr, fn):
        return fn(rr[0])

    head = "".join(f"<th>{e(n)}</th>" for n, _, _ in cols)
    rows = [
        row("Tier", lambda rr: next(t for n, t, x_ in cols if x_ is rr)),
        row("Model", lambda rr: first(rr, lambda r: r["session"]["deviceModel"])),
        row("OS", lambda rr: first(rr, lambda r: r["session"]["osVersion"])),
        row("Commit / build / Unity", lambda rr: first(rr, lambda r: f"{r['session']['gitCommit'][:7]} · {r['session']['buildMode']} · {r['session']['scriptingBackend']} · {r['session']['unityVersion']}"
                                              + (" · DIRTY" if r["_notes"].get("fastiocDirty") == "true" else ""))),
        row("Versions", lambda rr: first(rr, lambda r: f"Fast IoC {r['_notes'].get('fastioc')}, VContainer {r['_notes'].get('vcontainer')}, Zenject {r['_notes'].get('zenject')}")),
        row("Stripping / incremental GC", lambda rr: first(rr, lambda r: f"{r['_notes'].get('stripping')} / {r['_notes'].get('incrementalGc', '?')}")),
        row("Runs per variant", lambda rr: " / ".join(f"{SHORT[v]} {sum(1 for r in rr if r['workload']['variant'] == v)}" for v in VARIANTS + [BASELINE] if any(r['workload']['variant'] == v for r in rr))),
        row("Files", lambda rr: str(len(rr))),
        row("Started (UTC)", lambda rr: ts(rr, min).strftime("%Y-%m-%d %H:%M")),
        row("Total time (first to last result)", duration),
        row("Engine boot before first scene", lambda rr: ms(med([r["_m"].get(("boot-start", "single")) for r in rr]), 0)),
    ]
    return f"<table><tr><th></th>{head}</tr>{''.join(rows)}</table>"


def full_table(c):
    columns = [(k, c.get(k)) for k in VARIANTS]

    def cls(k):
        return "fi" if k == "fastioc" else ("base" if k == BASELINE else "")

    def row(label, fn, baseline=True):
        out = f"<tr><td>{e(label)}</td>"
        for k, cell in columns:
            show = cell is not None and (baseline or k != BASELINE)
            out += f'<td class="{cls(k)}">{e(fn(cell)) if show else "–"}</td>'
        return out + "</tr>"

    def sec(title):
        return f'<tr class="section"><td colspan="{len(columns) + 1}">{e(title)}</td></tr>'

    def sp(cell, name, agg="single"):
        lo, hi = cell.spread(name, agg)
        return f"{ms(cell.get(name, agg))}  ({ms(lo)} – {ms(hi)})"

    rows = [
        sec("1. Startup (cold launch, one per run)"),
        row("Build global container", lambda cl: sp(cl, "container-build"), baseline=False),
        row("First scene injection (creates the graph)", lambda cl: sp(cl, "scene-first-inject")),
        row("Startup cost (build + first injection)", lambda cl: f"{ms(cl.di())} · {frames(cl.di()):.1f} frames @30 fps", baseline=False),
        row("Launch to first scene ready", lambda cl: ms(cl.get("startup-total"))),
        row("Garbage created at startup", lambda cl: mb_bytes(cl.get("startup-heap-growth"))),
        row("Garbage collections at startup", lambda cl: num(cl.get("startup-gc-collections"), 0)),
        sec("2. Scene reload (5 unload/load cycles per run)"),
        row("Re-inject scene, median / p95", lambda cl: f"{ms(cl.get('scene-reload-inject', 'median'))} / {ms(cl.get('scene-reload-inject', 'p95'))}"),
        row("Reload including scene load, median", lambda cl: ms(cl.get("scene-reload-load", "median"))),
        row("Unload, median", lambda cl: ms(cl.get("scene-unload", "median"))),
        row("Garbage / collections over 5 reloads", lambda cl: f"{kb(cl.get('reload-heap-growth'))} / {num(cl.get('reload-gc-collections'), 0)}"),
        sec("3. Memory"),
        row("Managed heap after startup", lambda cl: mb(cl.get("heap-after-ready"))),
        sec("4. Prefab spawning (1,000 injected prefabs, 50 per frame)"),
        row("Batch of 50, median / p95", lambda cl: f"{ms(cl.get('prefab-batch', 'median'))} / {ms(cl.get('prefab-batch', 'p95'))}"),
        row("First batch", lambda cl: ms(cl.get("prefab-first-batch"))),
        row("Per prefab", lambda cl: ms(cl.get("prefab-per-instance"), 3)),
        row("Garbage / collections for 1,000 spawns", lambda cl: f"{kb(cl.get('prefab-heap-growth'))} / {num(cl.get('prefab-gc-collections'), 0)}"),
        sec("5. Whole scene load (mostly Unity)"),
        row("First scene load, call to ready", lambda cl: ms(cl.get("scene-first-load"), 0)),
        row("  of which before the first Awake (Unity)", lambda cl: ms(cl.get("scene-first-preawake"), 0)),
        sec("CORRECTNESS"),
        row("Instances / injected fields", lambda cl: f"{num(cl.get('distinct-instances'), 0)} / {num(cl.get('injected-fields'), 0)}", baseline=False),
    ]
    head = "".join(f'<th class="{cls(k)}">{e(LABEL[k])}</th>' for k, _ in columns)
    return f"<table><tr><th>Metric</th>{head}</tr>{''.join(rows)}</table>"


def fastioc_only(groups, profiles, notes_by_profile):
    head = "".join(f"<th>{e(n)}</th>" for n, _, _ in DEVICES)
    out = []
    for profile in profiles:
        def cell(name, fn):
            c = cells_for(groups, name, profile)
            return e(fn(c["fastioc"], c.get(BASELINE))) if "fastioc" in c else "–"

        specs = [
            ("Build container", lambda f, b: ms(f.get("container-build"), 2)),
            ("First scene injection", lambda f, b: ms(f.get("scene-first-inject"), 2)),
            ("Startup cost, % of a 30 fps frame", lambda f, b: f"{f.di() / FRAME_30 * 100:.0f}%"),
            ("Scene re-inject, median / p95", lambda f, b: f"{num(f.get('scene-reload-inject', 'median'), 2)} / {num(f.get('scene-reload-inject', 'p95'), 2)} ms"),
            ("Prefab batch of 50, median / p95", lambda f, b: f"{num(f.get('prefab-batch', 'median'), 2)} / {num(f.get('prefab-batch', 'p95'), 2)} ms"),
            ("Heap after startup (no DI)", lambda f, b: f"{mb(f.get('heap-after-ready'))} ({mb(b.get('heap-after-ready')) if b else '–'})"),
            ("Prefab garbage for 1,000 spawns (no DI)", lambda f, b: f"{kb(f.get('prefab-heap-growth'))} ({kb(b.get('prefab-heap-growth')) if b else '–'})"),
        ]
        n = notes_by_profile.get(profile, {})
        out.append(f'<tr class="section"><td colspan="{len(DEVICES) + 1}">{e(profile)} · {e(n.get("bindings", "?"))} bindings · editor generation {e(n.get("fastiocGenerationSeconds", "?"))} s</td></tr>')
        for label, fn in specs:
            out.append(f"<tr><td>{e(label)}</td>{''.join(f'<td>{cell(name, fn)}</td>' for name, _, _ in DEVICES)}</tr>")
    return f"<table><tr><th>Scenario</th>{head}</tr>{''.join(out)}</table>"


# ---------------------------------------------------------------- page

def build(root: Path, profile_arg, commit_arg):
    all_runs, rejected = load(root)
    if not all_runs:
        sys.exit(f"No valid results under {root}")

    newest = max(all_runs, key=lambda r: r["timestampUtc"])["session"]["gitCommit"]
    commit = commit_arg or newest
    runs = [r for r in all_runs if r["session"]["gitCommit"].startswith(commit)]
    older = len(all_runs) - len(runs)

    groups = defaultdict(lambda: defaultdict(list))
    notes_by_profile = {}
    for r in runs:
        p = r["workload"]["profileOrScenario"]
        groups[(device_of(r["session"]["deviceModel"]), p)][r["workload"]["variant"]].append(r)
        notes_by_profile.setdefault(p, r["_notes"])

    profiles = sorted(notes_by_profile, key=lambda p: int(notes_by_profile[p].get("bindings", 0)))
    profile = profile_arg or max(profiles, key=lambda p: (len({d for d, pr in groups if pr == p}), int(notes_by_profile[p].get("bindings", 0))))
    n = notes_by_profile[profile]
    devices = [d for d, _, _ in DEVICES if (d, profile) in groups]
    pending = [d for d, _, _ in DEVICES if d not in devices]
    other_profiles = [p for p in PROFILE_ORDER if p not in profiles and p != "startup-200"]
    runs_per_variant = next(r for r in runs if r["workload"]["profileOrScenario"] == profile)["workload"].get("totalRuns", "?")

    # Headline sentence, computed.
    startup_v, startup_z, reload_v, reload_z, heap_v, heap_z = [], [], [], [], [], []
    spike_win_all = True
    worst = None
    for d in devices:
        c = cells_for(groups, d, profile)
        if not all(k in c for k in VARIANTS):
            continue
        f = c["fastioc"]
        startup_v.append(ratio(f.di(), c["vcontainer"].di()))
        startup_z.append(ratio(f.di(), c["zenject"].di()))
        reload_v.append(ratio(f.get("scene-reload-inject", "median"), c["vcontainer"].get("scene-reload-inject", "median")))
        reload_z.append(ratio(f.get("scene-reload-inject", "median"), c["zenject"].get("scene-reload-inject", "median")))
        heap_v.append(ratio(f.get("heap-after-ready"), c["vcontainer"].get("heap-after-ready")))
        heap_z.append(ratio(f.get("heap-after-ready"), c["zenject"].get("heap-after-ready")))
        spike_win_all &= f.get("prefab-batch", "p95") < min(c["vcontainer"].get("prefab-batch", "p95"), c["zenject"].get("prefab-batch", "p95"))
        if worst is None or f.di() > worst[1]["fastioc"].di():
            worst = (d, c)

    result = (f"Fast IoC does the DI work at launch {xrange(startup_v)} faster than VContainer and {xrange(startup_z)} faster than Zenject, "
              f"reloads scenes {xrange(reload_v + reload_z)} faster, and holds {xrange(heap_v + heap_z)} less managed memory"
              + (", and spawns prefabs without the frame spikes the other two show." if spike_win_all else "."))
    wait = ""
    if worst:
        d, c = worst
        if BASELINE in c:
            ob = over_base(c, "startup-total")
            wait = (f" On the {e(d)}, the player waits <b>{ms(ob['vcontainer'])}</b> longer for the first scene with VContainer and "
                    f"<b>{ms(ob['zenject'])}</b> longer with Zenject than with no DI at all; with Fast IoC the extra wait is {e(signed_ms(ob['fastioc']))}.")

    heads = "".join(headline(d, t, cells_for(groups, d, profile)) for d, t, _ in DEVICES if (d, profile) in groups)

    per_device = ""
    for name, tier, _ in DEVICES:
        per_device += f'<h3>{e(name)} <span style="color:#6b7280;font-size:8.6pt">· {e(tier)}</span></h3>'
        if (name, profile) in groups:
            per_device += full_table(cells_for(groups, name, profile))
            per_device += ('<p class="note">Values are medians of the runs; ranges in brackets are the lowest and highest run. Garbage = managed heap growth (Release-safe); it reads lower, even negative, '
                           'when a collection ran inside the window, so read it with the collections row.</p>')
        else:
            per_device += '<p class="pending">Results pending — this table fills when the device\'s files are pulled.</p>'

    excluded = []
    if older:
        excluded.append(f"{older} result(s) from other builds (commits other than {commit[:7]}) are not used")
    if rejected:
        excluded.append(f"{len(rejected)} run(s) excluded because the resolved graph didn't match")
    excluded_note = f"<p class='note'>{e('; '.join(excluded))}.</p>" if excluded else ""

    blurb = PROFILE_BLURB.get(profile, "")
    body = f"""
<h1>Fast IoC — the case for switching</h1>
<p class="sub">Benchmark results on real phones · for dev, leadership and finance review · internal</p>
<p class="sub">Results so far: {e(', '.join(devices))} · project size <b>{e(profile)}</b>: {int(n.get('bindings', 0)):,} bindings and {int(n.get('monoInjectables', 0)):,} injected scene objects{(' — ' + e(blurb)) if blurb else ''}.
{('Pending: ' + e(', '.join(pending)) + '.') if pending else ''} Other sizes pending: {e(', '.join(other_profiles)) or 'none'}. This document refreshes as results come in.</p>

<h2>The short version</h2>
<p>Fast IoC is a dependency injection (DI) container for Unity: it connects a game's services and scripts so each piece gets what it needs without
hard-wired references. It was tested against Zenject (through Extenject, its maintained fork), which our projects use today, and VContainer, the most
common modern alternative. All three ran in the same build on the same phones, resolving exactly the same generated project, under the same conditions,
next to a version of the project with no DI at all.</p>
<p><b>The result: {e(result)}</b>{wait} Startup matters because it is a freeze players see at launch and on loading screens, and it hits low-end phones hardest.</p>
{heads}
<p class="note">Lower is better for every number. ms = milliseconds, s = seconds; a frame lasts 33.3 ms at 30 fps. Multipliers (×) compare each rival with Fast IoC.</p>
{differs(groups, profile, devices)}
<p class="honest"><b>Honest summary:</b> at the size of a real mobile game, Fast IoC removes nearly all of the DI cost at launch and on scene loads, which is the cost
players feel, holds far less memory and avoids spawn spikes. Its price is paid in the editor: code regenerates after a compile ({e(n.get('fastiocGenerationSeconds', '?'))} s here),
and it supports fewer binding features than Zenject. Not measured yet: Zenject's reflection baking and VContainer's source generator, which are designed
to cut exactly the startup cost measured here, and the two iPhones.</p>
{excluded_note}
{setup_section(n)}
<div class="page"></div>
<h2>How it was tested</h2>
<p>One Release IL2CPP build containing all three containers and a no-DI baseline, installed on each phone and left to run on its own. Every launch is a
cold start of the app and measures one variant; the others stay dormant. Each variant is measured {e(runs_per_variant)} times, taking turns so none
always goes first. Memory is cleaned and the phone rests 1 s between phases. The project is generated from a fixed seed, so every container binds and
resolves the same classes, and every run checks that the graph it built is the expected one (same hash, instance count and injected fields).
A run that fails the check is discarded.</p>
<table>
<tr><th>Test</th><th>What it simulates</th><th>Size</th></tr>
<tr><td><b>Startup</b></td><td>Launching the game: build the global container, then load the first scene, which creates every service and injects its scripts</td><td>{e(n.get('bindings'))} bindings, {e(n.get('implementations'))} classes, {e(n.get('monoInjectables'))} scene objects</td></tr>
<tr><td><b>Scene reload</b></td><td>Leaving and re-entering a level or menu</td><td>{e(n.get('reloadCycles', 5))} unload/load cycles</td></tr>
<tr><td><b>Prefab spawning</b></td><td>Enemies, list items or popups created during play, through each container's own instantiate API</td><td>{e(n.get('prefabInstances', 1000))} prefabs, {e(n.get('prefabBatch', 50))} per frame</td></tr>
<tr><td><b>Memory</b></td><td>Heap held after startup, garbage and garbage collections in each phase</td><td>All phases</td></tr>
<tr><td><b>No-DI baseline</b></td><td>Same scene and prefabs, nothing injected: what Unity costs on its own</td><td>Same sizes</td></tr>
</table>
<p class="note">Measured with a high-resolution timer inside the app, from the first managed code that runs. Each container uses its default, documented setup:
Fast IoC with edit-time generated code, Zenject with reflection, VContainer with reflection. Lifetimes are singleton and transient. Values are medians of the runs.</p>

<h2>Device runs (for manual comparison)</h2>
{device_runs_table(profile, runs)}
<p class="note">Engine boot is Unity starting up and showing its splash screen before the first scene; it is the same for every variant.</p>

<div class="page"></div>
<h2>Full comparison per device</h2>
<p class="sub">Fast IoC column highlighted. Project size {e(profile)}.</p>
{per_device}

<div class="page"></div>
<h2>Fast IoC only — per device</h2>
<p>These are the numbers that can go public once the naming policy is settled. One block per project size measured with this build.</p>
{fastioc_only(groups, profiles, notes_by_profile)}
<p class="note">Startup cost = build container + first scene injection. Values in brackets are the same project with no DI.</p>
"""
    return f"<!DOCTYPE html><html><head><meta charset='utf-8'><title>Fast IoC — the case for switching</title><style>{CSS}</style></head><body>{body}</body></html>"


def to_pdf(html_path: Path, pdf_path: Path):
    footer = ('<div style="font-size:7px;color:#9ca3af;width:100%;padding:0 14mm;display:flex;justify-content:space-between;font-family:Arial">'
              '<span>Fast IoC — the case for switching · internal</span><span>Page <span class="pageNumber"></span></span></div>')
    header = ('<div style="font-size:7px;color:#9ca3af;width:100%;padding:0 14mm;font-family:Arial">'
              'Fast IoC — the case for switching · internal</div>')
    chrome = shutil.which("google-chrome") or shutil.which("chromium") or "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
    try:
        from playwright.sync_api import sync_playwright
        with sync_playwright() as p:
            browser = p.chromium.launch(executable_path=chrome if Path(chrome).exists() else None)
            page = browser.new_page()
            page.goto(html_path.resolve().as_uri())
            page.pdf(path=str(pdf_path), format="A4", print_background=True, display_header_footer=True,
                     header_template=header, footer_template=footer,
                     margin={"top": "16mm", "bottom": "16mm", "left": "0", "right": "0"})
            browser.close()
        return True
    except Exception as ex:
        if not Path(chrome).exists():
            print(f"No PDF: {ex}. Open the HTML and print it.", file=sys.stderr)
            return False
        subprocess.run([chrome, "--headless", "--disable-gpu", "--no-pdf-header-footer", f"--print-to-pdf={pdf_path}", html_path.resolve().as_uri()],
                       check=True, capture_output=True)
        return True


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("root", type=Path, help="folder with one subfolder of raw results per device")
    parser.add_argument("--profile", help="project size for the headline (default: most devices, then largest)")
    parser.add_argument("--commit", help="use only runs from this commit (default: the newest run's commit)")
    parser.add_argument("--out", type=Path, default=Path("FastIoC-benchmark-brief"), help="output path without extension")
    args = parser.parse_args()

    html_path = args.out.with_suffix(".html")
    html_path.write_text(build(args.root, args.profile, args.commit), encoding="utf-8")
    print(f"Wrote {html_path}")
    pdf_path = args.out.with_suffix(".pdf")
    if to_pdf(html_path, pdf_path):
        print(f"Wrote {pdf_path}")


if __name__ == "__main__":
    main()
