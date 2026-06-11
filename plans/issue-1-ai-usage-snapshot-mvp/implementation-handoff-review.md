# issue #1 AI利用状況スナップショットMVP Implementation Handoff Review

- 作成日: 2026-06-11
- Gate: `implementation-handoff-review`
- 入力artifact:
  - `plans/issue-1-ai-usage-snapshot-mvp/parent-plan.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/plan-kernel.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/change-risk-triage.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/implementation-contract.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/implementation-contract-review.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/runtime-contract.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/test-design.md`
- Verdict: `READY_FOR_BOUNDED_IMPLEMENTATION`
- 実装可否: このhandoff後に、bounded implementationとして実装へ進行可能。

## 1. Handoff Summary

実装前に必要なPlan、risk triage、implementation contract、contract review、runtime contract、test designが揃っている。MVP範囲は共通スキーマCSV入力に限定され、外部ログイン、RPA、secret/API token、外部API本番実行、実データ保存は非対象として固定されている。

実装はSlice 1から開始し、README/設計メモ/人工サンプルCSVでデータ境界と `unknown` 方針を先に固定する。その後、CSV読込、正規化、集計、品質レポート、テストへ進む。

## 2. Artifact Chain Review

| Artifact | Status | Notes |
| --- | --- | --- |
| `parent-plan.md` | PASS | 全体像、slice、受け入れ条件、非ゴールが定義済み。 |
| `plan-kernel.md` | PASS | 実装対象範囲、非対象範囲、AC ledger、coverage ledgerが定義済み。 |
| `change-risk-triage.md` | PASS | `IMPLEMENTATION_CONTRACT_REQUIRED`、full-coverage不要と判定済み。 |
| `implementation-contract.md` | PASS | 実装方式、値表現、集計、候補判定、品質フラグ、禁止操作が定義済み。 |
| `implementation-contract-review.md` | PASS | blocking issueなし。 |
| `runtime-contract.md` | PASS | CLI、入出力、終了コード、runtime scenarioが定義済み。 |
| `test-design.md` | PASS | AC対応、test point、sample data requirementsが定義済み。 |

## 3. Parent Plan Coverage

| AC ID | Handoff status | 実装時の証跡 |
| --- | --- | --- |
| AC-01 共通スキーマCSVを読み込める | READY | CSV reader、正常系テスト、CLI実行結果 |
| AC-02 複数サービスを同じユーザー軸で並べた出力 | READY | `user-summary.csv` |
| AC-03 部署別集計 | READY | `department-summary.csv` |
| AC-04 サービス別集計 | READY | `service-summary.csv` |
| AC-05 `unknown` とゼロの分離 | READY | 値型、テスト、出力例 |
| AC-06 品質表示 | READY | `data-quality-report.md` |
| AC-07 本文系データ非保存の明記 | READY | `README.md` または `docs/design.md` |

## 4. Bounded Implementation Scope

実装で許可する範囲:

- `README.md`
- `docs/design.md`
- `samples/input/common-schema-sample.csv`
- `ai_usage_snapshot.cs`
- テスト用ファイル
- 必要なプロジェクト/テスト設定ファイル

実装で禁止する範囲:

- 外部サービスへのログイン
- RPA/Playwrightによる管理画面操作
- secret/API tokenの入力、保存、参照
- 外部API本番実行
- 実サービスCSVのrepo保存
- 本文系データの保存
- 親Planの非対象範囲の実装

## 5. Implementation Order

推奨順序:

1. READMEと設計メモでデータ境界、`unknown` 方針、禁止操作を明記する。
2. 人工サンプルCSVを作成する。
3. File-based AppでCSV読込・検証・正規化を実装する。
4. ユーザー別・部署別・サービス別集計を実装する。
5. 休眠候補・ライセンス未利用候補を実装する。
6. データ品質レポートを実装する。
7. UnitTest / IntegrationTestを追加する。
8. `dotnet test` とサンプルCLI実行で検証する。

## 6. Handoff Risks

| Risk | Status | 実装時の注意 |
| --- | --- | --- |
| `unknown` とゼロの混同 | Controlled | 明示的なunknown状態を持つ型を使う。 |
| CSVパースの独自実装リスク | Controlled | `CsvHelper` を採用する。 |
| 候補判定の断定表示 | Controlled | 候補理由と信頼度を出す。 |
| 本文系データ混入 | Controlled | サンプルに含めず、notesは簡易警告に留める。 |
| 実サービス調査へのスコープ拡大 | Controlled | 手動CSV形状確認は後続spikeへ分離する。 |
| テストが外部環境に依存する | Controlled | 人工CSVとローカル出力だけで検証する。 |

## 7. Residuals

| ID | Residual | 実装への影響 | 扱い |
| --- | --- | --- | --- |
| HR-01 | 初期対象サービス名 | 低 | 人工サービス名で開始可能。 |
| HR-02 | 実サービスCSV保存可否 | 中 | 実データは保存しない。匿名化サンプルのみ検討。 |
| HR-03 | `CsvHelper` version | 低 | 実装時に固定する。 |
| HR-04 | テスト構成 | 中 | 実装時にrepo形態へ合わせる。 |

## 8. Implementation Authorization

| 項目 | 判定 |
| --- | --- |
| Bounded implementation allowed | `Yes` |
| Full-coverage decomposition required | `No` |
| External operation required | `No` |
| Human decision blocking implementation | `No` |
| Human decision blocking real-data spike | `Yes` |

## 9. Next Gate

- 次Gate: `implementation-execution`
- 推奨実装開始範囲: Slice 1から開始するbounded implementation
- 実装時のsource of truth:
  - `implementation-handoff-review.md`
  - `implementation-contract.md`
  - `runtime-contract.md`
  - `test-design.md`

## 10. Gate結果

| 項目 | 結果 |
| --- | --- |
| Verdict | `READY_FOR_BOUNDED_IMPLEMENTATION` |
| Implementation owner | `standard-implementer` |
| Verification owner | `standard-verifier` |
| Production edit allowed after this gate | `Yes, bounded to handoff scope` |
| Remaining manual-only item | 実サービスCSVを扱うspikeの入力可否判断 |

