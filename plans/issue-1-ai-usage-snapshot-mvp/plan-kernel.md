# issue #1 AI利用状況スナップショットMVP Plan Kernel

- 作成日: 2026-06-11
- Gate: `plan-kernel`
- 対象Plan: `plans/issue-1-ai-usage-snapshot-mvp/parent-plan.md`
- Verdict: `READY_FOR_CHANGE_RISK_TRIAGE`
- 実装可否: このGateでは実装不可。次Gateでリスク分類を行う。

## 1. Source of Truth

このartifactは次をsource of truthとして扱う。

- GitHub issue #1「手動CSVインポートでAI利用状況スナップショットMVPを作る」
- `plans/issue-1-ai-usage-snapshot-mvp/parent-plan.md`
- `AGENTS.md`

親Planの目的は、複数AIサービスの利用状況を手動CSVから共通スキーマへ取り込み、ユーザー別・部署別・サービス別の最小レポートとデータ品質表示を作ることである。

## 2. 実装対象範囲

MVPで実装対象とする範囲は次の通り。

| ID | 対象 | 内容 | 状態 |
| --- | --- | --- | --- |
| P1 | 共通スキーマ | issue #1 のCSV列を入力契約として定義する。 | In Scope |
| P2 | 手動CSV読込 | 共通スキーマCSVを読み込み、ヘッダー・型・管理値を検証する。 | In Scope |
| P3 | `0` / `unknown` 分離 | 取得不能と利用ゼロを別状態として保持し、集計で混同しない。 | In Scope |
| P4 | 品質表示 | `collection_method`、`source_confidence`、古いデータ、未取得サービスをレポートに反映する。 | In Scope |
| P5 | 集計出力 | ユーザー別、部署別、サービス別のCSVまたはMarkdownレポートを出力する。 | In Scope |
| P6 | 候補抽出 | 併用サービス数、休眠候補、ライセンス未利用候補を出す。 | In Scope |
| P7 | データ境界 | 会話本文、プロンプト本文、生成結果本文を保存しない境界をREADMEまたは設計メモに明記する。 | In Scope |
| P8 | 検証 | サンプルCSVとテストで主要な受け入れ条件を確認する。 | In Scope |

## 3. 非対象範囲

次はMVPの非対象範囲として維持する。

| ID | 非対象 | 理由 |
| --- | --- | --- |
| N1 | 全AIサービスの完全自動収集 | issue #1 のOut of scope。 |
| N2 | 管理画面RPA/Playwright操作 | ログイン・画面操作・実サービス依存を避けるため。 |
| N3 | 外部サービスへのログイン | secretや認証状態を扱わないため。 |
| N4 | secret/API token の入力や保存 | 機密情報を扱わないため。 |
| N5 | 外部APIの本番実行 | MVPは手動CSV入力に限定するため。 |
| N6 | リアルタイム監視 | バッチ的なスナップショット作成が目的であるため。 |
| N7 | 監査ログ基盤やSIEM連携 | MVPの規模を超えるため。 |
| N8 | 利用量と成果物の因果分析 | 入力データの性質上、因果分析を扱わないため。 |
| N9 | 本文系データの保存・解析 | プライバシー境界として禁止するため。 |

## 4. 受け入れ条件Ledger

| AC ID | issue #1 受け入れ条件 | Plan上の対応 | 証跡候補 | 状態 |
| --- | --- | --- | --- | --- |
| AC-01 | 共通スキーマのCSVを読み込める。 | Slice 2 | 正常CSV読込テスト、実行ログ | Planned |
| AC-02 | 複数サービスを同じユーザー軸で並べた出力がある。 | Slice 4 | ユーザー別出力 | Planned |
| AC-03 | 部署別に利用者数、利用率、サービス偏り、ライセンス未利用候補を確認できる。 | Slice 4 | 部署別レポート | Planned |
| AC-04 | サービス別に利用者数、休眠候補、重複利用者数、データ信頼度を確認できる。 | Slice 4 | サービス別レポート | Planned |
| AC-05 | 取得不能な値は `unknown` として表現され、利用ゼロと混同されない。 | Slice 1-4 | 設計メモ、テスト、出力例 | Planned |
| AC-06 | レポートに手動集計・低信頼データ・古いデータ・未取得サービスが明示される。 | Slice 3-4 | 品質レポート | Planned |
| AC-07 | 本文系データを保存しないことがREADMEまたは設計メモに明記されている。 | Slice 1 | READMEまたは設計メモ | Planned |

## 5. Plan Coverage Ledger

| Plan item | 対応AC | 必要な後続artifact | 実装前の論点 |
| --- | --- | --- | --- |
| 共通スキーマ定義 | AC-01, AC-05 | implementation-contract, runtime-contract, test-design | 型、管理値、空文字の扱い |
| CSV読込・検証 | AC-01, AC-05 | implementation-contract, runtime-contract, test-design | CSV OSS採用、エラー形式 |
| 正規化・品質フラグ | AC-05, AC-06 | implementation-contract, runtime-contract, test-design | `unknown` 型表現、古いデータ判定 |
| ユーザー別集計 | AC-02 | implementation-contract, runtime-contract, test-design | `user_key` と `user_email` の優先順位 |
| 部署別集計 | AC-03 | implementation-contract, runtime-contract, test-design | 利用率の分母 |
| サービス別集計 | AC-04 | implementation-contract, runtime-contract, test-design | 重複利用者数と信頼度の計算 |
| 候補抽出 | AC-03, AC-04 | implementation-contract, test-design | 休眠・未利用閾値 |
| データ境界文書化 | AC-07 | implementation-contract, test-design | READMEと設計メモの分担 |
| データ品質レポート | AC-06 | implementation-contract, runtime-contract, test-design | 未取得サービスの表現方法 |

## 6. Slice境界

親PlanのSlice 1-5を維持する。ただし、実装前に次の依存関係を固定する。

| Slice | 目的 | 依存 | 実装準備状態 |
| --- | --- | --- | --- |
| Slice 1 | 設計・サンプル・README境界 | Plan Kernel, Risk Triage | Risk Triage後にcontract作成可能 |
| Slice 2 | CSV読込・検証 | Slice 1のスキーマ・サンプル方針 | implementation-contract必須 |
| Slice 3 | 正規化・品質フラグ | Slice 2の入力モデル | implementation-contract必須 |
| Slice 4 | 集計出力 | Slice 2-3のモデル | runtime-contractとtest-design必須 |
| Slice 5 | テスト・検証・残件整理 | Slice 1-4 | verification前にtest-design必須 |

## 7. 未確定項目

| ID | 未確定項目 | 影響 | 推奨処理 |
| --- | --- | --- | --- |
| U1 | 最初に想定するAIサービス名の一覧 | サンプルCSVと未取得サービス表示に影響する。 | 人工サンプルでは仮サービス名を使い、実サービス名は後続判断に分離する。 |
| U2 | 部署別利用率の分母 | 利用率の意味が変わる。 | implementation-contractで初期方針を明記する。 |
| U3 | ライセンス未利用候補の閾値 | 候補抽出の精度に影響する。 | 初期値をcontractで定義し、レポートに候補条件を出す。 |
| U4 | 休眠候補の期間閾値 | 候補抽出の精度に影響する。 | 初期値をcontractで定義し、レポートに候補条件を出す。 |
| U5 | 実サービスCSVのrepo保存可否 | プライバシー・運用に影響する。 | 実データは保存しない前提で進め、匿名化サンプルのみ許可する方針を検討する。 |
| U6 | MarkdownレポートをMVP必須にするか | 出力仕様に影響する。 | CSV出力を必須、Markdownは軽量品質レポートとして扱う方針を検討する。 |

## 8. Change Risk Triageへの入力

次Gateでは、少なくとも次のリスクを分類する。

- `unknown` と `0` の型表現・集計混同リスク
- 本文系データの混入リスク
- 手動CSVの列揺れとサービス固有変換リスク
- 候補判定が断定的に見えるリスク
- 外部ログイン、RPA、secret/API token、外部API実行へスコープが拡大するリスク
- File-based AppsとOSS CSVパーサー採用判断の実装実現リスク
- テストで実OS・外部サービスに依存しない設計リスク

## 9. Gate結果

- Verdict: `READY_FOR_CHANGE_RISK_TRIAGE`
- 実装開始可否: `No`
- 次Gate: `change-risk-triage`
- 次Gateの主目的: 実装実現リスク、スコープ拡大リスク、full-coverage要否、contract必須項目を分類する。

