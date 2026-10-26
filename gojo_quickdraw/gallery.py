"""Small offline input/result comparisons, projected from existing cached evidence.

Rendering neither runs an interpretation pass nor changes the saved source.
The final image is the last authored proposal, not a new native execution.
"""
from __future__ import annotations

import hashlib
from html import escape
import json
import math
import os
from pathlib import Path
from urllib.parse import quote, unquote, urlencode

from .review import collection_stamp
from .stages import _curve_extent
from .object_display import read as object_read, profile_color, COLORS
from .mapping_display import overlay as mapping_overlay
from .cleanup_display import svg as cleanup_image


def _relative(target, parent):
    return quote(os.path.relpath(target, parent).replace(os.sep, "/"), safe="/.-_")


def _inspector_links(dataset, inspector, stamps):
    """Use actual page membership, independent of the gallery's ordering/size."""
    manifest = inspector.with_suffix(".pages.json")
    if not inspector.is_file() or not manifest.is_file():
        return {}
    data = json.loads(manifest.read_text(encoding="utf-8"))
    if (data.get("schema") != "gojo.quickdraw-review-pages.v1"
            or data.get("dataset") != str(dataset) or data.get("journals") != stamps):
        return {}
    pages = [(inspector.parent / unquote(path)).resolve() for path in data["pages"]]
    if not all(path.is_file() for path in pages):
        return {}
    return {row["selectionId"]: (pages[row["page"]], row["itemId"]) for row in data["records"]}


def _number(value):
    if not math.isfinite(value):
        raise ValueError("A gallery profile contains a non-finite coordinate")
    return format(value, ".12g")


def _path(controls):
    if not 2 <= len(controls) <= 4:
        raise ValueError("Gallery curves must have two, three, or four control points")
    points = [" ".join(_number(v) for v in point) for point in controls]
    return "M" + points[0] + " " + {2: "L", 3: "Q", 4: "C"}[len(points)] + " ".join(points[1:])


def _layout_image(packet, original_only=False, mapping=False):
    parts = (packet.get("layout") or {}).get("parts", [])
    if not parts and not original_only:
        return '<div class="missing">No area estimate. Run full church analysis to add one.</div>'
    if mapping and not packet.get('boundaryMapping'):
        return '<div class="missing">Run full analysis to add source-to-shape mapping.</div>'
    strokes = packet["observation"]["strokes"]
    points = [p for s in strokes for p in s["points"]] + [p for part in parts for p in part["geometry"]["points"]]
    l, t = min([0, *[p[0] for p in points]]), min([0, *[p[1] for p in points]])
    r, b = max([512, *[p[0] for p in points]]), max([512, *[p[1] for p in points]])
    pad = .035 * max(r-l, b-t)
    box = ' '.join(_number(v) for v in (l-pad, t-pad, r-l+2*pad, b-t+2*pad))
    image = [f'<svg viewBox="{box}" role="img" aria-label="Area estimates over original ink">']
    for part in ([] if original_only else parts):
        geo = part["geometry"]; color = escape(part["color"], quote=True)
        image.append(f'<g data-layout="{escape(part["id"], quote=True)}"><title>{escape(part["role"])} · {escape(part["status"])}</title>')
        if geo.get("fillIndices"):
            values = ' '.join(','.join(_number(v) for v in geo["points"][i]) for i in geo["fillIndices"])
            image.append(f'<polygon points="{values}" fill="{color}" fill-opacity=".07" stroke="none"/>')
        for a, b in geo.get("segments", []):
            image.append(f'<path d="{_path([geo["points"][a], geo["points"][b]])}" stroke="{color}" stroke-width="2" stroke-dasharray="2 5"/>')
        image.append('</g>')
    # Acquired points are drawn last, so no proposed fill can conceal them.
    for stroke in strokes:
        values = ' '.join(','.join(_number(v) for v in p) for p in stroke["points"])
        image.append(f'<polyline data-stroke="{escape(stroke["id"], quote=True)}" points="{values}" stroke="#465852" stroke-width="2.1"/>')
        if len(stroke['points']) == 1:
            x,y=stroke['points'][0]
            image.append(f'<circle cx="{x}" cy="{y}" r="2" fill="#465852"/>')
    if mapping:image.append(mapping_overlay(packet))
    return ''.join(image) + '</svg>'


def _card(row, packet, number, links, parent):
    strokes = packet["observation"]["strokes"]
    objects = object_read(packet)
    steps = ((packet.get("analysis") or {}).get("proposal") or {}).get("steps", [])
    final = steps[-1] if steps else None
    edges = final.get("edges", []) if final else []
    residue = final.get("residueEdges", []) if final else []
    # Both thumbnails use the same frame, expanded when necessary. An authored
    # proposal outside the original 512 frame must remain visible, not clipped.
    points = [p for stroke in strokes for p in stroke["points"]]
    extents = [_curve_extent(edge["controls"]) for edge in [*edges, *residue]]
    bounds = [min([0, *[p[0] for p in points], *[b[0] for b in extents]]),
              min([0, *[p[1] for p in points], *[b[1] for b in extents]]),
              max([512, *[p[0] for p in points], *[b[2] for b in extents]]),
              max([512, *[p[1] for p in points], *[b[3] for b in extents]])]
    padding = max(bounds[2] - bounds[0], bounds[3] - bounds[1]) * .03
    view = [bounds[0] - padding, bounds[1] - padding,
            bounds[2] - bounds[0] + 2 * padding, bounds[3] - bounds[1] + 2 * padding]
    box = " ".join(_number(v) for v in view)
    key = str(row["keyId"])
    category = str(row.get("category") or packet.get("source", {}).get("word") or "Drawing")
    acquisition = []
    for stroke in strokes:
        data_id = escape(stroke["id"], quote=True)
        if len(stroke["points"]) == 1:
            x, y = map(_number, stroke["points"][0])
            acquisition.append(f'<circle data-stroke="{data_id}" cx="{x}" cy="{y}" r="1.4" fill="currentColor"/>')
        else:
            values = " ".join(",".join(_number(v) for v in point) for point in stroke["points"])
            acquisition.append(f'<polyline data-stroke="{data_id}" points="{values}"/>')
    if objects["available"]:
        stroke_by_index = {stroke["index"]: stroke for stroke in strokes}
        for span in packet["observation"]["spans"]:
            stroke = stroke_by_index[span["strokeIndex"]]
            values = " ".join(",".join(_number(v) for v in point)
                              for point in stroke["points"][span["startIndex"]:span["endIndex"] + 1])
            color = objects["spans"][span["id"]]["color"]
            role = objects["spans"][span["id"]]["role"]
            acquisition.append(f'<polyline class="object-ink" data-object-span="{escape(span["id"], quote=True)}" '
                               f'points="{values}" style="--object-color:{color}"><title>{role} candidate</title></polyline>')
    interpretation = []
    for group, profiles in (("residue", residue), ("final", edges)):
        for edge in profiles:
            color = profile_color(objects, edge.get("spans", edge.get("sourceSpanIds", [])))
            style = f' style="--object-color:{color}"' if objects["available"] else ""
            interpretation.append(f'<path class="{group}" data-edge="{escape(edge["id"], quote=True)}"{style} d="{_path(edge["controls"])}"/>')
    href = None
    selection_id = row.get("selectionId", row["itemId"])
    if selection_id in links:
        target, item = links[selection_id]
        mode = "step:" + str(len(steps) - 1) if final else "observed-fit"
        href = _relative(target, parent) + "?" + urlencode({"view": mode}) + "#" + quote(item, safe="")
    tag = "a" if href else "article"
    attributes = f' href="{escape(href, quote=True)}" target="_blank" rel="noopener"' if href else ""
    final_title = "Final" if final else "No final interpretation yet"
    image = (f'<svg viewBox="{box}" role="img" aria-label="Final interpretation for {escape(key, quote=True)}">'
             + "".join(interpretation) + "</svg>" if final else '<div class="missing">Run full analysis to add a final interpretation.</div>')
    expanded = any((bounds[0] < 0, bounds[1] < 0, bounds[2] > 512, bounds[3] > 512))
    card = (f'<{tag} class="comparison" data-item="{escape(selection_id, quote=True)}"{attributes}>'
            f'<div class="cardhead"><strong>{number}. {escape(category)}</strong>'
            f'<span class="key">{escape(key)}</span></div><div class="pair">'
            f'<div class="input"><div class="label">Input</div><div class="final-output"><svg viewBox="{box}" role="img" '
            f'aria-label="Original drawing {escape(key, quote=True)}">' + "".join(acquisition) + "</svg></div>"
            f'<div class="layout-output">{_layout_image(packet, original_only=True)}</div>'
            f'<div class="cleanup-output">{cleanup_image(packet, original_only=True)}</div></div>'
            f'<div class="output"><div class="final-output"><div class="label">{final_title}</div>{image}</div>'
            f'<div class="layout-output"><div class="label">Area estimates + original</div>{_layout_image(packet)}</div>'
            f'<div class="mapping-output"><div class="label">Source ↔ shape mapping</div>{_layout_image(packet, mapping=True)}</div>'
            f'<div class="cleanup-output"><div class="label">Church cleanup + faint original</div>{cleanup_image(packet)}</div></div></div>'
            f'<div class="cardfoot">{"Open detailed inspector ↗" if href else "Saved drawing"}'
            f'{" · expanded viewing frame" if expanded else ""}'
            f'{" · " + str(objects["candidates"]) + " object candidates" if objects["available"] else ""}</div></{tag}>')
    metadata = {"selectionId": selection_id, "itemId": row["itemId"], "keyId": key,
                "sourceStrokes": len(strokes), "finalEdges": len(edges), "residueEdges": len(residue),
                "finalStepId": final["id"] if final else None, "viewBox": view, "detailsHref": href,
                "objectCandidates": objects["candidates"], "objectColorsAvailable": objects["available"],
                "layoutParts": len((packet.get("layout") or {}).get("parts", [])),
                "cleanupParts": len((packet.get("cleanup") or {}).get("parts", [])),
                "boundaryMapping": (packet.get('boundaryMapping') or {}).get('summary')}
    return card, metadata


def build(dataset: Path, output: Path, page_size: int = 12, *, order="category", seed=0,
          inspector: Path | None = None) -> dict:
    """Render every selected record in bounded, portable comparison pages."""
    from .dataset import load_records
    from .selection import catalog
    if isinstance(page_size, bool) or not isinstance(page_size, int) or page_size < 1:
        raise ValueError("Gallery page size must be a positive integer")
    dataset, output = Path(dataset).resolve(), Path(output).resolve()
    if dataset == output or dataset in output.parents:
        raise ValueError("Gallery output must be outside the dataset")
    if output.suffix.lower() not in (".html", ".htm") or output.is_dir():
        raise ValueError("Gallery output must name an HTML file")
    inspector = Path(inspector).resolve() if inspector else output.parent / "evidence.html"
    if output == inspector:
        raise ValueError("Gallery output cannot replace the detailed inspector")
    total = catalog(dataset, 1, order=order, seed=seed)["storedRecords"]
    if not total:
        raise ValueError("No successfully encoded drawings to review")
    stamps = collection_stamp(dataset)
    links = _inspector_links(dataset, inspector, stamps)
    identity = hashlib.sha256(json.dumps([stamps, page_size, order, seed]).encode()).hexdigest()[:16]
    count = (total + page_size - 1) // page_size
    directory = output.parent / (output.stem + "-pages") / identity
    paths = [output] + [directory / f"page-{i+1:05}.html" for i in range(1, count)]
    records = iter(load_records(dataset, limit=total, order=order, seed=seed))
    bytes_written = linked = finals = seen = 0
    first = None
    for number, path in enumerate(paths):
        cards, metadata = [], []
        for _ in range(min(page_size, total - seen)):
            row, packet = next(records)
            seen += 1
            card, read = _card(row, packet, seen, links, path.parent)
            cards.append(card); metadata.append(read)
            linked += read["detailsHref"] is not None
            finals += read["finalStepId"] is not None
        nav = {"page": number + 1, "pageSize": page_size, "total": total,
               "start": number * page_size + 1, "end": seen,
               "pages": [_relative(target, path.parent) for target in paths]}
        data = {"schema": "gojo.quickdraw-comparison-gallery.v1", "pagination": nav, "records": metadata,
                "objectColors": COLORS}
        encoded = json.dumps(data, ensure_ascii=False, separators=(",", ":"), allow_nan=False).replace("<", "\\u003c")
        html = _HTML.replace("__DATA__", encoded).replace("__CARDS__", "\n".join(cards))
        path.parent.mkdir(parents=True, exist_ok=True)
        if number == 0:
            first = html
        else:
            _write(path, html)
            bytes_written += path.stat().st_size
    if collection_stamp(dataset) != stamps:
        raise ValueError("The collection changed during gallery generation; build it again when conversion finishes")
    _write(output, first)
    bytes_written += output.stat().st_size
    return {"drawings": seen, "finals": finals, "pages": count, "pageSize": page_size,
            "linkedDrawings": linked, "output": str(output), "htmlBytes": bytes_written,
            "portable": True, "externalRequests": 0,
            "qualification": "Final thumbnails read the last cached authored proposal. They do not execute native stages or modify source evidence."}


def _write(path, html):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(html, encoding="utf-8")
    temporary.replace(path)


_HTML = r'''<!doctype html>
<html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Gojo QuickDraw · Input / final</title>
<style>
:root{color-scheme:light;--ink:#183b38;--muted:#637770;--line:#d9e3dd;--green:#287b6f}*{box-sizing:border-box}body{margin:0;background:#f1f5f0;color:var(--ink);font:14px/1.45 system-ui,sans-serif}header,main,footer{max-width:1480px;margin:auto;padding:14px 24px}header{padding-bottom:0}h1{font-size:25px;margin:2px 0 4px}p{margin:4px 0}.subtle,.key,.cardfoot{color:var(--muted);font-size:12px}.toolbar{display:flex;align-items:center;gap:8px;flex-wrap:wrap;padding:10px 0}.toolbar label{display:flex;align-items:center;gap:6px}button,select{font:inherit;color:var(--ink);background:white;border:1px solid var(--line);border-radius:6px;padding:6px 9px;max-width:100%}button{cursor:pointer}button:disabled{opacity:.4;cursor:default}.toolbar .detail-toggle{margin-left:auto}input{accent-color:var(--green)}.grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}main{padding-top:8px;padding-bottom:0}.comparison{display:block;min-width:0;background:white;border:1px solid var(--line);border-radius:9px;overflow:hidden;text-decoration:none;color:inherit}.comparison[href]:hover{border-color:var(--green);box-shadow:0 2px 8px #183b3814}:focus-visible{outline:3px solid #dcac47;outline-offset:2px}.cardhead{display:flex;justify-content:space-between;gap:8px;align-items:baseline;padding:7px 10px 4px}.cardhead strong{font-size:12px}.key{font-size:10px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.pair{display:grid;grid-template-columns:1fr 1fr}.pair>div{min-width:0}.input{border-right:1px solid #edf1ee}.label{font-size:11px;color:var(--muted);text-align:center;min-height:20px}svg,.missing{width:100%;height:clamp(100px,calc((100vh - 360px)/4),210px);display:block}svg{fill:none;stroke-linecap:round;stroke-linejoin:round;stroke-width:2.7}.input svg{stroke:#5e6e69;color:#5e6e69}.output svg{stroke:var(--green)}.residue{stroke:#9ca9a4;stroke-width:1.5;opacity:.35}.hide-detail .residue{display:none}.missing{display:grid;place-content:center;text-align:center;color:var(--muted);font-size:12px;padding:16px}.cardfoot{padding:4px 10px 6px;font-size:10px;min-height:24px}footer{padding-top:8px;padding-bottom:12px}.bottom-nav{display:flex;justify-content:space-between;align-items:center;gap:8px}footer button{font-size:12px}@media(max-width:1000px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}svg,.missing{height:180px}.toolbar .detail-toggle{margin-left:0}}@media(max-width:650px){header,main,footer{padding-left:12px;padding-right:12px}.grid{grid-template-columns:1fr}svg,.missing{height:auto;aspect-ratio:1}.missing{min-height:150px}h1{font-size:23px}.toolbar{gap:6px}.cardhead strong{font-size:13px}}@media print{@page{size:A4 landscape;margin:8mm}header,main,footer{padding:0}.toolbar,footer,.cardfoot{display:none}.grid{grid-template-columns:repeat(3,minmax(0,1fr));gap:3mm}svg,.missing{height:30mm}header{margin-bottom:3mm}h1{font-size:16px}.comparison{break-inside:avoid}.cardhead{padding-top:3px}}
</style>
<style>.object-ink{display:none}.object-colors .object-ink{display:block;stroke:var(--object-color);stroke-width:3.2}.object-colors .output [style]{stroke:var(--object-color)}#objectLegend{display:flex;gap:12px;flex-wrap:wrap;font-size:12px;padding:0 0 8px}#objectLegend span:before{content:'';display:inline-block;width:9px;height:9px;border-radius:50%;background:var(--role-color);margin-right:5px}</style>
<style>.layout-output{display:none}.area-view .layout-output{display:block}.area-view .final-output,.area-view .input .object-ink{display:none}.area-view .output [style]{stroke:inherit}.area-view #objectLegend{display:none!important}.area-view .input svg{stroke:#465852}</style>
<style>.mapping-output{display:none}.mapping-view .output .layout-output{display:none}.mapping-view .mapping-output{display:block}</style>
<style>.cleanup-output{display:none}.cleanup-view .cleanup-output{display:block}.cleanup-view .layout-output,.cleanup-view .mapping-output,.cleanup-view .final-output{display:none}</style>
<header><div class="subtle">GOJO / QUICKDRAW</div><h1 id="galleryTitle">Input / final interpretation</h1>
<p class="subtle" id="galleryNote">Final = last saved interpretation. Faint lines = retained detail. Each pair shares a viewing frame.</p>
<label>Result <select id="resultView" aria-label="Result view"><option value="final">Final interpretation</option><option value="layout">Area estimates over original</option><option value="mapping">Source ↔ shape mapping</option><option value="cleanup">Church cleanup (final shape stage)</option></select></label>
<nav class="toolbar" aria-label="Gallery pages"><button id="firstPage">First</button><button id="prevPage">← Previous</button><label>Page <select id="page" aria-label="Page"></select></label><button id="nextPage">Next →</button><button id="lastPage">Last</button><span id="position" class="subtle"></span><label><input id="objectColors" type="checkbox">Object colors</label><label class="detail-toggle"><input id="retainedDetail" type="checkbox" checked>Retained detail</label></nav><div id="objectLegend" hidden></div></header>
<main><div class="grid">__CARDS__</div></main>
<footer><div class="bottom-nav"><button id="bottomPrev">← Previous page</button><span class="subtle" id="summary"></span><button id="bottomNext">Next page →</button></div></footer>
<script id="gallery-data" type="application/json">__DATA__</script>
<script>
'use strict';
const DATA=JSON.parse(document.getElementById('gallery-data').textContent),PAGING=DATA.pagination,$=id=>document.getElementById(id);
$('retainedDetail').checked=new URL(location.href).searchParams.get('detail')!=='0';
function detail(){document.body.classList.toggle('hide-detail',!$('retainedDetail').checked)}
function objects(){const active=$('objectColors').checked;document.body.classList.toggle('object-colors',active);$('objectLegend').style.display=active?'flex':'none';for(const card of document.querySelectorAll('a.comparison')){const u=new URL(card.href);u.searchParams.set('objects',active?'1':'0');card.href=u.href}}
function resultView(){const mode=$('resultView').value,area=mode!=='final',mapping=mode==='mapping',cleanup=mode==='cleanup';document.body.classList.toggle('cleanup-view',cleanup);document.body.classList.toggle('area-view',area);document.body.classList.toggle('mapping-view',mapping);$('galleryTitle').textContent=cleanup?'Input / church cleanup':mapping?'Input / source-to-shape mapping':area?'Input / area estimates':'Input / final interpretation';$('galleryNote').textContent=cleanup?'Shape-based refinement over faint original ink. Solid = refined estimate; dashed = added expectation or ground. Open a pair to inspect attachments.':mapping?'Part color = assigned source intervals · ochre = shared support · rose = unresolved. Dotted shapes are proposed boundaries. Open a pair to inspect the mapping.':area?'Pale areas with dotted borders = ideal estimates. Solid ink = original. Hover a shape for its role; open a pair for evidence.':'Final = last saved interpretation. Faint lines = retained detail. Each pair shares a viewing frame.';$('objectColors').disabled=$('retainedDetail').disabled=area;for(const card of document.querySelectorAll('a.comparison')){const row=DATA.records.find(r=>r.selectionId===card.dataset.item),u=new URL(card.href),original=new URL(row.detailsHref,location.href);u.searchParams.set('view',cleanup&&row.cleanupParts?'cleanup':mapping&&row.boundaryMapping?'mapping':area&&row.layoutParts?'layout':original.searchParams.get('view'));card.href=u.href}}
$('resultView').value=['layout','mapping','cleanup','final'].includes(new URL(location.href).searchParams.get('view'))?new URL(location.href).searchParams.get('view'):DATA.records.some(r=>r.cleanupParts)?'cleanup':'final';
$('resultView').onchange=()=>{resultView();const u=new URL(location.href);u.searchParams.set('view',$('resultView').value);history.replaceState(null,'',u.href)};
$('objectColors').checked=new URL(location.href).searchParams.get('objects')==='1'||new URL(location.href).searchParams.get('objects')===null&&DATA.records.some(r=>r.objectColorsAvailable);
for(const [role,color]of Object.entries(DATA.objectColors||{})){const item=document.createElement('span');item.textContent=role==='unknown'?'unknown / ambiguous':role;item.style.setProperty('--role-color',color);$('objectLegend').append(item)}
function goPage(i){if(i<0||i>=PAGING.pages.length)return;const u=new URL(PAGING.pages[i],location.href);u.searchParams.set('detail',$('retainedDetail').checked?'1':'0');u.searchParams.set('objects',$('objectColors').checked?'1':'0');u.searchParams.set('view',$('resultView').value);location.href=u.href}
PAGING.pages.forEach((_,i)=>{const o=document.createElement('option');o.value=i;o.textContent=(i+1)+' of '+PAGING.pages.length;$('page').append(o)});
$('page').value=String(PAGING.page-1);$('page').onchange=()=>goPage(Number($('page').value));
$('firstPage').onclick=()=>goPage(0);$('prevPage').onclick=$('bottomPrev').onclick=()=>goPage(PAGING.page-2);$('nextPage').onclick=$('bottomNext').onclick=()=>goPage(PAGING.page);$('lastPage').onclick=()=>goPage(PAGING.pages.length-1);
$('firstPage').disabled=$('prevPage').disabled=$('bottomPrev').disabled=PAGING.page===1;$('lastPage').disabled=$('nextPage').disabled=$('bottomNext').disabled=PAGING.page===PAGING.pages.length;
$('position').textContent=`Drawings ${PAGING.start}–${PAGING.end} of ${PAGING.total}`;$('summary').textContent=`${PAGING.pageSize} pairs per page · ${PAGING.pages.length} pages`;
$('retainedDetail').onchange=()=>{detail();const u=new URL(location.href);u.searchParams.set('detail',$('retainedDetail').checked?'1':'0');history.replaceState(null,'',u.href)};detail();
$('objectColors').onchange=()=>{objects();const u=new URL(location.href);u.searchParams.set('objects',$('objectColors').checked?'1':'0');history.replaceState(null,'',u.href)};objects();resultView();
document.addEventListener('keydown',e=>{if(['INPUT','SELECT','BUTTON','A'].includes(e.target.tagName))return;if(e.key==='ArrowRight')goPage(PAGING.page);if(e.key==='ArrowLeft')goPage(PAGING.page-2)});
</script></html>'''
