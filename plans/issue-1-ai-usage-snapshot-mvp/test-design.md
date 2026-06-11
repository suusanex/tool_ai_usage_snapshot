# issue #1 AI利用状況スナップショットMVP Test Design

- 作成日: 2026-06-11
- Gate: `test-design-kernel`
- 入力artifact:
  - `plans/issue-1-ai-usage-snapshot-mvp/plan-kernel.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/implementation-contract.md`
  - `plans/issue-1-ai-usage-snapshot-mvp/runtime-contract.md`
- Verdict: `READY_FOR_IMPLEMENTATION_HANDOFF_REVIEW`
- 実装可否: このGateでは実装不可。

## 1. Test Strategy

テストは、実OS状態、外部サービス、認証、管理画面、API tokenに依存しない。入力は人工CSVサンプルだけを使う。

UnitTestでは値変換、検証、集計、候補判定を確認する。IntegrationTestではCLIに人工CSVを渡し、出力ファイルと終了コードを確認する。

## 2. Acceptance Criteria Mapping

| AC ID | Test Point | Test type |
| --- | --- | --- |
| AC-01 | 共通スキーマCSVを読み込める。 | Unit / Integration |
| AC-02 | 複数サービスを同じユーザー軸で並べた出力がある。 | Integration |
| AC-03 | 部署別に利用者数、利用率、サービス偏り、ライセンス未利用候補を確認できる。 | Unit / Integration |
| AC-04 | サービス別に利用者数、休眠候補、重複利用者数、データ信頼度を確認できる。 | Unit / Integration |
| AC-05 | `unknown` と利用ゼロを混同しない。 | Unit / Integration |
| AC-06 | 手動集計・低信頼・古いデータ・未取得サービスが明示される。 | Integration |
| AC-07 | 本文系データを保存しない境界がREADMEまたは設計メモに明記されている。 | Documentation check |

## 3. Test Points

| Test Point ID | 観点 | 入力 | 期待結果 |
| --- | --- | --- | --- |
| TP-01 | 正常CSV | 複数部署・複数サービスの人工CSV | 終了コード0、必須出力生成。 |
| TP-02 | 必須列欠損 | `service` などを欠いたCSV | 終了コード4。 |
| TP-03 | 型不正 | `active_days=abc` | 終了コード5。 |
| TP-04 | 管理値不正 | `active=yes` | 終了コード5。 |
| TP-05 | `0` と `unknown` | `event_count=0` と `event_count=unknown` が混在 | unknownがゼロ加算されない。 |
| TP-06 | ユーザー軸 | 同一 `user_key` に複数service | `services_used_count` が2以上。 |
| TP-07 | 部署別利用率 | 同一部署にactive true/false/unknown | CSV登場ユーザー分母で利用率が出る。 |
| TP-08 | サービス別 | 複数ユーザー・複数サービス | 利用者数、休眠候補、重複利用者数が出る。 |
| TP-09 | 低信頼 | `source_confidence=low` | 品質レポートに低信頼が出る。 |
| TP-10 | 古いデータ | `period_end` が基準日から90日以上前 | 品質レポートに古いデータが出る。 |
| TP-11 | 未取得サービス | `service=unknown` | 品質レポートに未取得または不明サービスが出る。 |
| TP-12 | 本文系データ境界 | notesに長文または改行 | 本文内容は保存・解析せず、警告に留める。 |
| TP-13 | 禁止操作 | テスト環境 | 外部ログイン、RPA、API呼び出しが不要。 |
| TP-14 | 例外ログ | 入力ファイル不正 | `Exception.ToString()` 相当の詳細がtraceに残る。 |

## 4. Sample Data Requirements

人工サンプルCSVには次を含める。

- 2部署以上
- 3ユーザー以上
- 2サービス以上
- `active=true`, `active=false`, `active=unknown`
- `event_count=0`, 正の数値, `unknown`
- `source_confidence=high`, `low`
- `collection_method=manual_csv`, `manual_aggregate`
- 古い `period_end` または `imported_at`
- `service=unknown` の行

実ユーザー情報、実メールアドレス、会話本文、プロンプト本文、生成結果本文は含めない。

## 5. Documentation Checks

READMEまたは設計メモに次が記載されていることを確認する。

- `0` と `unknown` の扱い
- 本文系データを保存しない境界
- RPA/Playwrightを使わない境界
- 外部ログイン、secret/API token、外部API本番実行を使わない境界
- 手動集計・低信頼データ・古いデータ・未取得サービスの表示方針

## 6. Verification Commands

実装後に想定する検証コマンド:

```powershell
dotnet test
dotnet run ai_usage_snapshot.cs -- --input samples/input/common-schema-sample.csv --output out/sample --as-of 2026-06-11
```

File-based App単体でテスト構成を持たない場合は、実装時に repo 方針に沿ってテスト実行方法を確定する。

## 7. Gate結果

| 項目 | 結果 |
| --- | --- |
| Verdict | `READY_FOR_IMPLEMENTATION_HANDOFF_REVIEW` |
| Test points | `TP-01` - `TP-14` |
| External dependency | `No` |
| Production edit allowed | `No` |

