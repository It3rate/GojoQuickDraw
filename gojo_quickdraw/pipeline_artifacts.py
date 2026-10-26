"""Inspectable stage views backed by the saved source and native constructions.

Geometry here is a read cache. The native archive, with ordered source
correspondences and construction programs, remains authoritative.
"""
from __future__ import annotations


def prepare(packet):
    proposal = packet["analysis"].get("proposal")
    if proposal:
        from quickdraw_gestalt import Author
        program, history = Author(packet["observation"], proposal).run()
        packet["analysis"]["constructionProgram"] = program
        packet["analysis"]["constructionHistory"] = history


def capture(packet, native):
    observation = packet["observation"]
    stages = [
        {"id": "original", "title": "Original drawing", "dependsOn": [], "kind": "strokes",
         "profiles": [stroke["points"] for stroke in observation["strokes"]], "evidence": "sourceOriginalJson"},
        {"id": "fitted", "title": "Close fitted outlines", "dependsOn": ["original"], "kind": "profiles",
         "profiles": [span["controls"] for span in observation["spans"]], "evidence": "observation.spans"}]
    proposal = packet["analysis"].get("proposal")
    views = native.get("stageViews") or []
    if proposal:
        if len(views) != len(proposal["steps"]) or not native["summary"].get("drawingStageCorrespondenceVerified"):
            raise ValueError("Every proposed construction rung must execute and restore before publication")
        packet["analysis"]["proposalNativeExecuted"] = True
        for index, (step, view) in enumerate(zip(proposal["steps"], views)):
            if view["index"] != index:
                raise ValueError("Native drawing stages changed order")
            ident = "fitted" if index == 0 else f"outline-{index}"
            saved = {"id": ident, "title": step["title"], "dependsOn": ["original" if index == 0 else "fitted" if index == 1 else f"outline-{index - 1}"],
                     "kind": "profiles", "profiles": view["profiles"], "residue": view["residue"],
                     "nativeRung": index, "nativeRestored": True, "evidence": f"analysis.constructionHistory.passes[{index}]",
                     "operations": step["operations"], "metrics": step["metrics"],
                     "sourceMapping": packet["analysis"]["constructionProgram"]["sourceMapping"]["passes"][index]}
            if index == 0:
                saved["title"] = "Close fitted outlines"
                stages[1] = saved
            else:
                stages.append(saved)
    for section, title, dependencies in (("layout", "Shape areas and attachments", ["fitted"]),
                                          ("cleanup", "Refined church shapes", ["areas"])):
        if section in packet:
            stages.append({"id": "areas" if section == "layout" else "cleanup", "title": title,
                           "kind": "parts", "parts": packet[section]["parts"], "dependsOn": dependencies,
                           "evidence": section, "nativeRestored": True})
    packet["analysis"]["qualification"] = (
        "Authored interpretations, not trained priors. Construction rungs execute and restore in native Gojo. "
        "Area proposals read the fitted source and context; cleanup refines those areas. "
        "This is a branched interpretation history, not independent evidence or a single sequential optimizer.")
    return {"schema": "gojo.quickdraw-pipeline-stages.v1", "itemId": packet["itemKey"],
            "sourceId": packet["source"]["key_id"], "stages": stages,
            "qualification": packet["analysis"]["qualification"],
            "unavailable": [] if proposal else ["Church construction and refinement are not applicable to this category or empty acquisition."]}
