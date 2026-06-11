# issue #1 AI利用状況スナップショットMVP Implementation Contract Review

- 作成日: 2026-06-11
- Gate: `implementation-contract-review-kernel`
- 入力artifact:
  - `plans/issue-1-ai-usage-snapshot-mvp/implementation-contract.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/change-risk-triage.md`
- Verdict: `READY_FOR_RUNTIME_AND_TEST_DESIGN`
- 実装可否: このGateでは実装不可。

## 1. Review Summary

Implementation Contractは、`change-risk-triage` が要求したIC-01からIC-12を概ね満たしている。特に、`unknown` と `0` の分離、CSVパーサー採用方針、集計単位、候補判定、品質フラグ、禁止操作、エラー処理が実装前の契約として明文化されている。

runtime-contract と test-design に進める状態である。

## 2. Contract Coverage

| Contract ID | Review result | Notes |
| --- | --- | --- |
| IC-01 ファイル配置と実行方式 | PASS | File-based Appと予定パスが定義されている。 |
| IC-02 CSVパーサー採用方針 | PASS | `CsvHelper` 採用理由が明記されている。versionは実装時に固定する。 |
| IC-03 入力スキーマ | PASS | 必須ヘッダーと管理値が定義されている。 |
| IC-04 `unknown` とゼロ/falseの型表現 | PASS | 明示的なunknown状態を持つ値表現が指定されている。 |
| IC-05 ユーザー照合 | PASS | `user_key`、`user_email`、行単位識別子の優先順位が定義されている。 |
| IC-06 部署別利用率の分母 | PASS | 初期分母をCSV登場ユーザーに固定している。 |
| IC-07 候補判定 | PASS | 初期条件、低信頼扱い、出力ファイルが定義されている。 |
| IC-08 データ品質フラグ | PASS | 主要フラグが定義されている。 |
| IC-09 出力ファイル一覧 | PASS | 必須出力が定義されている。 |
| IC-10 本文系データ非保存 | PASS | 禁止境界と簡易警告の範囲が定義されている。 |
| IC-11 禁止操作 | PASS | ログイン、RPA、secret/API token、外部API実行が禁止されている。 |
| IC-12 エラー処理とトレースログ | PASS | フォールバック禁止と `Exception.ToString()` トレースが定義されている。 |

## 3. Blocking Issues

なし。

## 4. Non-blocking Residuals

| ID | Residual | Owner | 次の扱い |
| --- | --- | --- | --- |
| R-ICR-01 | `CsvHelper` の具体version | implementation-execution | 実装時に安定版を選び、File-based Appの `#:package` に固定する。 |
| R-ICR-02 | `body_data_risk` の簡易検査閾値 | implementation-execution | notesの長さ・改行など、本文解析しない範囲で実装する。 |
| R-ICR-03 | 実行出力 `out/` をrepoに残すか | implementation-execution / verification | 生成物として扱い、必要なら `.gitignore` とREADMEに反映する。 |
| R-ICR-04 | 実サービス名 | human / later spike | MVPサンプルでは人工サービス名で進める。 |

## 5. Runtime Contractへの要求

runtime-contractでは次を必ず固定する。

- CLI形式
- 入力・出力パス
- 成功時の生成ファイル
- 失敗時の終了コードとエラー分類
- トレースログ出力
- 禁止操作がruntime surfaceに含まれないこと

## 6. Test Designへの要求

test-designでは次を必ず固定する。

- `0` と `unknown` の区別を確認するテスト
- 欠損列・型不正・管理値不正の失敗テスト
- ユーザー別・部署別・サービス別出力の受け入れテスト
- 品質レポート出力テスト
- 本文系データ非保存境界のレビュー可能性
- 実OS・外部サービスに依存しないこと

## 7. Gate結果

| 項目 | 結果 |
| --- | --- |
| Verdict | `READY_FOR_RUNTIME_AND_TEST_DESIGN` |
| Blocking issue | `None` |
| Runtime contract allowed | `Yes` |
| Test design allowed | `Yes` |
| Production edit allowed | `No` |

