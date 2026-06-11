# issue #1 AI利用状況スナップショットMVP Implementation Contract

- 作成日: 2026-06-11
- Gate: `implementation-contract-kernel`
- 入力artifact:
  - `plans/issue-1-ai-usage-snapshot-mvp/parent-plan.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/plan-kernel.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/change-risk-triage.md`
- Verdict: `READY_FOR_IMPLEMENTATION_CONTRACT_REVIEW`
- 実装可否: このGateでは実装不可。review、runtime-contract、test-design、handoff-review完了後に実装へ進む。

## 1. 実装方針

MVPは .NET/C# のFile-based Appとして実装する。単一ファイルのCLIツールから開始し、複数ファイル化は現時点では行わない。

初期予定パス:

| 種別 | パス | 備考 |
| --- | --- | --- |
| 実行ツール | `ai_usage_snapshot.cs` | File-based App。 |
| README | `README.md` | 実行方法、データ境界、禁止操作を記載する。 |
| 設計メモ | `docs/design.md` | スキーマ、値表現、品質判定、候補判定を記載する。 |
| 入力サンプル | `samples/input/common-schema-sample.csv` | 人工データのみ。 |
| 出力サンプル | `samples/expected-output/` | テスト期待値または例示用。 |
| 実行出力 | `out/` | 実行時生成物。repoに保存するかは実装時に判断する。 |
| テスト | `tests/` | 実OS・外部サービスに依存しない。 |

## 2. 依存OSS採用方針

CSVパーサーには `CsvHelper` を採用する方針とする。

理由:

- BCLにはRFC 4180相当の汎用CSVパーサーが標準提供されていない。
- 引用符、カンマ、改行、エスケープを独自実装すると実装リスクが高い。
- このMVPの本質はCSV構文解析ではなく、共通スキーマ検証、`unknown`表現、集計、品質表示である。

File-based Appでは `#:package CsvHelper@<version>` を使用する。具体versionは実装時点で安定版を選定し、READMEまたは実装コメントに記録する。

## 3. 入力スキーマ契約

入力CSVは次のヘッダーを持つ。

```csv
period_start,period_end,user_key,user_email,display_name,department,service,license_status,active,active_days,event_count,event_unit,last_activity_at,collection_method,source_confidence,imported_at,notes
```

### 3.1 必須列

全列をヘッダーとして必須とする。値が未取得の場合は空文字ではなく `unknown` を推奨する。ただし、入力互換性のため、空文字は列ごとの規則に従って `unknown` へ正規化できる。

### 3.2 管理値

| 列 | 許可値 |
| --- | --- |
| `license_status` | `licensed`, `unlicensed`, `unknown` |
| `active` | `true`, `false`, `unknown` |
| `collection_method` | `manual_csv`, `manual_aggregate`, `unknown` |
| `source_confidence` | `high`, `medium`, `low`, `unknown` |

`service` と `event_unit` は自由文字列を許可するが、空または `unknown` の場合は品質レポートで警告対象にする。

## 4. 値表現

`0` と `unknown` を混同しないため、内部モデルでは三値または明示的なunknown状態を持つ型を使う。

| 入力 | 内部表現 | 集計時の扱い |
| --- | --- | --- |
| `0` | known numeric zero | 数値ゼロとして集計する。 |
| `false` | known boolean false | 明示的な非アクティブとして扱う。 |
| `unknown` | unknown value | ゼロとして加算しない。 |
| 空文字 | normalized unknown または validation error | 列ごとの規則に従う。 |

推奨内部表現:

- 数値: `KnownValue<int>` または同等の小さな値オブジェクト
- bool: `KnownValue<bool>`
- 日時: `KnownValue<DateTimeOffset>`

リフレクションは使用しない。

## 5. ユーザー照合

ユーザー別集計のキーは次の優先順位で決定する。

1. `user_key` が `unknown` でも空でもない場合は `user_key`
2. それ以外で `user_email` が `unknown` でも空でもない場合は `user_email`
3. どちらも取得不能な場合は `unknown-user:<row-number>` のような行単位識別子を内部的に付与し、品質レポートで警告する。

出力では、個人情報の露出を抑えるため、MVPのユーザー別出力は `user_key`、`user_email`、`display_name` をそのまま含めるが、READMEに手動CSVの取扱注意を記載する。実データをrepoへ保存しない。

## 6. 集計仕様

### 6.1 ユーザー別

出力候補: `out/user-summary.csv`

主な列:

- `user_key`
- `user_email`
- `display_name`
- `department`
- `services_used_count`
- `services_licensed_count`
- `active_services_count`
- `unknown_activity_services_count`
- `last_activity_at_max`
- `quality_flags`

### 6.2 部署別

出力候補: `out/department-summary.csv`

部署別利用率の初期分母は「CSVに登場する当該部署のユーザー数」とする。部署メンバー全体を分母にするには、別途メンバー台帳が必要なためMVPでは扱わない。

主な列:

- `department`
- `csv_user_count`
- `active_user_count`
- `usage_rate_within_csv_users`
- `dominant_service`
- `licensed_inactive_candidate_count`
- `unknown_activity_user_count`
- `quality_flags`

### 6.3 サービス別

出力候補: `out/service-summary.csv`

主な列:

- `service`
- `licensed_user_count`
- `active_user_count`
- `dormant_candidate_count`
- `overlap_user_count`
- `low_confidence_record_count`
- `unknown_activity_record_count`
- `old_data_record_count`
- `quality_flags`

## 7. 候補判定

候補は断定ではなく、常に理由と信頼度を付ける。

| 候補 | 初期条件 | 出力 |
| --- | --- | --- |
| 休眠候補 | `license_status=licensed` かつ `active=false`、または `last_activity_at` が基準日から90日以上前。 | `dormant-candidates.csv` |
| ライセンス未利用候補 | `license_status=licensed` かつ `active=false` かつ `event_count=0`。 | `license-unused-candidates.csv` |
| 併用サービス | 同一ユーザーキーに2以上の `service` が存在する。 | `user-summary.csv` |

`active`、`event_count`、`last_activity_at` のいずれかが `unknown` の場合は、候補判定を弱め、`candidate_confidence=low` または `insufficient_data` として出力する。

## 8. データ品質フラグ

| Flag | 条件 |
| --- | --- |
| `manual_aggregate` | `collection_method=manual_aggregate` |
| `low_confidence` | `source_confidence=low` |
| `unknown_activity` | `active=unknown` または活動系の主要値が `unknown` |
| `old_data` | `imported_at` または `period_end` が基準日から90日以上前 |
| `missing_user_key` | `user_key` と `user_email` がどちらも取得不能 |
| `missing_service` | `service` が空または `unknown` |
| `body_data_risk` | `notes` に本文系データらしき長文が入る疑いがある場合 |

`body_data_risk` はMVPでは簡易検査に留める。本文内容の解析は行わず、長すぎるnotesや改行を含むnotesを警告する程度にする。

## 9. 出力ファイル契約

MVPの必須出力は次とする。

| ファイル | 必須 | 内容 |
| --- | --- | --- |
| `user-summary.csv` | Yes | ユーザー別集計 |
| `department-summary.csv` | Yes | 部署別集計 |
| `service-summary.csv` | Yes | サービス別集計 |
| `data-quality-report.md` | Yes | データ出所、取得方法、信頼度、古いデータ、未取得サービス、禁止データ境界 |
| `dormant-candidates.csv` | Yes | 休眠候補 |
| `license-unused-candidates.csv` | Yes | ライセンス未利用候補 |

Markdownレポートは品質レポートを必須とし、集計本体はCSVを正とする。

## 10. 禁止操作

実装中および実行中に次を行わない。

- 外部サービスへのログイン
- 管理画面RPA/Playwright操作
- secret/API token の入力、保存、参照
- 外部APIの本番実行
- 実サービスCSVのrepo保存
- 会話本文、プロンプト本文、生成結果本文の保存

## 11. エラー処理とログ

- フォールバックで処理を継続しない。
- 入力CSV不正、列欠損、型不正、管理値不正は明示的な失敗として返す。
- 例外を捕捉して終了コードへ変換する場合は、トレースログへ `Exception.ToString()` を出力する。
- CLIの標準エラーには、ユーザー向けの短いエラー概要を出す。
- 詳細なスタックトレースはトレースログに出す。

## 12. 実装対象外として残す項目

| 項目 | 扱い |
| --- | --- |
| サービス固有CSVから共通スキーマへの変換 | 後続spikeまたは別slice。 |
| 部署メンバー台帳を使った真の利用率 | 後続要件。 |
| 実サービスAPI連携 | 非対象。 |
| 課金額按分 | 非対象。 |
| 本文データ検出の高度なDLP | 非対象。 |

## 13. Gate結果

| 項目 | 結果 |
| --- | --- |
| Verdict | `READY_FOR_IMPLEMENTATION_CONTRACT_REVIEW` |
| Contract completeness | `Non-trivial` |
| Review required | `Yes` |
| Next gate | `implementation-contract-review-kernel` |
| Production edit allowed | `No` |

