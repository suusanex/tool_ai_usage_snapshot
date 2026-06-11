# issue #1 AI利用状況スナップショットMVP Change Risk Triage

- 作成日: 2026-06-11
- Gate: `change-risk-triage`
- 入力artifact:
  - `plans/issue-1-ai-usage-snapshot-mvp/parent-plan.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/plan-kernel.md`
- Verdict: `IMPLEMENTATION_CONTRACT_REQUIRED`
- 実装可否: このGateでは実装不可。次Gateで実装契約を作成する。

## 1. 判定概要

この変更は、単純なCSV読込ツールではなく、値の意味、プライバシー境界、品質表示、候補判定、将来の実サービスCSV調査を含む。実装前に `implementation-contract-kernel` で具体的な入出力、型、管理値、候補判定、エラー形式を固定する必要がある。

推奨ルートは、標準のPlan網羅チェック・残件判定フローである。現時点では full-coverage 3 layer route は不要。ただし、サービス固有CSV変換や複数の実サービス対応を同時に実装範囲へ入れる場合は、slice decomposition を再検討する。

## 2. リスク分類

| Risk ID | リスク | 分類 | 重大度 | 発生可能性 | 判定 |
| --- | --- | --- | --- | --- | --- |
| R-01 | `unknown` と `0` の混同 | Data Semantics | High | Medium | Contract必須 |
| R-02 | 本文系データが `notes` やサンプルに混入する | Privacy Boundary | High | Medium | Contract必須 |
| R-03 | 手動CSVの列揺れで共通スキーマ読込とサービス固有変換が混ざる | Scope / Data Ingestion | High | High | Contract必須 |
| R-04 | 休眠候補・未利用候補が断定表示される | Reporting Semantics | Medium | Medium | Contract必須 |
| R-05 | 外部ログイン、RPA、API token、外部API実行へスコープが広がる | Scope Control / Security | High | Low | 明示的禁止 |
| R-06 | CSVパーサー独自実装で引用符・改行・エスケープを誤る | Implementation Realization | Medium | Medium | OSS検討必須 |
| R-07 | File-based Appsとテスト配置の整合が曖昧 | Build / Test | Medium | Medium | Contract必須 |
| R-08 | 部署別利用率の分母が未定義 | Metric Semantics | Medium | High | Contract必須 |
| R-09 | データ品質レポートで低信頼・古いデータ・未取得サービスが見落とされる | Reporting Coverage | Medium | Medium | Contract必須 |
| R-10 | 実サービスCSVをrepoに保存してしまう | Privacy / Data Handling | High | Low | Human decision required |

## 3. Implementation-realization risk

判定: `Yes`

理由:

- 三値状態を含む値表現が必要であり、単純な `int` / `bool` では受け入れ条件を満たせない。
- CSVパースはBCLだけでは実用的な引用符・改行・エスケープ対応が弱いため、OSS採用または採用しない理由の明記が必要である。
- 集計ロジックは、ユーザー別・部署別・サービス別で分母と重複扱いが異なる。
- 品質表示と候補判定は、出力上の誤読を避けるため、判定理由と信頼度を含めた仕様化が必要である。
- READMEまたは設計メモへのデータ境界記載が受け入れ条件になっている。

次Gateで `implementation-contract-kernel` を実行する。

## 4. Full-coverage risk

判定: `No for current MVP scope`

理由:

- 親PlanはSlice 1-5に分解済みであり、MVPは共通スキーマCSV入力に限定されている。
- 外部ログイン、RPA、secret/API token、外部API本番実行は非対象として固定されている。
- サービス固有CSV変換は調査スパイクまたは後続sliceへ分離されている。

再判定条件:

- 複数サービス固有CSVの変換実装を同時に入れる場合。
- 実サービスからの自動取得をMVPへ含める場合。
- 課金額按分や監査ログ連携など、親Planの非対象範囲を取り込む場合。

## 5. Contractで固定すべき項目

`implementation-contract-kernel` では次を必須決定事項とする。

| Contract ID | 決定項目 | 必要理由 |
| --- | --- | --- |
| IC-01 | ファイル配置と実行方式 | File-based Apps、README、docs、samples、out、testsの境界を固定する。 |
| IC-02 | CSVパーサー採用方針 | OSS採用または独自実装理由を明記する。 |
| IC-03 | 入力スキーマの必須列・任意列・管理値 | 検証エラーとunknown変換を安定させる。 |
| IC-04 | `unknown` と数値ゼロ・bool falseの型表現 | AC-05の中核条件を満たす。 |
| IC-05 | `user_key` と `user_email` の照合優先順位 | ユーザー別集計のキーを固定する。 |
| IC-06 | 部署別利用率の分母 | AC-03の意味を固定する。 |
| IC-07 | 休眠候補・ライセンス未利用候補の初期条件 | 候補表示の誤読を抑える。 |
| IC-08 | データ品質フラグ | AC-06を満たす。 |
| IC-09 | 出力ファイル一覧と列 | Result reporting instructionsに対応する。 |
| IC-10 | 本文系データ非保存の境界 | AC-07とプライバシーリスクに対応する。 |
| IC-11 | 禁止操作 | RPA、ログイン、secret/API token、外部API本番実行を除外する。 |
| IC-12 | エラー処理とトレースログ | AGENTS.mdの例外処理ルールに対応する。 |

## 6. Runtime Contractへの引き継ぎ候補

次々Gateの `runtime-contract-kernel` では、少なくとも次を扱う。

- CLI引数: 入力CSVパス、出力ディレクトリ、基準日または取込日時の扱い
- 成功時出力: ユーザー別、部署別、サービス別、品質レポート
- 失敗時出力: 欠損列、型不正、管理値不正、ファイル読込失敗
- 禁止操作: 外部ログイン、RPA、secret/API token、外部API実行
- ログ: 例外発生時の `Exception.ToString()` トレース

## 7. Test Designへの引き継ぎ候補

`test-design-kernel` では、少なくとも次のテストポイントを扱う。

| Test Point ID | 観点 |
| --- | --- |
| TP-01 | 正常な共通スキーマCSVを読み込める。 |
| TP-02 | 必須列欠損で失敗する。 |
| TP-03 | `0` と `unknown` が集計で混同されない。 |
| TP-04 | 複数サービスが同じユーザー軸で並ぶ。 |
| TP-05 | 部署別利用率とライセンス未利用候補が出力される。 |
| TP-06 | サービス別の休眠候補、重複利用者数、信頼度が出力される。 |
| TP-07 | 低信頼・古いデータ・未取得サービスが品質レポートに出る。 |
| TP-08 | 本文系データをサンプルや出力に含めない境界がレビュー可能である。 |
| TP-09 | 外部サービスや実OS状態に依存しない。 |

## 8. Human Required Items

現時点で実装を完全に閉じるために残る人間判断は次の通り。

| ID | 項目 | Gateへの影響 | 初期処理案 |
| --- | --- | --- | --- |
| H-01 | 初期対象サービス名 | サンプルと未取得サービス表示 | 人工サービス名で開始可能。 |
| H-02 | 部署別利用率の分母 | 集計仕様 | 初期値を「CSV登場ユーザー」にしてcontractへ明記する案を検討。 |
| H-03 | ライセンス未利用候補の閾値 | 候補抽出 | 初期条件をcontractで明記し、後で変更可能にする案を検討。 |
| H-04 | 休眠候補の期間閾値 | 候補抽出 | 初期条件をcontractで明記し、後で変更可能にする案を検討。 |
| H-05 | 実サービスCSVのrepo保存可否 | データ取扱 | 実データ保存不可、匿名化サンプルのみ可の方針を検討。 |
| H-06 | Markdownレポートの必須度 | 出力仕様 | CSV必須、Markdown品質レポートを推奨としてcontract化する案を検討。 |

これらは実装開始前にすべて決定していなくても、初期値をcontractに明記すればMVP実装は進行可能である。ただし、実サービスCSVのrepo保存可否は明示的な判断なしに実データを保存しない。

## 9. 推奨Next Gate

- 次Gate: `implementation-contract-kernel`
- 推奨tier: `HIGH_MODEL`
- 実装開始可否: `No`
- READY条件:
  - IC-01からIC-12までが明文化されている。
  - 受け入れ条件AC-01からAC-07への対応がcontractに残っている。
  - 禁止操作とデータ境界がREADMEまたは設計メモへ反映される方針になっている。
  - unresolvedな人間判断がある場合、初期値・保留条件・残件報告方法が明記されている。

## 10. Gate結果

| 項目 | 結果 |
| --- | --- |
| change-risk-triage verdict | `IMPLEMENTATION_CONTRACT_REQUIRED` |
| implementation-realization risk | `Yes` |
| full-coverage route required | `No for current MVP scope` |
| next gate | `implementation-contract-kernel` |
| production edit allowed | `No` |
| tests edit allowed | `No` |
| sample data edit allowed | `No` |

