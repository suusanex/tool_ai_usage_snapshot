# AI利用状況スナップショット MVP

このリポジトリは、共通CSVスキーマを手動で取り込む前提で、
AI利用状況を集約して要約レポートを出力する最小実装を置くための実験的実装である。

## 実行方法

```powershell
dotnet run --file ai_usage_snapshot.cs -- --input <input.csv> --output <output-dir> [--as-of <yyyy-MM-dd>]
```

`--as-of` を省略すると、実行時点（UTC）を基準日として扱う。

## 出力ファイル

- `user-summary.csv`
  - ユーザー単位のサービス数、イベント数、状態集計
- `department-summary.csv`
  - 部署単位のユニークユーザー数、ライセンス状態、イベント集計
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
  追加の自動収集（API/認証/RPA/Playwright）は含めない。
- 会話履歴・プロンプト本文・生成物本文は保存対象外。
- 失敗時はフェイルファーストで終了し、フォールバック処理を行わない。
