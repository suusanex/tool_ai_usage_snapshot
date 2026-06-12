# AI利用状況スナップショット MVP

このリポジトリは、共通CSVスキーマを手動で取り込み、
複数AIサービスの利用状況を横断比較する最小実装を置くための実験的実装である。

## 実行方法

```powershell
dotnet run --file ai_usage_snapshot.cs -- --input <input.csv> --output <output-dir> [--as-of <yyyy-MM-dd>]
```

`--as-of` を省略すると、実行時点（UTC）を基準日として扱う。

## 共通CSVの考え方

共通CSVは、データソース固有の `event_count` をそのまま持ち寄る方式ではなく、
横断比較用の共通単位として `ai_interaction_count` を持つ。

- `ai_interaction_count`
  - ユーザーがAIへ送った入力1回相当の回数
- `ai_interaction_unit`
  - 初期値は `ai_interactions`
- `ai_interaction_basis`
  - 変換根拠
  - 例: `chatgpt_business_sent_messages`, `codex_usage_turns`, `m365_copilot_prompts_submitted_all_apps`
- `active_days`
  - 期間内に利用が確認できた日数
- `active_surface_count`
  - 利用が確認できたアプリ面数やクライアント面数

## 主な入力列

```text
period_start,period_end,user_key,user_email,display_name,department,service,license_status,active,ai_interaction_count,ai_interaction_unit,ai_interaction_basis,active_days,active_surface_count,last_activity_at,collection_method,source_confidence,imported_at,notes
```

## 出力ファイル

- `user-summary.csv`
  - ユーザー単位のサービス数、利用量、状態集計
- `department-summary.csv`
  - 部署単位のユニークユーザー数、ライセンス状態、利用量集計
- `service-summary.csv`
  - サービス単位の利用状況集計
- `dormant-candidates.csv`
  - 休眠候補ユーザー/サービス
- `license-unused-candidates.csv`
  - ライセンス利用なし候補
- `data-quality-report.md`
  - 欠損、unknown、低信頼データの品質結果
- `trace.log`
  - 例外時の `Exception.ToString()` を含む実行トレース

## 利用制約

- 本実装は共通スキーマのCSV手動インポートのみ対応し、
  追加の自動収集そのものは含めない。
- 会話履歴・プロンプト本文・生成物本文は保存対象外。
- 失敗時はフェイルファーストで終了し、フォールバック処理を行わない。
