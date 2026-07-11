# Phase 11 - Full Optional Vision

## Muc tieu

Lam sau MVP neu con thoi gian. Phase nay bat vision that su, nhung van optional va khong duoc pha text/log baseline.

## Scope

- Vision ADR va threat model.
- Safe JPEG/PNG reader, magic-byte/type/size/pixel/frame limits.
- EXIF/GPS/device metadata stripping.
- Canonical preprocessing/downscale/re-encode.
- Visible sensitive-data preflight.
- Multimodal provider adapter cho structured visual evidence.
- Vision prompt/schema package.
- Output validator: region, confidence, entity binding, text bounds.
- Game catalog grounding cho scene/entity/visible state.
- Screenshot EvidenceSource/SourceGroup integration.
- Trust precedence: screenshot khong override trusted metadata/log.
- Optional Worker stage `ExtractingVisualEvidence` voi checkpoint/cache/retry.
- Repro synthesis chi dung validated visual facts.
- Screenshot-context duplicate signal va ranker version moi.
- Vision OFF/ON ablation benchmark.

## Exit gate

Vision full chi dong khi golden screenshot tao visual facts co provenance, ambiguous image khong bi doan thanh confirmed fact, va Vision OFF/provider failure van giu core result usable.
