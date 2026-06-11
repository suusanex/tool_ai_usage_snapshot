# issue #1 Verification

- 実行日時: 2026-06-11
- ゲート: `verification-kernel`
- 入力artifact:
  - `plans/issue-1-ai-usage-snapshot-mvp/implementation-execution.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/code-review-focus-kernel.md`

## 実行手順

1. サンプル実行
   - `dotnet run --file ai_usage_snapshot.cs -- --input samples/input/common-schema-sample.csv --output out/sample3 --as-of 2026-06-11`
2. テスト実行
   - `dotnet test AiUsageSnapshot.Tests/AiUsageSnapshot.Tests.csproj`

## 検証結果

- 1) サンプル実行: 成功（終了コード0）。
  - 生成ファイル確認:
    - `out/sample3/user-summary.csv`
    - `out/sample3/department-summary.csv`
    - `out/sample3/service-summary.csv`
    - `out/sample3/dormant-candidates.csv`
    - `out/sample3/license-unused-candidates.csv`
    - `out/sample3/data-quality-report.md`
    - `out/sample3/trace.log`
- 2) テスト実行: 成功（2件成功）。
- 例外ログ:
  - 正常系実行時は `trace.log` 追加なし（stderr空）。

## 残件

- 本番データの取り込み前に、`samples/input/common-schema-sample.csv` を実データ準拠データで差し替える想定試験を実施する。

## Residual

- `as-of` の運用ルール（基準日の扱い）を運用チームで確認。

## Verdict

- `verification-kernel` 結果: `PASS`
- 次ゲート: `coverage-gap-triage`
