#!/usr/bin/env python3
"""Merge five AI sweep arms and render baseline-relative, dependency-free reports."""

from __future__ import annotations

import csv
import html
import json
import math
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable


LEVELS = (0, 1, 2, 3, 4)
BASELINE = 1
FOCUS = 4
METRICS = {
    "winRate": {"title": "Win rate", "file": "winrate.svg", "unit": "%", "better": 1},
    "avgPlace": {"title": "Average place", "file": "avg-place.svg", "unit": "", "better": -1},
    "avgScore": {"title": "Average score", "file": "avg-score.svg", "unit": "", "better": 1},
}
LEVEL_LABELS = {
    0: "pure random control",
    1: "legacy baseline",
    2: "fair strategy",
    3: "fair strategy + memory",
    4: "Legacy+ hybrid",
}


def load_json(path: Path) -> dict[str, Any]:
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, value: Any) -> None:
    with path.open("w", encoding="utf-8") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2)
        handle.write("\n")


def empty_stat() -> dict[str, float]:
    return {
        "games": 0,
        "wins": 0,
        "top1": 0,
        "top2": 0,
        "top3": 0,
        "top4": 0,
        "top5": 0,
        "top6": 0,
        "scoreSum": 0.0,
        "scoreSquareSum": 0.0,
        "placeSum": 0.0,
        "placeSquareSum": 0.0,
    }


def mean_se(total: float, square_total: float, count: int) -> tuple[float, float]:
    if count == 0:
        return 0.0, 0.0
    mean = total / count
    if count == 1:
        return mean, 0.0
    variance = max(0.0, (square_total - total * total / count) / (count - 1))
    return mean, math.sqrt(variance / count)


def bounded_ci(value: float, se: float, low: float | None = None, high: float | None = None) -> list[float]:
    left, right = value - 1.96 * se, value + 1.96 * se
    if low is not None:
        left = max(low, left)
    if high is not None:
        right = min(high, right)
    return [round(left, 3), round(right, 3)]


def wilson(successes: int, count: int) -> tuple[list[float], float]:
    if count == 0:
        return [0.0, 0.0], 0.0
    z = 1.96
    p = successes / count
    denominator = 1 + z * z / count
    center = (p + z * z / (2 * count)) / denominator
    half = z * math.sqrt(p * (1 - p) / count + z * z / (4 * count * count)) / denominator
    interval = [round(100 * max(0.0, center - half), 3), round(100 * min(1.0, center + half), 3)]
    return interval, 100 * half / z


def finalized_row(name: str, stat: dict[str, float]) -> dict[str, Any]:
    games = int(stat["games"])
    wins = int(stat["wins"])
    avg_score, avg_score_se = mean_se(stat["scoreSum"], stat["scoreSquareSum"], games)
    avg_place, avg_place_se = mean_se(stat["placeSum"], stat["placeSquareSum"], games)
    win_ci, win_se = wilson(wins, games)
    return {
        "name": name,
        "games": games,
        "wins": wins,
        **{f"top{place}": int(stat[f"top{place}"]) for place in range(1, 7)},
        "winRate": round(100 * wins / games, 3) if games else 0.0,
        "winRateSe": round(win_se, 6),
        "winRateCi95": win_ci,
        "avgScore": round(avg_score, 3),
        "avgScoreSe": round(avg_score_se, 6),
        "avgScoreCi95": bounded_ci(avg_score, avg_score_se),
        "avgPlace": round(avg_place, 3),
        "avgPlaceSe": round(avg_place_se, 6),
        "avgPlaceCi95": bounded_ci(avg_place, avg_place_se, 1, 6),
    }


def merge_level(paths: Iterable[Path], expected_level: int) -> dict[str, Any]:
    stats: dict[str, dict[str, float]] = defaultdict(empty_stat)
    reports = 0
    total_requested = total_finished = total_stuck = 0
    errors: list[dict[str, Any]] = []
    stuck_games: list[dict[str, Any]] = []
    versions: set[str] = set()
    duration = 0.0

    for path in paths:
        report = load_json(path)
        level = report.get("options", {}).get("aiDifficulty")
        if level != expected_level:
            raise ValueError(f"{path}: expected AI level {expected_level}, found {level}")
        reports += 1
        total_requested += int(report.get("gamesRequested", 0))
        total_finished += int(report.get("gamesFinished", 0))
        total_stuck += int(report.get("gamesStuck", 0))
        errors.extend(report.get("errors", []))
        stuck_games.extend(report.get("stuckGames", []))
        duration += float(report.get("durationSeconds", 0))
        if report.get("gameVersion"):
            versions.add(str(report["gameVersion"]))

        games = report.get("games")
        if games is None:
            raise ValueError(f"{path}: missing per-game data needed for exact averages and confidence intervals")
        for game in games:
            for player in game.get("players", []):
                if player.get("isStructuralClone", False):
                    continue
                name = player.get("character")
                if not name:
                    continue
                stat = stats[name]
                score = float(player.get("score", 0))
                place = int(player.get("place", 0))
                stat["games"] += 1
                stat["wins"] += int(bool(player.get("isWinner", False)))
                stat["scoreSum"] += score
                stat["scoreSquareSum"] += score * score
                stat["placeSum"] += place
                stat["placeSquareSum"] += place * place
                if 1 <= place <= 6:
                    stat[f"top{place}"] += 1

    if reports == 0:
        raise ValueError(f"AI level {expected_level}: no batch reports found")

    characters = [finalized_row(name, stat) for name, stat in stats.items()]
    characters.sort(key=lambda row: (-row["winRate"], row["name"].casefold()))
    return {
        "aiDifficulty": expected_level,
        "label": LEVEL_LABELS[expected_level],
        "batches": reports,
        "gameVersions": sorted(versions),
        "totalRequested": total_requested,
        "totalFinished": total_finished,
        "totalStuck": total_stuck,
        "durationSecondsSum": round(duration, 1),
        "errorCount": len(errors),
        "errors": errors,
        "stuckGames": stuck_games,
        "characters": characters,
    }


def metric_improvement(row: dict[str, Any], baseline: dict[str, Any], metric: str) -> tuple[float, float]:
    direction = METRICS[metric]["better"]
    delta = direction * (float(row[metric]) - float(baseline[metric]))
    se = math.hypot(float(row[f"{metric}Se"]), float(baseline[f"{metric}Se"]))
    z_score = delta / se if se > 0 else 0.0
    return delta, z_score


def comparison_rows(merged: dict[int, dict[str, Any]]) -> list[dict[str, Any]]:
    by_level = {
        level: {row["name"]: row for row in report["characters"]}
        for level, report in merged.items()
    }
    names = sorted(set().union(*(rows.keys() for rows in by_level.values())), key=str.casefold)
    result = []
    for name in names:
        levels = {str(level): by_level[level].get(name) for level in LEVELS}
        baseline = levels[str(BASELINE)]
        changes: dict[str, dict[str, dict[str, Any] | None]] = {}
        for metric in METRICS:
            changes[metric] = {}
            for level in LEVELS:
                current = levels[str(level)]
                if current is None or baseline is None:
                    changes[metric][str(level)] = None
                    continue
                delta, z_score = metric_improvement(current, baseline, metric)
                changes[metric][str(level)] = {
                    "improvementVsLevel1": round(delta, 4),
                    "zScoreVsLevel1": round(z_score, 4),
                    "significant95": abs(z_score) >= 1.96,
                }
        result.append({"name": name, "levels": levels, "changesVsLevel1": changes})
    return result


def fmt_value(metric: str, value: float) -> str:
    if metric == "winRate":
        return f"{value:.2f}%"
    if metric == "avgPlace":
        return f"{value:.3f}"
    return f"{value:.2f}"


def fmt_delta(metric: str, value: float) -> str:
    suffix = " pp" if metric == "winRate" else ""
    precision = 2 if metric != "avgPlace" else 3
    return f"{value:+.{precision}f}{suffix}"


def blend(base: str, target: str, amount: float) -> str:
    base_rgb = tuple(int(base[index:index + 2], 16) for index in (1, 3, 5))
    target_rgb = tuple(int(target[index:index + 2], 16) for index in (1, 3, 5))
    mixed = tuple(round(a + (b - a) * amount) for a, b in zip(base_rgb, target_rgb))
    return "#" + "".join(f"{value:02x}" for value in mixed)


def svg_chart(rows: list[dict[str, Any]], metric: str) -> str:
    meta = METRICS[metric]
    ranked = sorted(
        rows,
        key=lambda row: (
            -((row["changesVsLevel1"][metric][str(FOCUS)] or {}).get("improvementVsLevel1", -math.inf)),
            row["name"].casefold(),
        ),
    )
    left, cell_width, row_height = 320, 220, 38
    width = left + cell_width * len(LEVELS) + 20
    top, bottom = 150, 55
    height = top + row_height * len(ranked) + bottom
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" fill="#f8fafc"/>',
        '<style>text{font-family:Inter,Segoe UI,Arial,sans-serif;fill:#0f172a}.title{font-size:25px;font-weight:700}.sub{font-size:13px;fill:#475569}.head{font-size:14px;font-weight:700}.name{font-size:13px;font-weight:600}.value{font-size:13px;font-weight:700}.detail{font-size:11px;fill:#475569}</style>',
        f'<text x="24" y="36" class="title">AI progression · {html.escape(meta["title"])}</text>',
        f'<text x="24" y="61" class="sub">Sorted by AI {FOCUS} improvement versus AI 1. Positive Δ always means better (lower is better for place).</text>',
        '<text x="24" y="82" class="sub">Blue = better than L1 · red = worse · stronger colour = larger difference relative to sampling noise · ★ = 95% significant.</text>',
    ]
    for level in LEVELS:
        x = left + level * cell_width
        header_fill = "#334155" if level == BASELINE else "#e2e8f0"
        header_text = "#ffffff" if level == BASELINE else "#0f172a"
        parts.append(f'<rect x="{x}" y="100" width="{cell_width - 6}" height="38" rx="6" fill="{header_fill}"/>')
        parts.append(f'<text x="{x + (cell_width - 6) / 2}" y="117" text-anchor="middle" class="head" style="fill:{header_text}">AI {level}{" · BASELINE" if level == BASELINE else ""}</text>')
        parts.append(f'<text x="{x + (cell_width - 6) / 2}" y="132" text-anchor="middle" font-size="10" style="fill:{header_text}">{html.escape(LEVEL_LABELS[level])}</text>')

    for index, item in enumerate(ranked):
        y = top + index * row_height
        if index % 2:
            parts.append(f'<rect x="12" y="{y}" width="{width - 24}" height="{row_height}" fill="#f1f5f9"/>')
        parts.append(f'<text x="24" y="{y + 23}" class="name">{html.escape(item["name"])}</text>')
        baseline = item["levels"].get(str(BASELINE))
        for level in LEVELS:
            current = item["levels"].get(str(level))
            x = left + level * cell_width
            fill = "#e2e8f0" if level == BASELINE else "#ffffff"
            label = "no data"
            detail = ""
            tooltip = f"{item['name']} · AI {level}: no data"
            if current is not None:
                label = fmt_value(metric, float(current[metric]))
                detail = f"n={current['games']:,}"
                tooltip = f"{item['name']} · AI {level}: {label}; n={current['games']:,}"
                change = item["changesVsLevel1"][metric].get(str(level))
                if level != BASELINE and baseline is not None and change is not None:
                    improvement = float(change["improvementVsLevel1"])
                    z_score = float(change["zScoreVsLevel1"])
                    if abs(z_score) >= 0.15 and improvement != 0:
                        amount = min(0.82, 0.14 + 0.23 * abs(z_score))
                        fill = blend("#ffffff", "#2563eb" if improvement > 0 else "#dc2626", amount)
                    marker = " ★" if change["significant95"] else ""
                    detail = f"Δ {fmt_delta(metric, improvement)}{marker} · n={current['games']:,}"
                    tooltip += f"; improvement vs L1 {fmt_delta(metric, improvement)}; z={z_score:.2f}"
            parts.append(f'<g><title>{html.escape(tooltip)}</title><rect x="{x}" y="{y + 3}" width="{cell_width - 6}" height="{row_height - 6}" rx="4" fill="{fill}" stroke="#cbd5e1"/>')
            parts.append(f'<text x="{x + 10}" y="{y + 18}" class="value">{html.escape(label)}</text>')
            parts.append(f'<text x="{x + 10}" y="{y + 31}" class="detail">{html.escape(detail)}</text></g>')
    parts.append('</svg>')
    return "\n".join(parts) + "\n"


def key_movers(rows: list[dict[str, Any]], metric: str, level: int = FOCUS, count: int = 8) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    eligible = [
        row for row in rows
        if row["changesVsLevel1"][metric].get(str(level)) is not None
    ]
    eligible.sort(key=lambda row: row["changesVsLevel1"][metric][str(level)]["improvementVsLevel1"], reverse=True)
    return eligible[:count], list(reversed(eligible[-count:]))


def mover_table(rows: list[dict[str, Any]], metric: str, heading: str) -> str:
    pieces = [f"<h3>{html.escape(heading)}</h3><table><thead><tr><th>Character</th><th>L1</th><th>L{FOCUS}</th><th>Improvement</th><th>Signal</th></tr></thead><tbody>"]
    for item in rows:
        baseline = item["levels"]["1"]
        current = item["levels"][str(FOCUS)]
        change = item["changesVsLevel1"][metric][str(FOCUS)]
        pieces.append(
            "<tr>"
            f"<td>{html.escape(item['name'])}</td>"
            f"<td>{fmt_value(metric, baseline[metric])}</td>"
            f"<td>{fmt_value(metric, current[metric])}</td>"
            f"<td class=\"{'good' if change['improvementVsLevel1'] >= 0 else 'bad'}\">{fmt_delta(metric, change['improvementVsLevel1'])}</td>"
            f"<td>{'★ 95%' if change['significant95'] else 'within noise'}</td>"
            "</tr>"
        )
    pieces.append("</tbody></table>")
    return "".join(pieces)


def render_html(root: Path, merged: dict[int, dict[str, Any]], rows: list[dict[str, Any]], warnings: list[str]) -> None:
    cards = []
    for level in LEVELS:
        report = merged[level]
        cards.append(
            f'<div class="card"><b>AI {level}</b><span>{html.escape(LEVEL_LABELS[level])}</span>'
            f'<strong>{report["totalFinished"]:,} / {report["totalRequested"]:,}</strong>'
            f'<small>{report["errorCount"]} errors · {report["totalStuck"]} stuck</small></div>'
        )
    sections = []
    for metric, meta in METRICS.items():
        gains, losses = key_movers(rows, metric)
        sections.append(
            f'<section id="{metric}"><h2>{html.escape(meta["title"])}</h2>'
            f'<a href="{meta["file"]}"><img src="{meta["file"]}" alt="{html.escape(meta["title"])} chart"></a>'
            '<div class="tables">'
            + mover_table(gains, metric, f"Largest AI {FOCUS} gains vs L1")
            + mover_table(losses, metric, f"Largest AI {FOCUS} regressions vs L1")
            + '</div></section>'
        )
    warning_html = "" if not warnings else (
        '<div class="warning"><b>Partial / unhealthy sweep:</b> ' + html.escape(" ".join(warnings)) +
        ' Treat the charts as diagnostic only; do not compare incomplete arms as a final result.</div>'
    )
    document = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>KOTGH AI sweep comparison</title>
<style>
:root{{--ink:#0f172a;--muted:#64748b;--line:#cbd5e1;--panel:#fff;--good:#1d4ed8;--bad:#b91c1c}}*{{box-sizing:border-box}}body{{margin:0;background:#f8fafc;color:var(--ink);font:14px Inter,Segoe UI,Arial,sans-serif}}main{{max-width:1500px;margin:auto;padding:28px}}h1{{margin:0 0 8px;font-size:32px}}h2{{margin-top:42px}}p{{color:#475569;max-width:1000px;line-height:1.55}}nav a{{margin-right:18px}}.warning{{margin:18px 0;padding:13px 15px;border:1px solid #f59e0b;border-radius:8px;background:#fffbeb;color:#78350f;line-height:1.45}}.cards{{display:grid;grid-template-columns:repeat(5,minmax(180px,1fr));gap:12px;margin:22px 0}}.card{{display:flex;flex-direction:column;gap:5px;padding:16px;background:var(--panel);border:1px solid var(--line);border-radius:10px}}.card span,.card small{{color:var(--muted)}}.card strong{{font-size:20px}}section>img,section>a>img{{display:block;width:100%;height:auto;background:white;border:1px solid var(--line);border-radius:10px}}.tables{{display:grid;grid-template-columns:1fr 1fr;gap:18px}}table{{width:100%;border-collapse:collapse;background:white}}th,td{{padding:8px;border-bottom:1px solid #e2e8f0;text-align:right}}th:first-child,td:first-child{{text-align:left}}.good{{color:var(--good);font-weight:700}}.bad{{color:var(--bad);font-weight:700}}code{{background:#e2e8f0;padding:2px 5px;border-radius:4px}}@media(max-width:850px){{.cards,.tables{{grid-template-columns:1fr}}main{{padding:16px}}}}
</style></head><body><main>
<h1>Bot AI progression sweep</h1>
<p>AI level 1 is the baseline. Every chart cell shows the absolute metric and its improvement versus L1; positive improvement always means better, including average place where a lower raw number is better. Colour strength is scaled by the combined standard error, and ★ marks a difference beyond the approximate 95% threshold. Each arm is a homogeneous field (all six players use the same level), so this measures which character policies gain or regress relative to their same-level opponents—not an overall head-to-head win rate between AI levels. Forced coverage keeps rare/low-tier characters visible; results are bot-policy signals, not human-meta balance truth.</p>
<nav><a href="winrate.svg">Win rate</a><a href="avg-place.svg">Average place</a><a href="avg-score.svg">Average score</a><a href="comparison.csv">CSV</a><a href="comparison.json">JSON</a><a href="summary.md">Markdown summary</a></nav>
{warning_html}<div class="cards">{''.join(cards)}</div>{''.join(sections)}
</main></body></html>"""
    (root / "index.html").write_text(document, encoding="utf-8")


def render_csv(root: Path, rows: list[dict[str, Any]]) -> None:
    fields = [
        "character", "ai_level", "games", "wins", "win_rate", "win_rate_ci95_low", "win_rate_ci95_high",
        "win_rate_improvement_vs_l1", "avg_place", "avg_place_ci95_low", "avg_place_ci95_high",
        "avg_place_improvement_vs_l1", "avg_score", "avg_score_ci95_low", "avg_score_ci95_high",
        "avg_score_improvement_vs_l1",
    ]
    with (root / "comparison.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for item in rows:
            for level in LEVELS:
                current = item["levels"].get(str(level))
                if current is None:
                    continue
                changes = {
                    metric: (item["changesVsLevel1"][metric][str(level)] or {}).get("improvementVsLevel1", "")
                    for metric in METRICS
                }
                writer.writerow({
                    "character": item["name"], "ai_level": level, "games": current["games"], "wins": current["wins"],
                    "win_rate": current["winRate"], "win_rate_ci95_low": current["winRateCi95"][0], "win_rate_ci95_high": current["winRateCi95"][1],
                    "win_rate_improvement_vs_l1": changes["winRate"],
                    "avg_place": current["avgPlace"], "avg_place_ci95_low": current["avgPlaceCi95"][0], "avg_place_ci95_high": current["avgPlaceCi95"][1],
                    "avg_place_improvement_vs_l1": changes["avgPlace"],
                    "avg_score": current["avgScore"], "avg_score_ci95_low": current["avgScoreCi95"][0], "avg_score_ci95_high": current["avgScoreCi95"][1],
                    "avg_score_improvement_vs_l1": changes["avgScore"],
                })


def render_markdown(root: Path, merged: dict[int, dict[str, Any]], rows: list[dict[str, Any]], warnings: list[str]) -> None:
    lines = ["# Bot AI progression sweep", "", "AI level 1 is the baseline. Positive change means better (including average place, where lower is better).", "", "Each arm is a homogeneous six-bot field. Deltas measure per-character policy fit against same-level opponents, not overall head-to-head strength between AI levels.", "", "## Run health", "", "| AI | Label | Finished / requested | Errors | Stuck |", "|---:|---|---:|---:|---:|"]
    if warnings:
        lines[6:6] = ["> ⚠ Partial / unhealthy sweep: " + " ".join(warnings), "", "Do not use these charts as a final cross-level comparison.", ""]
    for level in LEVELS:
        report = merged[level]
        lines.append(f"| {level} | {LEVEL_LABELS[level]} | {report['totalFinished']:,} / {report['totalRequested']:,} | {report['errorCount']} | {report['totalStuck']} |")
    for metric, meta in METRICS.items():
        gains, losses = key_movers(rows, metric, count=10)
        lines.extend(["", f"## {meta['title']}: AI {FOCUS} vs AI 1", "", f"| Character | L1 | L{FOCUS} | Improvement | 95% signal |", "|---|---:|---:|---:|:---:|"])
        for item in gains + losses:
            baseline = item["levels"]["1"]
            current = item["levels"][str(FOCUS)]
            change = item["changesVsLevel1"][metric][str(FOCUS)]
            lines.append(f"| {item['name']} | {fmt_value(metric, baseline[metric])} | {fmt_value(metric, current[metric])} | {fmt_delta(metric, change['improvementVsLevel1'])} | {'★' if change['significant95'] else ''} |")
    (root / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def build(root: Path, expected_total: int, allow_partial: bool = False) -> None:
    merged: dict[int, dict[str, Any]] = {}
    for level in LEVELS:
        level_dir = root / f"ai-{level}"
        paths = sorted(level_dir.glob("batch-*.json"))
        merged[level] = merge_level(paths, level)
        write_json(level_dir / "merged.json", merged[level])
        print(
            f"[report:L{level}] {merged[level]['totalFinished']:,}/{merged[level]['totalRequested']:,} finished, "
            f"{merged[level]['errorCount']} errors, {merged[level]['totalStuck']} stuck"
        )

    wrong_totals = {
        level: report["totalRequested"]
        for level, report in merged.items()
        if report["totalRequested"] != expected_total
    }
    warnings = []
    if wrong_totals:
        details = ", ".join(f"L{level}={total:,}" for level, total in wrong_totals.items())
        warnings.append(f"Expected {expected_total:,} games per AI level; found {details}.")
        if not allow_partial:
            raise ValueError(warnings[0] + " Re-run with --allow-partial to render a clearly labelled diagnostic report.")
    for level, report in merged.items():
        if report["errorCount"] or report["totalStuck"]:
            warnings.append(f"AI {level} recorded {report['errorCount']} errors and {report['totalStuck']} stuck games.")

    rows = comparison_rows(merged)
    comparison = {
        "baselineAiDifficulty": BASELINE,
        "reportStatus": "partial-or-unhealthy" if warnings else "complete-clean",
        "warnings": warnings,
        "experimentInterpretation": "homogeneous six-bot fields; character deltas are relative to same-level opponents, not direct cross-level head-to-head strength",
        "metricChangeConvention": "positive is better; avgPlace improvement reverses the raw delta because lower place is better",
        "levels": {str(level): merged[level] for level in LEVELS},
        "characters": rows,
    }
    write_json(root / "comparison.json", comparison)
    for metric, meta in METRICS.items():
        (root / meta["file"]).write_text(svg_chart(rows, metric), encoding="utf-8")
    render_csv(root, rows)
    render_markdown(root, merged, rows, warnings)
    render_html(root, merged, rows, warnings)


def usage() -> int:
    print("usage: sweep-report.py games-requested REPORT.json", file=sys.stderr)
    print("       sweep-report.py build SWEEP_DIRECTORY EXPECTED_GAMES_PER_LEVEL [--allow-partial]", file=sys.stderr)
    return 2


def main(argv: list[str]) -> int:
    try:
        if len(argv) == 3 and argv[1] == "games-requested":
            print(int(load_json(Path(argv[2])).get("gamesRequested", 0)))
            return 0
        if len(argv) in (4, 5) and argv[1] == "build":
            if len(argv) == 5 and argv[4] != "--allow-partial":
                return usage()
            build(Path(argv[2]), int(argv[3]), len(argv) == 5)
            return 0
        return usage()
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as error:
        print(f"[report] ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
