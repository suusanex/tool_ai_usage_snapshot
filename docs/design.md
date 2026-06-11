# 設計メモ（issue #1）

## 目的

- 手動CSV（共通スキーマ）を1回で読込んで、ユーザー・部署・サービス単位で集計する。
- `0` と `unknown` を明確に分離し、品質情報（不足・低信頼）を明示する。
- 本文系データ（会話文、プロンプト本文、生成物本文）は取り込まない前提を維持する。

## 入力

CSVヘッダーは以下を必須とする。

`period_start,period_end,user_key,user_email,display_name,department,service,license_status,active,event_count,event_unit,last_activity_at,collection_method,source_confidence,imported_at,notes`

- `license_status` は `licensed` / `unlicensed` / `unknown`
- `active` は `true` / `false` / `unknown`
- `collection_method` は `manual_csv` / `manual_aggregate` / `unknown`
- `source_confidence` は `high` / `medium` / `low` / `unknown`
- `event_count` は `0` / 正整数 / `unknown`
- `last_activity_at` は日付文字列または `unknown`

## 集計ルール

- ユーザーキー決定
  - `user_key` があれば優先
  - 無ければ `user_email`、それも無ければ `unknown-user:<row>`
- サービス単位は `service` 値でユニーク化する（`unknown` も別値として扱う）。
- `active=false` を「休眠/未利用の重要シグナル」として扱う。
- `last_activity_at` が `--as-of` から 90日以上古い場合、休眠候補の補助シグナルにする。
- `event_count=0` かつ `license_status=licensed`、`active=false` は未利用候補の主シグナルとする。
- `active` と `event_count` が `unknown` の場合、候補は作らず low confidence として品質へ記録する。

## 出力

- `user-summary.csv`
  - `user_key` / `user_email` / `display_name` / `department`
  - `service_count`
  - `event_count_total`
  - `event_count_unknown_rows`
  - `licensed_service_count`
  - `active_service_count`
  - `inactive_service_count`
  - `last_activity_at_latest`
  - `dormant_candidate_count`
  - `license_unused_candidate_count`
- `department-summary.csv`
  - `department`
  - `user_count`
  - `licensed_user_count`
  - `active_user_count`
  - `inactive_user_count`
  - `event_count_total`
  - `event_count_unknown_rows`
- `service-summary.csv`
  - `service`
  - `user_count`
  - `licensed_user_count`
  - `active_user_count`
  - `inactive_user_count`
  - `event_count_total`
- `dormant-candidates.csv`
  - `user_key,user_email,display_name,department,service,reason,confidence`
- `license-unused-candidates.csv`
  - `user_key,user_email,display_name,department,service,reason,confidence`
- `data-quality-report.md`
  - 行数、warning 件数、フラグ別集計、警告サンプル
- `trace.log`
  - 実行中に捕捉した全例外を `Exception.ToString()` で追記

## 制約

- CSV本文保存なし、本文列 (`notes`) はレポートや候補判定の判断素材には使わない。
- 自動収集・認証・外部トークン管理・外部API実行は含めない。
- 失敗時は例外を明示し、成功系のフォールバックを行わない。
