# Phase 7 - Vision Safe-Off MVP

## 1. Muc tieu va pham vi

Giu screenshot dung vi tri optional feature trong proposal ma khong de vision chan release:

```text
Screenshot attachment
  -> private storage tu Phase 1
  -> ExtractingVisualEvidence checkpoint
  -> Skipped khi Vision:Enabled=false
  -> warning/degraded khi provider unavailable
  -> text/log repro, duplicate va QA van chay
```

MVP bat buoc hoan thanh safe-off. Visual extraction that chi la tuy chon nho neu con thoi gian.

## 2. Diem noi voi source hien tai

- Intake da nhan PNG/JPEG va luu private qua `AttachmentType.Screenshot`.
- `ProcessAnalysisCommandHandler` dang chay cac stage theo thu tu va persist checkpoint.
- `AnalysisStage`/`AnalysisRun.TransitionStage` quy dinh state machine va progress.
- `AnalysisWarning` da duoc persist/tra qua analysis result.
- Phase 6 them `EvidenceSourceType.Screenshot`; neu Phase 6 chua them thi them tai day.

Khong sua duplicate/repro/QA de bat buoc co screenshot. Screenshot khong duoc lam direct proof neu vision stage skipped/fail.

## 3. Behavior chot truoc khi code

### 3.1 Config modes

| Config/state | Stage result | Analysis result |
|---|---|---|
| `Vision:Enabled=false` | `Skipped` | Warning `VISION_DISABLED` hoac `VISION_SKIPPED` |
| Enabled, khong co screenshot | `Skipped` | `VISION_NO_SCREENSHOT`; core flow tiep tuc |
| Enabled, extractor thanh cong | `Completed` | Optional visual facts |
| Enabled, provider/config unavailable | `Degraded` | `VISION_PROVIDER_UNAVAILABLE`; core flow tiep tuc |
| File khong decode/invalid | `Degraded` cho file do | `VISION_IMAGE_INVALID`; khong fabricate fact |

MVP mac dinh `Vision:Enabled=false` trong ca API va Worker. `Required=false` la co dinh; khong co production mode bat vision trong phase nay.

### 3.2 Public contract

- Analysis status/checkpoint cho thay `ExtractingVisualEvidence` voi outcome `Skipped`, `Completed` hoac `Degraded`.
- Warning chi mo ta stage, khong tiet lo file name/storage key/provider payload.
- `qualityOutcome` cua Phase 6 co the la `PassedWithWarnings` khi vision skipped, nhung `allowedActions` core khong bi mat chi vi vision off.

## 4. Work package 7.1 - Options va startup validation

### Them moi

Trong `src/GameBug.Application/Vision/`:

- `VisionOptions.cs`: `Enabled`, `Required` (MVP luon false), `Provider`, `StageVersion`, `TimeoutSeconds`, `MaxImagesPerAnalysis`.
- `VisionStageOutcome.cs`: `Completed`, `Skipped`, `Degraded` neu checkpoint payload can enum rieng.

### Sua config

Trong ca:

- `src/GameBug.Api/appsettings.json`.
- `src/GameBug.Worker/appsettings.json`.

Them:

```json
"Vision": {
  "Enabled": false,
  "Required": false,
  "Provider": "Disabled",
  "StageVersion": "vision-safe-off-v1",
  "TimeoutSeconds": 20,
  "MaxImagesPerAnalysis": 2
}
```

Sua `src/GameBug.Application/DependencyInjection.cs` hoac `Infrastructure/DependencyInjection.cs` tuy noi dat options:

- Bind va validate range.
- Khi disabled, khong validate provider secret va khong khoi tao provider client.
- Khi enabled ma provider/config thieu, uu tien register unavailable extractor tra degraded; khong lam API/Worker crash trong MVP safe-off.

Them `tests/GameBug.Application.UnitTests/VisionOptionsTests.cs` cho default off va invalid limits.

## 5. Work package 7.2 - Them stage vao state machine

### Sua Domain

- `src/GameBug.Domain/Analysis/AnalysisStage.cs`: them `ExtractingVisualEvidence` sau `ExtractingEvidence`.
- `src/GameBug.Domain/Analysis/AnalysisRun.cs`:
  - Cho transition `ExtractingEvidence -> ExtractingVisualEvidence -> GroundingGameContext`.
  - Cap nhat `StageStartPercent`/`StageCompletePercent` de progress khong lui; goi y visual 45-50, grounding 50-60.
  - Restore checkpoint cua visual stage phai hop le.

Khong them status terminal moi vao `AnalysisStatus`; skipped/degraded la stage outcome + warning, khong phai analysis failure.

### Sua contracts/query

- `src/GameBug.Contracts/BugReports/AnalysisContracts.cs`: enum/string stage response chap nhan `ExtractingVisualEvidence`; neu co checkpoint response thi them outcome.
- `src/GameBug.Api/Endpoints/BugReports/AnalysisEndpoints.cs`: map stage moi, khong default ve unknown.

### Tests

- Mo rong `tests/GameBug.Domain.UnitTests/AnalysisDomainTests.cs`: transition va progress stage moi.
- Mo rong analysis query tests: stage moi serialize dung.

## 6. Work package 7.3 - Visual extractor port va safe-off implementation

### Them port

Trong `src/GameBug.Application/Abstractions/Vision/`:

- `IVisualEvidenceExtractor.cs`.
- `VisualExtractionRequest.cs`: analysis id, attachment descriptor/stream handle toi thieu.
- `VisualExtractionResult.cs`: outcome, facts, warning codes, provider/version metadata.

Port khong tra raw provider response va khong dua Infrastructure type vao Application.

### Them implementation bat buoc

Trong `src/GameBug.Infrastructure/Vision/`:

- `DisabledVisualEvidenceExtractor.cs`: deterministic `Skipped`, zero facts, warning `VISION_DISABLED`.
- `UnavailableVisualEvidenceExtractor.cs`: deterministic `Degraded`, zero facts, warning `VISION_PROVIDER_UNAVAILABLE`.

Sua `src/GameBug.Infrastructure/DependencyInjection.cs`:

- `Enabled=false` -> register disabled implementation.
- Enabled nhung provider chua co -> unavailable implementation.
- Khong co network/provider call trong safe-off path.

### Tuy chon mock nho

Neu can demo screenshot, them:

- `FixtureVisualEvidenceExtractor.cs`, chi nhan fixture allowlisted theo checksum.
- Fixture tai `tests/Fixtures/Vision/golden-*.png` va expected JSON.
- Ket qua toi da mot vai visual fact co source `Screenshot` va source ref la attachment ID.

Khong parse OCR/region/entity that trong phase nay; chuyen sang Phase 11.

## 7. Work package 7.4 - Chen stage vao pipeline

Sua `src/GameBug.Application/Analysis/ProcessAnalysis/ProcessAnalysisCommandHandler.cs`:

1. Sau `ExtractingEvidence`, tinh visual input hash tu ordered screenshot attachment IDs/checksums + vision enabled/provider/stage version.
2. Tim checkpoint `ExtractingVisualEvidence`.
3. Neu checkpoint hop le, restore outcome/warnings/fact references.
4. Neu chua co checkpoint, transition vao stage moi.
5. Khi disabled/no screenshot, tao completed checkpoint co payload outcome `Skipped`; khong mo stream va khong goi extractor provider.
6. Khi enabled, chi doc attachment `AttachmentType.Screenshot` qua `IObjectStorageReader`, gioi han count va cancellation/timeout.
7. Provider/IO failure duoc catch tai boundary stage, chuyen thanh `Degraded` + warning; khong nem loi lam fail toan analysis.
8. Chi persist visual facts da validate; zero fact la ket qua hop le.
9. Complete stage va tiep tuc `GroundingGameContext`.

Checkpoint payload goi y:

```json
{
  "outcome": "Skipped",
  "evidenceFactIds": [],
  "provider": "Disabled",
  "stageVersion": "vision-safe-off-v1",
  "processedAttachmentCount": 0
}
```

Khong ghi raw image, OCR text hoac provider response trong checkpoint.

### Evidence persistence

- Tai su dung `IAnalysisRunRepository.SaveEvidencePackAsync` neu merge visual facts an toan.
- Neu method hien tai luu ca pack va de trung existing facts, them method rieng `SaveEvidenceFactsAsync(analysisRunId, facts)` thay vi save lai log facts.
- `EvidenceSource.SourceRef` dung attachment ID, khong dung storage key/original filename.

## 8. Work package 7.5 - Trust/result interaction

Sua trust orchestration cua Phase 6:

- Vision `Skipped/Degraded` tao non-blocking violation/warning.
- Khong downgrade confirmed text/log facts chi vi vision off.
- Screenshot source chi hop le neu visual fact thuc su duoc extractor tao va thuoc attachment cua cung report/run.
- `CreateNew`/`MarkDuplicate` khong duoc them dieu kien `vision completed`.

Sua `GetAnalysisResultQueryHandler.cs` va contract:

- Tra warning/stage outcome.
- Neu co mock visual facts, tra qua evidence collection hien co.
- Khong them empty/fabricated visual fact khi skipped.

## 9. Work package 7.6 - Tests

### Unit tests

Them `tests/GameBug.Application.UnitTests/VisionStageTests.cs`:

- Disabled -> extractor/provider khong duoc goi, checkpoint Skipped, warning dung.
- No screenshot -> Skipped.
- Unavailable -> Degraded, pipeline tiep tuc.
- Checkpoint restore -> khong xu ly lai image.
- Visual input hash thay khi checksum/stage version thay.

Mo rong `AnalysisPipelineTests.cs`:

- Vision off van ket thuc `AwaitingQaReview`.
- Core repro fields va duplicate candidates giong baseline vision-off truoc khi them stage.
- Provider exception khong chuyen analysis sang Failed.

### Integration/functional tests

- Persist/restore visual checkpoint outcome va warning.
- GET analysis result serialize stage/warning dung.
- QA CreateNew/MarkDuplicate van chay khi warning `VISION_DISABLED`.
- Neu lam fixture extractor: invalid/non-fixture image khong tao fact.

## 10. Thu tu implementation

1. Options/default off.
2. AnalysisStage + transition/progress tests.
3. Port + disabled/unavailable implementations.
4. Pipeline checkpoint safe-off.
5. Result/trust mapping.
6. Regression tests; sau do moi can nhac fixture extractor.

## 11. Khong lam trong phase nay

Chuyen sang [Phase 11 - Full Optional Vision](phase-11-optional-vision-full.md): safe decoder/preprocessor/EXIF stripping, visible PII preflight, multimodal provider, OCR/region/entity grounding, visual trust precedence, screenshot duplicate signal va OFF/ON ablation.

## 12. Exit gate

Phase 7 dong khi `Vision:Enabled=false` van cho full text/log flow den QA decision, visual stage hien ro `Skipped`, provider unavailable hien `Degraded`, va khong truong hop nao fabricate visual evidence. Khong can co visual extractor that de dong phase.
