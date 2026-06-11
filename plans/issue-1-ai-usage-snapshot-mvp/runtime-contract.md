# issue #1 AI利用状況スナップショットMVP Runtime Contract

- 作成日: 2026-06-11
- Gate: `runtime-contract-kernel`
- 入力artifact:
  - `plans/issue-1-ai-usage-snapshot-mvp/implementation-contract.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/implementation-contract-review.md`
- Verdict: `READY_FOR_TEST_DESIGN_AND_HANDOFF_REVIEW`
- 実装可否: このGateでは実装不可。

## 1. Runtime Surface

MVPは単一のCLIとして実行する。

```powershell
dotnet run ai_usage_snapshot.cs -- --input <input.csv> --output <out-dir> [--as-of <yyyy-MM-dd>]
```

引数:

| 引数 | 必須 | 内容 |
| --- | --- | --- |
| `--input` | Yes | 共通スキーマCSVのパス。 |
| `--output` | Yes | 出力ディレクトリ。存在しない場合は作成する。 |
| `--as-of` | No | 古いデータ判定と休眠判定の基準日。未指定時は実行日。 |

## 2. Runtime Non-goals

CLIは次を行わない。

- 外部サービスへのログイン
- 管理画面操作
- RPA/Playwright操作
- secret/API token の入力、保存、参照
- 外部API実行
- 実サービスCSVの取得
- 会話本文、プロンプト本文、生成結果本文の保存

## 3. Input Contract

`--input` はImplementation Contractの共通スキーマヘッダーを持つCSVである。

入力検証:

| 検証 | 失敗条件 |
| --- | --- |
| ファイル存在 | `--input` が存在しない。 |
| ヘッダー | 必須ヘッダーが欠けている、または重複している。 |
| 日付 | `period_start`, `period_end`, `last_activity_at`, `imported_at` が日付または `unknown` として解釈できない。 |
| 数値 | `active_days`, `event_count` が非負整数または `unknown` として解釈できない。 |
| 管理値 | `license_status`, `active`, `collection_method`, `source_confidence` が許可値外。 |

## 4. Success Outputs

成功時は `--output` 配下に次を出力する。

| ファイル | 内容 |
| --- | --- |
| `user-summary.csv` | ユーザー別集計。 |
| `department-summary.csv` | 部署別集計。 |
| `service-summary.csv` | サービス別集計。 |
| `dormant-candidates.csv` | 休眠候補。 |
| `license-unused-candidates.csv` | ライセンス未利用候補。 |
| `data-quality-report.md` | データ出所、取得方法、信頼度、古いデータ、未取得サービス、禁止データ境界。 |
| `trace.log` | 実行ログと例外詳細。 |

成功時終了コード:

- `0`: 出力生成成功。品質警告があっても、入力として処理可能であれば成功とする。

## 5. Failure Outputs

失敗時はフォールバックせず、終了コードとエラー概要を返す。

| 終了コード | 分類 | 例 |
| --- | --- | --- |
| `2` | CLI引数不正 | `--input` または `--output` がない。 |
| `3` | 入力ファイル不正 | ファイルが存在しない、読み込めない。 |
| `4` | CSVスキーマ不正 | 必須ヘッダー欠損、重複ヘッダー。 |
| `5` | CSV値不正 | 型不正、管理値不正。 |
| `10` | 予期しない例外 | 未分類例外。 |

失敗時も `trace.log` に `Exception.ToString()` を出力する。`trace.log` を作成できない場合は、標準エラーへ `Exception.ToString()` を出力する。

## 6. Data Quality Runtime Rules

- `unknown` はゼロとして扱わない。
- `source_confidence=low` は品質レポートで明示する。
- `collection_method=manual_aggregate` は手動集計として品質レポートで明示する。
- `period_end` または `imported_at` が `--as-of` から90日以上前の場合、古いデータとして品質レポートで明示する。
- `service=unknown` または空相当は未取得・不明サービスとして品質レポートで明示する。
- `notes` の長文・改行は本文系データ混入の疑いとして警告するが、本文内容の解析は行わない。

## 7. Runtime Scenario Ledger

| Scenario ID | シナリオ | 期待結果 |
| --- | --- | --- |
| RT-01 | 正常CSVを指定して実行する。 | 必須出力ファイルが生成され、終了コード0。 |
| RT-02 | `--input` がない。 | 終了コード2、エラー概要を出す。 |
| RT-03 | 入力ファイルが存在しない。 | 終了コード3、エラー概要とtraceを出す。 |
| RT-04 | 必須ヘッダーが欠けている。 | 終了コード4、欠損列を示す。 |
| RT-05 | `active_days` に不正値がある。 | 終了コード5、行番号と列名を示す。 |
| RT-06 | `unknown` と `0` が混在する。 | 成功し、集計で混同しない。 |
| RT-07 | 低信頼・古いデータが含まれる。 | 成功し、品質レポートに警告が出る。 |
| RT-08 | notesに長文または改行がある。 | 成功し、本文系データ混入疑いの警告が出る。 |

## 8. Gate結果

| 項目 | 結果 |
| --- | --- |
| Verdict | `READY_FOR_TEST_DESIGN_AND_HANDOFF_REVIEW` |
| Runtime scenarios | `RT-01` - `RT-08` |
| External operation required | `No` |
| Production edit allowed | `No` |

