# Small learned models

`joint-pilot-2026-10-27.json` is the first external local construction ranker:
520 weights over native coarse band reads, with five connected target
constructions. It is a pilot opinion provider, not a reviewed church recognizer
or an automatic geometry editor. See [the experiment](../docs/local-joint-model.md).

Load it without an ML runtime:

```python
import json
from gojo_quickdraw.joint_model import predict

with open("models/joint-pilot-2026-10-27.json", encoding="utf-8") as stream:
    model = json.load(stream)
# `bands` must come from the matching native sixteen-cell feature schema.
opinion = predict(model, bands)
```

The original run's native opinion witnesses reference the exact SHA-256 of
this weight file. Original coordinates, labels, identifiers and recognition
outcomes are not accepted by the predictor interface.
