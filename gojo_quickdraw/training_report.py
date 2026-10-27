"""Portable review of local model opinions; SVGs are views, not training inputs."""
import html
from pathlib import Path

from .training_data import NAMES, split


def svg(runs, encounter=None, context=None):
    local_points = [p for run in runs for p in run]
    points = local_points + [p for run in (context or []) for p in run]
    if not points:
        return ''
    left, right = min(p[0] for p in points), max(p[0] for p in points)
    top, bottom = min(p[1] for p in points), max(p[1] for p in points)
    scale = 140/max(right-left, bottom-top, 1)
    def xy(p):
        return 90+(p[0]-(left+right)/2)*scale, 90+(p[1]-(top+bottom)/2)*scale
    rows = []
    for run in context or []:
        vertices = ' '.join(f'{x:.2f},{y:.2f}' for x, y in map(xy, run))
        rows.append(f'<polyline class="context" points="{vertices}" fill="none" stroke="#9caaa4" stroke-opacity=".38" stroke-width="1"/>')
    for i, run in enumerate(runs):
        vertices = ' '.join(f'{x:.2f},{y:.2f}' for x, y in map(xy, run))
        rows.append(f'<polyline points="{vertices}" fill="none" stroke="{("#247d72", "#c17829")[i % 2]}" stroke-width="2"/>')
    if encounter:
        x, y = xy(encounter)
        rows.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="4" fill="none" stroke="#c63b7a"/>')
    local = [xy(p) for p in local_points]
    x, y = min(p[0] for p in local)-8, min(p[1] for p in local)-8
    w, h = max(p[0] for p in local)-x+8, max(p[1] for p in local)-y+8
    box = f' data-local="{x:.2f} {y:.2f} {max(w,10):.2f} {max(h,10):.2f}"' if context else ''
    return '<svg viewBox="0 0 180 180" role="img"'+box+'>' + ''.join(rows) + '</svg>'


def build(output, report, examples, predictions, templates, seed, contexts=None):
    output = Path(output)
    def percent(value):
        return '—' if value is None else f'{100*value:.1f}%'
    rows = []
    for mode, result in report['ablations'].items():
        rows.append(f'<tr><td>{html.escape(mode)}</td><td>{percent(result["controlledTest"]["top1Agreement"])}</td><td>{percent(result["realWeakTest"]["top1Agreement"])}</td><td>{percent(result["realWeakTest"]["proposalCoverage"])}</td></tr>')
    rows.append(f'<tr><td><strong>Selected: {html.escape(report["selectedModel"])}</strong></td><td>{percent(report["controlledTest"]["top1Agreement"])}</td><td>{percent(report["realWeakTest"]["top1Agreement"])}</td><td>{percent(report["realWeakTest"]["proposalCoverage"])}</td></tr>')
    cards = []
    # Inspect held-out real disagreements first, plus a bounded control sample.
    held_out = [i for i, row in enumerate(examples) if split(row['group'], seed) == 'test']
    def difficulty(i):
        return examples[i]['label'] is None or predictions[i]['ranking'][0] == examples[i]['label'], examples[i]['id']
    weak = sorted((i for i in held_out if examples[i]['labelBasis'] != 'controlled-construction' and examples[i]['label'] is not None), key=difficulty)[:60]
    unlabelled = sorted((i for i in held_out if examples[i]['label'] is None), key=lambda i: examples[i]['id'])[:60]
    controlled = []
    for label in range(len(NAMES)):
        controlled.extend(sorted((i for i in held_out if examples[i]['labelBasis'] == 'controlled-construction' and examples[i]['label'] == label), key=difficulty)[:12])
    chosen = weak + controlled + unlabelled
    for index in chosen:
        row, prediction = examples[index], predictions[index]
        best = prediction['ranking'][0]
        view = templates[best]
        paths = [[view['points'][a], view['points'][b]] for a, b in view['segments']]
        expected = NAMES[row['label']] if row['label'] is not None else 'unlabelled'
        note = row['labelBasis'] + ' · ' + split(row['group'], seed)
        scores = ', '.join(f'{NAMES[i]} {prediction["scores"][i]:.2f}' for i in prediction['ranking'][:3])
        cells = row['bands']
        reaches = ('negligible', 'tiny', 'small', 'medium', 'large', 'most')
        bends = ('approximately straight', 'slightly bent', 'gently curved', 'curved', 'strongly curved', 'very curved')
        hints = f'A: {reaches[cells[0]]} / {reaches[cells[1]]}; B: {reaches[cells[2]]} / {reaches[cells[3]]}. '
        hints += f'Curves: {bends[cells[7]]} / {bends[cells[8]]}.'
        category = 'control' if row['labelBasis'] == 'controlled-construction' else 'weak' if row['label'] is not None else 'unlabelled'
        cards.append(f'<article data-kind="{category}"><b>{html.escape(row["sourceKey"])}</b><div class="pair">{svg(row["runs"], row["encounter"], (contexts or {}).get(row["sourceKey"]))}{svg(paths)}</div>'
                     f'<strong>{html.escape(NAMES[best])} · {html.escape(prediction["status"])}</strong><p>Reference: {html.escape(expected)}</p>'
                     f'<small>{html.escape(note)}<br>{html.escape(scores)}<br>{html.escape(hints)}</small>'
                     f'<details><summary>Source and native evidence</summary><p>{html.escape(row["id"])}<br>Native cells: {html.escape(str(row["bands"]))}</p><a href="native/{html.escape(row["archive"])}">Native patch and candidate constructions</a>'
                     f'<br><a href="opinions/{html.escape(row["archive"])}">Native learned opinions</a></details></article>')
    payload = '<!doctype html><meta charset="utf-8"><title>Local joint model</title><style>' \
        'body{font:16px system-ui;background:#f1f5f0;color:#183d43;margin:24px}p{max-width:1000px}table{border-collapse:collapse}td,th{padding:8px;border-bottom:1px solid #bdccc4;text-align:left}' \
        '.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:14px;margin-top:20px}article{background:white;border:1px solid #cbdcd3;border-radius:12px;padding:14px}.pair{display:flex}.pair svg{width:50%;max-height:190px}small{color:#617570}a{color:#247d72}' \
        '</style><h1>Local joint construction experiment</h1>' \
        '<p>Left: source runs and selected encounter. Right: highest-ranked native construction in its normalized frame. All alternative constructions remain in the native opinion archive. A proposal is provisional; it does not change a drawing or establish contact.</p>' \
        '<p>Controlled test references come from known generators. Real references are weak mappings to our earlier authored finals, not human ground truth. Unlabelled patches are inference only. Sources and templates stay in one split.</p>' \
        '<p>The first three rows use controlled training only. The selected model may also use weak real training patches; model selection uses validation groups.</p>' \
        f'<p>{report["sourceDrawings"]} source drawings · {report["controlledPatches"]} controls · {report["realPatches"]} real patches · selected model: {html.escape(report["selectedModel"])} · {report["parameterCount"]} weights.</p>' \
        '<table><tr><th>Input</th><th>Controlled held-out agreement</th><th>Real weak held-out agreement</th><th>Real proposal coverage</th></tr>' + ''.join(rows) + '</table>' \
        '<p><a href="model.json">Model weights and feature schema</a> · <a href="report.json">Metrics, splits and timings</a> · <a href="predictions.ndjson.gz">All patch predictions</a></p>' \
        '<p>Curvature cells retain bend, total turning, turn concentration and short terminal hooks. The model receives coarse cells; source positions in this page are display/audit data.</p>' \
        '<p><button onclick="filter(\'all\')">All examples</button> <button onclick="filter(\'weak\')">Drawing references</button> <button onclick="filter(\'control\')">Controlled examples</button> <button onclick="filter(\'unlabelled\')">Unlabelled drawings</button> <label><input type="checkbox" checked onchange="context(this.checked)">Drawing context</label></p>' \
        '<div class="grid">' + ''.join(cards) + '</div><script>' \
        'function filter(kind){document.querySelectorAll("article").forEach(a=>a.hidden=kind!=="all"&&a.dataset.kind!==kind)}' \
        'function context(show){document.querySelectorAll(".context").forEach(p=>p.style.display=show?"":"none");document.querySelectorAll("svg[data-local]").forEach(s=>s.setAttribute("viewBox",show?"0 0 180 180":s.dataset.local))}' \
        '</script>'
    (output/'report.html').write_text(payload, encoding='utf-8')
