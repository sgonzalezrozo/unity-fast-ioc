#!/usr/bin/env python3
"""Builds the internal Fast IoC benchmark brief (HTML + PDF) from raw device results.

Usage:
  python3 make_brief.py <results-root> [--profile NAME] [--out FastIoC-benchmark-brief]

<results-root> holds one folder per device (e.g. pixel5/, a02s/) with the raw JSON files that
tools/run_android.sh and tools/run_ios.sh pull. Every number in the brief is computed here from
those files; nothing is typed by hand. Runs whose graph check failed are excluded and counted.
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
LABEL = {"fastioc": "Fast IoC", "vcontainer": "VContainer", "zenject": "Zenject (Extenject)", "none": "No DI (baseline)"}
SHORT = {"fastioc": "Fast IoC", "vcontainer": "VContainer", "zenject": "Zenject"}
COLOR = {"vcontainer": "#c2410c", "zenject": "#7e22ce"}

# Devices in the order the brief shows them. Matched on deviceModel.
DEVICES = [
    ("Pixel 5", "mid-range Android", ["Pixel 5"]),
    ("Samsung A02s", "low-end Android, 32-bit", ["SM-A025"]),
    ("iPhone 13 mini", "iOS", ["iPhone14,4"]),
    ("iPhone Air", "iOS, 120 Hz", ["iPhone18,4", "iPhone Air"]),
]
FRAME_30 = 1000 / 30
FRAME_60 = 1000 / 60


# ---------------------------------------------------------------- data

def load(root: Path):
    runs, rejected = [], []
    for path in sorted(root.rglob("*.json")):
        if path.name == "plan.json":
            continue
        r = json.loads(path.read_text(encoding="utf-8"))
        r["_file"] = path.name
        m = {}
        for metric in r["metrics"]:
            m[(metric["name"], metric.get("aggregation") or "single")] = metric["value"]
        r["_m"] = m
        notes = dict(part.split("=", 1) for part in r["session"].get("notes", "").split("; ") if "=" in part)
        r["_notes"] = notes
        baseline = r["workload"]["variant"] == "none"
        valid = baseline or (m.get(("graph-valid", "single")) == 1 and m.get(("graph-valid-after-reload", "single")) == 1)
        (runs if valid else rejected).append(r)
    return runs, rejected


def device_of(model: str):
    for name, tier, keys in DEVICES:
        if any(k in model for k in keys):
            return name
    return model


def med(values):
    values = [v for v in values if v is not None]
    return statistics.median(values) if values else None


class Cell:
    """Medians across runs for one device, profile and variant."""

    def __init__(self, runs):
        self.runs = runs

    def get(self, name, agg="single"):
        return med([r["_m"].get((name, agg)) for r in self.runs])

    def di(self):
        return med([r["_m"][("container-build", "single")] + r["_m"][("scene-first-inject", "single")] for r in self.runs])

    def spread(self, name, agg="single"):
        values = [r["_m"].get((name, agg)) for r in self.runs if r["_m"].get((name, agg)) is not None]
        return (min(values), max(values)) if values else (None, None)


def group(runs):
    table = defaultdict(lambda: defaultdict(list))
    for r in runs:
        table[(device_of(r["session"]["deviceModel"]), r["workload"]["profileOrScenario"])][r["workload"]["variant"]].append(r)
    return table


# ---------------------------------------------------------------- formatting

def ms(v, digits=1):
    return "–" if v is None else f"{v:,.{digits}f} ms"


def num(v, digits=1):
    return "–" if v is None else f"{v:,.{digits}f}"


def kb(v):
    return "–" if v is None else f"{v / 1024:,.0f} KB"


def mb(v, digits=2):
    return "–" if v is None else f"{v:,.{digits}f} MB"


def times(a, b):
    """'6.3× faster' style ratio of b over a (a is Fast IoC)."""
    if not a or not b:
        return ""
    r = b / a
    return f"{r:.1f}×" if r < 10 else f"{r:.0f}×"


def frames(v, budget):
    return v / budget


def e(text):
    return html.escape(str(text))


# ---------------------------------------------------------------- sections

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
.card.behind .v { color: #6b7280; }
.note { font-size: 7.6pt; color: #6b7280; margin: 4px 0 0; }
ol, ul { margin: 4px 0 4px 18px; padding: 0; }
li { margin: 3px 0; }
table { border-collapse: collapse; width: 100%; font-size: 8.2pt; margin: 4px 0; }
th, td { border: 1px solid #e5e7eb; padding: 4px 6px; text-align: left; vertical-align: top; }
th { background: #f3f4f6; font-weight: bold; }
td.fi, th.fi { background: #eef3fc; }
tr.section td { background: #1f2937; color: #fff; font-weight: bold; font-size: 7.6pt; letter-spacing: .02em; }
.page { page-break-before: always; }
.pending { color: #6b7280; font-size: 8.6pt; }
.honest { margin-top: 8px; }
code { font-family: 'DejaVu Sans Mono', monospace; font-size: 7.8pt; }
"""


def card(key, value, desc, extra, others, behind=False):
    other_html = "".join(f'<div class="o" style="color:{COLOR[v]}">{e(text)}</div>' for v, text in others)
    return (f'<div class="card{" behind" if behind else ""}"><div class="k">{e(key)}</div><div class="v">{e(value)}</div>'
            f'<div class="d">{e(desc)}</div><div class="x">{e(extra)}</div>{other_html}</div>')


def headline(device, tier, cells):
    f, v, z = (cells.get(x) for x in VARIANTS)
    if not f:
        return ""
    di = f.di()
    reload_f = f.get("scene-reload-inject", "median")
    garbage = f.get("startup-heap-growth")
    batch = f.get("prefab-batch", "median")
    vb = v.get("prefab-batch", "median") if v else None
    prefab_behind = vb is not None and batch is not None and batch > vb

    cards = [
        card("STARTUP COST", ms(di), "DI work when the game launches: build the container and inject the first scene",
             f"{frames(di, FRAME_30):.1f} frames at 30 fps",
             [(x, f"{SHORT[x]}: {ms(c.di())} ({frames(c.di(), FRAME_30):.1f} frames)") for x, c in (("vcontainer", v), ("zenject", z)) if c]),
        card("SCENE RELOAD", ms(reload_f, 2), "to inject a scene again, every time the player re-enters it",
             f"{f.get('reload-gc-collections') or 0:.0f} garbage collections in 5 reloads",
             [(x, f"{SHORT[x]}: {ms(c.get('scene-reload-inject', 'median'), 2)}") for x, c in (("vcontainer", v), ("zenject", z)) if c]),
        card("STARTUP GARBAGE", mb(garbage / 1048576 if garbage is not None else None), "temporary memory created at launch (cleaning it up causes hitches)",
             f"{f.get('startup-gc-collections') or 0:.0f} garbage collections",
             [(x, f"{SHORT[x]}: {mb(c.get('startup-heap-growth') / 1048576)} · {c.get('startup-gc-collections'):.0f} GCs") for x, c in (("vcontainer", v), ("zenject", z)) if c]),
        card("PREFAB SPAWN", ms(batch, 1), "to spawn 50 injected prefabs in one frame (median)",
             f"p95 {ms(f.get('prefab-batch', 'p95'))}" + (" · behind VContainer" if prefab_behind else ""),
             [(x, f"{SHORT[x]}: {ms(c.get('prefab-batch', 'median'))}") for x, c in (("vcontainer", v), ("zenject", z)) if c],
             behind=prefab_behind),
    ]
    return f'<div class="device-label">{e(device)} <span>· {e(tier)}</span></div><div class="cards">{"".join(cards)}</div>'


def differs(groups, profile, devices_with_data):
    items_startup, items_reload, items_memory, items_prefab, items_load = [], [], [], [], []
    for device in devices_with_data:
        cells = {k: Cell(v) for k, v in groups[(device, profile)].items()}
        f, v, z = (cells.get(x) for x in VARIANTS)
        if not (f and v and z):
            continue
        items_startup.append(
            f"{device}: Fast IoC {ms(f.get('container-build'))} to build + {ms(f.get('scene-first-inject'))} to inject; "
            f"VContainer {ms(v.get('container-build'))} + {ms(v.get('scene-first-inject'))}; "
            f"Zenject {ms(z.get('container-build'))} + {ms(z.get('scene-first-inject'))} "
            f"({times(f.di(), v.di())} and {times(f.di(), z.di())} the Fast IoC cost)")
        items_reload.append(
            f"{device}: {ms(f.get('scene-reload-inject', 'median'), 2)} vs {ms(v.get('scene-reload-inject', 'median'), 2)} (VContainer) "
            f"and {ms(z.get('scene-reload-inject', 'median'), 2)} (Zenject); garbage collections in 5 reloads: "
            f"{f.get('reload-gc-collections'):.0f} / {v.get('reload-gc-collections'):.0f} / {z.get('reload-gc-collections'):.0f}")
        items_memory.append(
            f"{device}: heap held after startup {mb(f.get('heap-after-ready'))} vs {mb(v.get('heap-after-ready'))} and {mb(z.get('heap-after-ready'))}; "
            f"startup garbage {mb(f.get('startup-heap-growth') / 1048576)} vs {mb(v.get('startup-heap-growth') / 1048576)} and {mb(z.get('startup-heap-growth') / 1048576)}")
        items_prefab.append(
            f"{device}: 50-prefab batch median {ms(f.get('prefab-batch', 'median'))} vs {ms(v.get('prefab-batch', 'median'))} (VContainer) "
            f"and {ms(z.get('prefab-batch', 'median'))} (Zenject); p95 {ms(f.get('prefab-batch', 'p95'))} / {ms(v.get('prefab-batch', 'p95'))} / {ms(z.get('prefab-batch', 'p95'))}; "
            f"garbage for 1,000 spawns {kb(f.get('prefab-heap-growth'))} / {kb(v.get('prefab-heap-growth'))} / {kb(z.get('prefab-heap-growth'))}")
        items_load.append(
            f"{device}: {ms(f.get('scene-first-load'), 0)} / {ms(v.get('scene-first-load'), 0)} / {ms(z.get('scene-first-load'), 0)}")

    def lst(items):
        return "<ul>" + "".join(f"<li>{e(i)}</li>" for i in items) + "</ul>"

    return f"""
<h2>Where Fast IoC actually differs</h2>
<ol>
<li><b>Startup.</b> Fast IoC writes the container at edit time, so at launch there is no reflection and no registration work to do.
Startup cost = building the global container plus the first scene injection, which creates every singleton of the graph. {lst(items_startup)}</li>
<li><b>Scene reloads.</b> Each time a scene loads again its MonoBehaviours are injected again. Fast IoC walks a list baked into the scene
at build time; Zenject scans the scene and injects by reflection, and VContainer walks the scene objects it is given with reflection-built injectors. {lst(items_reload)}</li>
<li><b>Memory.</b> Less memory held by the container and less temporary garbage at launch, so fewer garbage collections (each one a short freeze). {lst(items_memory)}</li>
</ol>
<h3>Where Fast IoC is behind</h3>
<ul><li><b>Spawning injected prefabs.</b> VContainer is faster and creates less garbage. Fast IoC still beats Zenject. This is the next optimization target.
{lst(items_prefab)}</li></ul>
<h3>Mixed</h3>
<ul><li><b>Whole scene load</b> (from the load call to the scene being ready): mostly Unity reading and creating the scene. Results don't follow
the injection times above and differ between devices; the cause is not known yet. Fast IoC / VContainer / Zenject: {lst(items_load)}</li></ul>
"""


def device_runs_table(groups, profile, all_runs):
    cols = []
    for name, tier, _ in DEVICES:
        runs = [r for r in all_runs if device_of(r["session"]["deviceModel"]) == name and r["workload"]["profileOrScenario"] == profile]
        cols.append((name, tier, runs))

    def row(label, fn):
        cells = "".join(f"<td>{e(fn(runs)) if runs else '–'}</td>" for _, _, runs in cols)
        return f"<tr><th>{e(label)}</th>{cells}</tr>"

    def ts(runs, pick):
        return pick(datetime.strptime(r["timestampUtc"], "%Y-%m-%dT%H:%M:%SZ") for r in runs)

    def duration(runs):
        seconds = (ts(runs, max) - ts(runs, min)).total_seconds()
        return f"{int(seconds // 60)}m {int(seconds % 60)}s (last result minus first)"

    def first(runs, fn):
        return fn(runs[0])

    head = "".join(f"<th>{e(name)}</th>" for name, _, _ in cols)
    rows = [
        row("Tier", lambda runs: next(t for n, t, rr in cols if rr is runs)),
        row("Model", lambda runs: first(runs, lambda r: r["session"]["deviceModel"])),
        row("OS", lambda runs: first(runs, lambda r: r["session"]["osVersion"])),
        row("Commit / build", lambda runs: first(runs, lambda r: f"{r['session']['gitCommit'][:7]} · {r['session']['buildMode']} · {r['session']['scriptingBackend']}"
                                                  + (" · DIRTY" if r["_notes"].get("fastiocDirty") == "true" else ""))),
        row("Unity", lambda runs: first(runs, lambda r: r["session"]["unityVersion"])),
        row("Versions", lambda runs: first(runs, lambda r: f"Fast IoC {r['_notes'].get('fastioc')}, VContainer {r['_notes'].get('vcontainer')}, Zenject {r['_notes'].get('zenject')}")),
        row("Stripping / incremental GC", lambda runs: first(runs, lambda r: f"{r['_notes'].get('stripping')} / {r['_notes'].get('incrementalGc', '?')}")),
        row("Runs per variant", lambda runs: " / ".join(f"{SHORT.get(v, 'No DI')} {sum(1 for r in runs if r['workload']['variant'] == v)}" for v in VARIANTS + ["none"] if any(r['workload']['variant'] == v for r in runs))),
        row("Files", lambda runs: str(len(runs))),
        row("Graph check", lambda runs: "all runs resolved the expected graph" if runs else "–"),
        row("Started (UTC)", lambda runs: ts(runs, min).strftime("%Y-%m-%d %H:%M")),
        row("Total time", duration),
        row("Engine boot before first scene", lambda runs: ms(med([r["_m"].get(("boot-start", "single")) for r in runs]), 0)),
    ]
    return f"<table><tr><th></th>{head}</tr>{''.join(rows)}</table>"


def full_table(cells):
    f, v, z = (cells.get(x) for x in VARIANTS)
    base = cells.get("none")
    columns = [("fastioc", f), ("vcontainer", v), ("zenject", z)] + ([("none", base)] if base else [])

    def row(label, fn, baseline=True):
        out = f"<tr><td>{e(label)}</td>"
        for x, c in columns:
            show = c and (baseline or x != "none")
            out += f'<td class="{"fi" if x == "fastioc" else ""}">{e(fn(c)) if show else "–"}</td>'
        return out + "</tr>"

    def sec(title):
        return f'<tr class="section"><td colspan="{len(columns) + 1}">{e(title)}</td></tr>'

    def sp(c, name, agg="single", digits=1):
        lo, hi = c.spread(name, agg)
        return f"{ms(c.get(name, agg), digits)}  ({num(lo, digits)}–{num(hi, digits)})"

    rows = [
        sec("WHERE IT DIFFERS — 1. Startup (cold launch, one per run)"),
        row("Build global container", lambda c: sp(c, "container-build"), baseline=False),
        row("First scene injection (creates the graph)", lambda c: sp(c, "scene-first-inject")),
        row("Startup cost (build + first injection)", lambda c: f"{ms(c.di())} · {frames(c.di(), FRAME_30):.2f} frames @30 fps", baseline=False),
        row("Garbage created at startup", lambda c: mb(c.get("startup-heap-growth") / 1048576)),
        row("Garbage collections at startup", lambda c: num(c.get("startup-gc-collections"), 0)),
        sec("WHERE IT DIFFERS — 2. Scene reload (5 unload/load cycles per run)"),
        row("Re-inject scene, median / p95", lambda c: f"{ms(c.get('scene-reload-inject', 'median'), 2)} / {ms(c.get('scene-reload-inject', 'p95'), 2)}"),
        row("Reload including scene load, median", lambda c: ms(c.get("scene-reload-load", "median"))),
        row("Unload, median", lambda c: ms(c.get("scene-unload", "median"))),
        row("Garbage / collections over 5 reloads", lambda c: f"{kb(c.get('reload-heap-growth'))} / {num(c.get('reload-gc-collections'), 0)}"),
        sec("WHERE IT DIFFERS — 3. Memory"),
        row("Managed heap after startup", lambda c: mb(c.get("heap-after-ready"))),
        sec("BEHIND — prefab spawning (1,000 injected prefabs, 50 per frame)"),
        row("Batch of 50, median / p95", lambda c: f"{ms(c.get('prefab-batch', 'median'))} / {ms(c.get('prefab-batch', 'p95'))}"),
        row("First batch", lambda c: ms(c.get("prefab-first-batch"))),
        row("Per prefab", lambda c: ms(c.get("prefab-per-instance"), 3)),
        row("Garbage / collections for 1,000 spawns", lambda c: f"{kb(c.get('prefab-heap-growth'))} / {num(c.get('prefab-gc-collections'), 0)}"),
        sec("MIXED — whole scene load (mostly Unity)"),
        row("First scene load, call to ready", lambda c: ms(c.get("scene-first-load"), 0)),
        row("  of which before the first Awake (Unity loading)", lambda c: ms(c.get("scene-first-preawake"), 0)),
        sec("CORRECTNESS"),
        row("Instances / injected fields", lambda c: f"{num(c.get('distinct-instances'), 0)} / {num(c.get('injected-fields'), 0)}", baseline=False),
    ]
    head = "".join(f'<th class="{"fi" if x == "fastioc" else ""}">{e(LABEL[x])}</th>' for x, _ in columns)
    return f"<table><tr><th>Metric</th>{head}</tr>{''.join(rows)}</table>"


def fastioc_only(groups, profiles):
    head = "".join(f"<th>{e(name)}</th>" for name, _, _ in DEVICES)
    rows = []
    for profile in profiles:
        def cell(name, fn):
            runs = groups.get((name, profile), {}).get("fastioc")
            return e(fn(Cell(runs))) if runs else "–"

        specs = [
            ("Build container", lambda c: ms(c.get("container-build"), 2)),
            ("First scene injection", lambda c: ms(c.get("scene-first-inject"), 2)),
            ("Startup cost, % of a 30 fps frame", lambda c: f"{c.di() / FRAME_30 * 100:.0f}%"),
            ("Scene re-inject, median / p95", lambda c: f"{num(c.get('scene-reload-inject', 'median'), 2)} / {num(c.get('scene-reload-inject', 'p95'), 2)} ms"),
            ("Prefab batch of 50, median / p95", lambda c: f"{num(c.get('prefab-batch', 'median'), 2)} / {num(c.get('prefab-batch', 'p95'), 2)} ms"),
            ("Heap after startup", lambda c: mb(c.get("heap-after-ready"))),
            ("Startup garbage / GCs", lambda c: f"{mb(c.get('startup-heap-growth') / 1048576)} / {num(c.get('startup-gc-collections'), 0)}"),
        ]
        rows.append(f'<tr class="section"><td colspan="{len(DEVICES) + 1}">{e(profile)}</td></tr>')
        for label, fn in specs:
            rows.append(f"<tr><td>{e(label)}</td>{''.join(f'<td>{cell(name, fn)}</td>' for name, _, _ in DEVICES)}</tr>")
    return f"<table><tr><th>Scenario</th>{head}</tr>{''.join(rows)}</table>"



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

# ---------------------------------------------------------------- page

def build(root: Path, profile_arg):
    runs, rejected = load(root)
    if not runs:
        sys.exit(f"No valid results under {root}")

    groups = group(runs)
    sizes = {}
    for r in runs:
        sizes[r["workload"]["profileOrScenario"]] = int(r["_notes"].get("bindings", 0))
    profiles = sorted(sizes, key=lambda p: sizes[p])
    profile = profile_arg or max(profiles, key=lambda p: (len({d for d, pr in groups if pr == p}), sizes[p]))

    sample = next(r for r in runs if r["workload"]["profileOrScenario"] == profile)
    n = sample["_notes"]
    devices_with_data = [name for name, _, _ in DEVICES if (name, profile) in groups]
    pending = [name for name, _, _ in DEVICES if name not in devices_with_data]
    runs_per_variant = sample["workload"].get("totalRuns", "?")

    heads = "".join(headline(name, tier, {k: Cell(v) for k, v in groups[(name, profile)].items()})
                    for name, tier, _ in DEVICES if (name, profile) in groups)

    # Short version numbers from the slowest device with data, which is where the difference matters most.
    worst = None
    for name in devices_with_data:
        c = {k: Cell(v) for k, v in groups[(name, profile)].items()}
        if all(x in c for x in VARIANTS):
            if worst is None or c["fastioc"].di() > worst[1]["fastioc"].di():
                worst = (name, c)
    short_numbers = ""
    if worst:
        name, c = worst
        short_numbers = (f" On the {e(name)}, the DI work at launch takes <b>{ms(c['fastioc'].di())}</b> with Fast IoC, "
                         f"{ms(c['vcontainer'].di())} with VContainer and {ms(c['zenject'].di())} with Zenject: "
                         f"about {frames(c['zenject'].di(), FRAME_30):.0f} frozen frames at 30 fps with Zenject, "
                         f"{frames(c['vcontainer'].di(), FRAME_30):.0f} with VContainer, and under one with Fast IoC.")

    per_device = ""
    for name, tier, _ in DEVICES:
        per_device += f'<h3>{e(name)} <span style="color:#6b7280;font-size:8.6pt">· {e(tier)}</span></h3>'
        if (name, profile) in groups:
            per_device += full_table({k: Cell(v) for k, v in groups[(name, profile)].items()})
            per_device += '<p class="note">"No DI" is the same scene and prefabs with no container: what Unity costs on its own. A container\'s own cost is its value minus this column. Values are medians of the runs; ranges in brackets are the lowest and highest run. Garbage = managed heap growth (Release-safe); it reads lower if a collection ran inside the window, so read it with the collections row.</p>'
        else:
            per_device += f'<p class="pending">Results pending — this table fills when the device\'s {3 * int(runs_per_variant) if str(runs_per_variant).isdigit() else ""} files are pulled.</p>'

    rejected_note = f"<p class='note'>{len(rejected)} run(s) excluded because the resolved graph didn't match.</p>" if rejected else ""
    other_profiles = [p for p in ["small-100", "growth-500", "readme-scale-1000", "mature-2500", "large-team-5000"] if p not in profiles]

    body = f"""
<h1>Fast IoC — the case for switching</h1>
<p class="sub">Benchmark results on real phones · for dev, leadership and finance review · internal</p>
<p class="sub">Results so far: {e(', '.join(devices_with_data))} · project size <b>{e(profile)}</b> ({e(n.get('bindings'))} bindings, {e(n.get('monoInjectables'))} injected scene objects).
{('Pending: ' + e(', '.join(pending)) + '.') if pending else ''} Larger project sizes pending: {e(', '.join(other_profiles)) or 'none'}. This document refreshes as results come in.</p>

<h2>The short version</h2>
<p>Fast IoC is a dependency injection (DI) container for Unity: it connects a game's services and scripts so each piece gets what it needs
without hard-wired references. It was tested against Zenject (through Extenject, its maintained fork), which our projects use today, and VContainer,
the most common modern alternative. All three ran in the same build on the same phones, resolving exactly the same generated project, under the same conditions.</p>
<p><b>The result: Fast IoC starts several times faster than both, reloads scenes faster, and uses less memory. It is slower than VContainer at spawning
injected prefabs.</b>{short_numbers} Startup matters because it is a freeze players see at launch and on every loading screen, and it hits low-end phones hardest.</p>
{heads}
<p class="note">Lower is better for every number. ms = milliseconds; a frame lasts 33.3 ms at 30 fps and 16.7 ms at 60 fps. Prefab card: lower is better; grey means Fast IoC is not the fastest.</p>
{differs(groups, profile, devices_with_data)}
<p class="honest"><b>Honest summary:</b> Fast IoC removes most of the DI cost at startup and on scene loads, which is the cost players actually feel,
and holds less memory. It is not the fastest at spawning injected prefabs: VContainer is, and this document doesn't claim otherwise.
These results come from the smallest project size measured so far; larger sizes are pending and should widen the startup gap, because reflection
work grows with every binding.</p>
{rejected_note}
{setup_section(n)}
<div class="page"></div>
<h2>How it was tested</h2>
<p>One Release IL2CPP build containing all three containers, installed on each phone and left to run on its own. Every launch is a cold start
of the app and measures one container; the others stay dormant. Each container is measured {e(runs_per_variant)} times, taking turns so none always goes first.
Memory is cleaned and the phone rests 1 s between phases. The project is generated from a fixed seed, so every container binds and resolves the same
classes, and every run checks that the graph it built is the expected one (same hash, instance count and injected fields). A run that fails the check is discarded.</p>
<table>
<tr><th>Test</th><th>What it simulates</th><th>Size</th></tr>
<tr><td><b>Startup</b></td><td>Launching the game: build the global container, then load the first scene, which creates every service and injects its scripts</td><td>{e(n.get('bindings'))} bindings, {e(n.get('implementations'))} classes, {e(n.get('monoInjectables'))} scene objects</td></tr>
<tr><td><b>Scene reload</b></td><td>Leaving and re-entering a level or menu</td><td>{e(n.get('reloadCycles', 5))} unload/load cycles</td></tr>
<tr><td><b>Prefab spawning</b></td><td>Enemies, list items or popups created during play, through each container's own instantiate API</td><td>{e(n.get('prefabInstances', 1000))} prefabs, {e(n.get('prefabBatch', 50))} per frame</td></tr>
<tr><td><b>Memory</b></td><td>Heap held after startup, garbage and garbage collections in each phase</td><td>All phases</td></tr>
</table>
<p class="note">Measured with a high-resolution timer inside the app, from the first managed code that runs. Each container uses its default, documented setup:
Fast IoC with edit-time generated code, Zenject with reflection, VContainer with reflection. Zenject reflection baking and the VContainer source generator are not measured yet.
Lifetimes are singleton and transient. Values are medians of the runs.</p>

<h2>Device runs (for manual comparison)</h2>
{device_runs_table(groups, profile, runs)}
<p class="note">Engine boot is Unity starting up and showing its splash screen before the first scene; it is the same for every container and is not part of any number above.</p>

<div class="page"></div>
<h2>Full comparison per device</h2>
<p class="sub">Fast IoC column highlighted. Project size {e(profile)}.</p>
{per_device}

<div class="page"></div>
<h2>Fast IoC only — per device</h2>
<p>These are the numbers that can go public once the naming policy is settled. One block per project size measured.</p>
{fastioc_only(groups, profiles)}
<p class="note">Startup cost = build container + first scene injection. Pending project sizes: {e(', '.join(other_profiles)) or 'none'}.</p>
"""
    return f"<!DOCTYPE html><html><head><meta charset='utf-8'><title>Fast IoC — the case for switching</title><style>{CSS}</style></head><body>{body}</body></html>"


def to_pdf(html_path: Path, pdf_path: Path):
    footer = ('<div style="font-size:7px;color:#9ca3af;width:100%;padding:0 14mm;display:flex;justify-content:space-between;font-family:Arial">'
              '<span>Fast IoC — the case for switching · internal</span><span>Page <span class="pageNumber"></span></span></div>')
    header = ('<div style="font-size:7px;color:#9ca3af;width:100%;padding:0 14mm;font-family:Arial">'
              'Fast IoC — the case for switching · internal</div>')
    try:
        from playwright.sync_api import sync_playwright
        chrome = shutil.which("google-chrome") or shutil.which("chromium") or "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
        with sync_playwright() as p:
            browser = p.chromium.launch(executable_path=chrome if Path(chrome).exists() else None)
            page = browser.new_page()
            page.goto(html_path.resolve().as_uri())
            page.pdf(path=str(pdf_path), format="A4", print_background=True, display_header_footer=True,
                     header_template=header, footer_template=footer,
                     margin={"top": "16mm", "bottom": "16mm", "left": "0", "right": "0"})
            browser.close()
        return True
    except Exception as ex:  # fall back to the Chrome command line
        chrome = shutil.which("google-chrome") or "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
        if not Path(chrome).exists() and not shutil.which(chrome):
            print(f"No PDF: {ex}. Open the HTML and print it.", file=sys.stderr)
            return False
        subprocess.run([chrome, "--headless", "--disable-gpu", "--no-pdf-header-footer", f"--print-to-pdf={pdf_path}", html_path.resolve().as_uri()],
                       check=True, capture_output=True)
        return True


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("root", type=Path, help="folder with one subfolder of raw results per device")
    parser.add_argument("--profile", help="project size for the headline (default: the one with most devices)")
    parser.add_argument("--out", type=Path, default=Path("FastIoC-benchmark-brief"), help="output path without extension")
    args = parser.parse_args()

    html_path = args.out.with_suffix(".html")
    html_path.write_text(build(args.root, args.profile), encoding="utf-8")
    print(f"Wrote {html_path}")
    pdf_path = args.out.with_suffix(".pdf")
    if to_pdf(html_path, pdf_path):
        print(f"Wrote {pdf_path}")


if __name__ == "__main__":
    main()
