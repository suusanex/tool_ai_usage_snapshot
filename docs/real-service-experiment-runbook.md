# 実サービス利用状況取得 実験手順書

- 作成日: 2026-06-11
- 対象: `ai_usage_snapshot.cs` の実サービス入力準備
- 関連Plan: `plans/issue-1-ai-usage-snapshot-mvp/residual-decision-gate.md`
- 前提: 対象組織で実際に利用しているサービスだけを扱う。

## 1. 対象範囲

このRunbookで扱う対象サービスは次の5つに限定する。

1. GitHub Copilot
2. Microsoft 365 Copilot
3. ユーザーローカル ChatAI
4. ChatGPT Business
5. Codex（ChatGPT Business契約での利用）

上記以外のAIサービスは、このフェーズでは将来課題として扱う。OpenAI API Platform、Anthropic Claude API、Claude Code、Google Gemini、Cursor、JetBrains AI、Notion AIなどは、このRunbookの実験対象に含めない。

## 2. 結論

MVP実装後の次フェーズは、対象5サービスから取得できる利用状況データを確認し、共通CSVスキーマへ変換できるかを検証する作業である。

公開情報で分かるAPI、権限、CSV export、利用状況画面の有無は先に整理し、人手作業は次に限定する。

1. 実テナント・実契約で対象画面/APIにアクセスできるか確認する。
2. APIキー、OAuthアプリ、管理者同意などの資格情報を発行する。
3. 管理画面にしかない項目は、手動でCSV exportまたは画面確認を行う。
4. 取得した実データを匿名化または一時保管し、共通CSVスキーマへ変換できるか検証する。
5. 公開情報だけでは判断できない列揺れ、匿名化設定、テナント固有制約を確認する。

APIが公開されている対象では、資格情報の発行だけを人手で行い、疎通・レスポンス構造確認・共通CSV変換はスクリプトで検証する。

## 3. 実験の原則

| 原則 | 内容 |
| --- | --- |
| 最小権限 | 読み取り専用、レポート閲覧、Usage/Billing閲覧など、必要最小限の権限で実施する。 |
| 機密値非保存 | APIキー、アクセストークン、Cookie、セッション情報をrepo、チャット、Markdown、CSVへ保存しない。 |
| 本文非保存 | プロンプト本文、会話本文、生成物本文、ソースコード断片、添付ファイル本文を保存しない。 |
| 実データ非コミット | 実サービスCSV、未匿名化CSV、APIレスポンスの生データをrepoへコミットしない。 |
| 匿名化優先 | repoに残すのは列名、型、サンプル値のカテゴリ、匿名化済み最小サンプルに限定する。 |
| 失敗を記録 | 取得不可、権限不足、列不足、unknown化が必要な項目も成果として記録する。 |

推奨するローカル作業場所:

```text
D:\Data\local-ai-usage-snapshot-experiments\<yyyyMMdd>\<service>\
```

## 4. 公開情報で確認済みの概要

| サービス | 公開情報で確認できた取得経路 | API/権限 | CSV/画面エクスポート | スクリプト検証方針 | 人手で残ること |
| --- | --- | --- | --- | --- | --- |
| GitHub Copilot | Copilot usage metrics REST API | Organization: `read:org` または fine-grained `View Organization Copilot Metrics`; Enterprise: `manage_billing:copilot` / `read:enterprise` または Enterprise Copilot metrics read | 管理画面側のexport可否は実環境確認 | REST APIで日次/28日レポートを取得し列を確認 | org/enterprise名、権限付与、token発行 |
| Microsoft 365 Copilot | Microsoft Graph Copilot reports API、Microsoft 365 admin center usage report | `Reports.Read.All`; delegated時はReports Reader等のEntraロール | admin centerでCSV export可能 | Graph APIでJSONまたはCSV形式を取得 | tenant admin consent、匿名化設定、ライセンス有無 |
| ユーザーローカル ChatAI | 管理ダッシュボード | 公開情報上、利用状況取得APIは確認できず | 利用状況管理ダッシュボードあり。CSV export可否は公開情報だけでは未確認 | APIではなく、管理画面/CSVの列確認から開始 | 管理者権限、CSV export可否、部署/個人別粒度 |
| ChatGPT Business | Business workspaceのBilling/credits/spend controls、管理者向けusage analytics | 公開情報上、Workspace Analytics CSV APIは確認できず。Business subscriptionはAPI Platform利用を含まない | Business workspaceの一般データexportは不可。Billing/usage analytics画面の確認が中心 | 管理画面で取得できるcredits/usage情報を手動またはCSV相当で確認 | owner/admin権限、Businessで取得できる実際のusage項目 |
| Codex（ChatGPT Business契約） | ChatGPT Businessのseat/credits/spend controls、Codex rate card | Standard ChatGPT seatはCodex baseline accessを含む。Codex seatはCodex-onlyでusage-based、workspace creditsが必要 | Workspace settings > Billingでcredits/spend controlsを確認 | Codex単体APIではなく、Business workspaceのcredit/seat/usage情報として扱う | Codex seat有無、standard seatでの利用、per-user credit limit/usage確認 |

## 5. API/取得検証スクリプト対象

次は資格情報または実CSVが得られればスクリプトで検証する。資格情報そのものは環境変数またはローカルsecret storeで扱い、repoへ保存しない。

| 優先 | サービス | 検証スクリプト候補 | 入力する秘密情報/入力ファイル | 期待する出力 |
| --- | --- | --- | --- | --- |
| 1 | GitHub Copilot | `experiments/github-copilot-usage-probe.cs` | `GITHUB_TOKEN`, org/enterprise名 | APIステータス、取得列、匿名化サンプル |
| 2 | Microsoft 365 Copilot | `experiments/m365-copilot-usage-probe.cs` | Graph access tokenまたはapp credentials | JSON/CSV列、期間、匿名化有無 |
| 3 | ユーザーローカル ChatAI | `experiments/userlocal-chatai-csv-probe.cs` | 管理画面から取得した匿名化CSVまたは列名サンプル | 列名、型、部署/個人別粒度 |
| 4 | ChatGPT Business | `experiments/chatgpt-business-export-probe.cs` | 管理画面から取得したusage/credits情報の匿名化CSVまたは手動転記CSV | seat/usage/credits列の変換可否 |
| 5 | Codex（ChatGPT Business契約） | `experiments/chatgpt-business-codex-credits-probe.cs` | Billing/credits/spend controlsの匿名化CSVまたは手動転記CSV | Codex利用をservice行へ変換できるか |

スクリプト作成時のルール:

- API取得はGET相当の読み取りに限定する。
- レスポンス全文は保存しない。列名、型、件数、匿名化済み先頭数行だけ保存する。
- エラー時は `Exception.ToString()` をローカルtraceへ出す。
- 実ユーザー識別子は、保存前にhashまたは人工IDへ置換する。
- 成果物は `docs/source-mapping/<service>.md` と匿名化済み `samples/input/<service>-common-schema-sample.csv` へ反映する。

## 6. 共通CSVスキーマへの対応

実験では、各サービスから次の列へ寄せられるかを確認する。

```csv
period_start,period_end,user_key,user_email,display_name,department,service,license_status,active,event_count,event_unit,last_activity_at,collection_method,source_confidence,imported_at,notes
```

| 共通列 | 取得方針 |
| --- | --- |
| `period_start`, `period_end` | レポート期間、API集計期間、エクスポート対象期間から設定する。 |
| `user_key` | サービス側のユーザーID、アカウントID、メールアドレスのハッシュ化IDなどを優先する。 |
| `user_email` | 実データ保存可否が未決の場合は匿名化または空欄にする。 |
| `display_name` | 原則保存しない。必要な場合も匿名化する。 |
| `department` | サービスから取れない場合は人事/IdP側CSVとの結合候補として `unknown` にする。 |
| `service` | `github-copilot`, `m365-copilot`, `userlocal-chatai`, `chatgpt-business`, `codex-chatgpt-business` のいずれか。 |
| `license_status` | 座席割当または契約ユーザーなら `licensed`、未割当なら `unlicensed`、不明なら `unknown`。 |
| `active` | 期間内利用が確認できるなら `true`、ライセンスありで利用ゼロなら `false`、取れなければ `unknown`。 |
| `event_count` | リクエスト数、メッセージ数、アクティブ日数、利用イベント数、credits消費など、サービスで取れる代表値を入れる。取れない場合は `unknown`。 |
| `event_unit` | `requests`, `messages`, `active_days`, `credits`, `events`, `seats` など。 |
| `last_activity_at` | ユーザー単位の最終利用日が取れる場合だけ入れる。 |
| `collection_method` | 手動CSVは `manual_csv`、画面転記や集計値だけなら `manual_aggregate`。 |
| `source_confidence` | ユーザー単位かつ期間が明確なら `high`、集計値や一部欠損ありなら `medium`、画面転記・推定なら `low`。 |
| `imported_at` | 実験で取り込んだ日付。 |
| `notes` | 制約だけを書く。本文やプロンプト内容は書かない。 |

## 7. 共通実施手順

1. 対象サービスの公開情報欄を確認し、API/CSV/画面の候補を選ぶ。
2. 人手で必要な資格情報や管理画面権限だけを用意する。
3. APIがあるサービスでは、まず疎通スクリプトで1日または7日分だけ取得する。
4. 管理画面CSVしかないサービスでは、最小期間でCSVを手動エクスポートする。
5. CSV exportがない場合は、画面上の列名と集計値を匿名化した手動転記CSVで検証する。
6. 取得した列名、型、期間、粒度、権限、注意点を `docs/source-csv-notes.md` に記録する。
7. 共通CSVスキーマへ手動または一時変換で寄せる。
8. 次のコマンドで実行する。

```powershell
dotnet run --file ai_usage_snapshot.cs -- --input <common-schema.csv> --output <out-dir> --as-of <yyyy-MM-dd>
```

9. `data-quality-report.md` を確認し、`unknown`、`low_confidence`、`manual_aggregate`、`old_data` の出方を見る。
10. サービス別に「正式対応する」「変換スパイクが必要」「当面は手動集計のみ」のいずれかを判定する。

## 8. サービス別Runbook

### 8.1 GitHub Copilot

公開情報で分かっていること:

- Copilot usage metrics REST APIがある。
- Organization向けはOrganization ownerまたはfine-grained `View Organization Copilot Metrics` 権限が必要。
- classic PAT/OAuthではOrganization APIに `read:org` が必要。
- Enterprise向けはEnterprise owner、billing manager、fine-grained `View Enterprise Copilot Metrics` 権限などが必要。
- Enterprise classic PAT/OAuthでは `manage_billing:copilot` または `read:enterprise` が必要。
- Enterprise/Organizationの28日レポート、特定日ユーザー利用メトリクスなどのエンドポイントがある。

スクリプトで検証すること:

1. orgまたはenterpriseの対象を指定して、最新28日または特定日のmetrics endpointをGETする。
2. レスポンスのユーザー識別子、日付、機能別イベント、集計値を列名だけ抽出する。
3. GitHub loginを `user_key` にできるか、メール対応表が必要かを記録する。
4. 代表的なイベント数を `event_count` にできるか確認する。

人手で残ること:

- 対象org/enterprise名の決定。
- token発行と権限付与。
- GitHub loginと社内ユーザーID/メールアドレスの対応可否。

暫定判定:

- `READY_FOR_API_PROBE`

### 8.2 Microsoft 365 Copilot

公開情報で分かっていること:

- Microsoft 365 admin centerのCopilot usage reportはCSV export可能。
- Microsoft Graphには `GET /copilot/reports/getMicrosoft365CopilotUsageUserDetail(period='D7')` がある。
- Graph APIのleast privileged permissionは `Reports.Read.All`。
- delegated accessではReports Reader、AI AdministratorなどのEntraロールが必要。
- `D7`, `D30`, `D90`, `D180`, `ALL` の期間指定がある。
- APIレスポンスには `lastActivityDate`、Teams/Word/Excel/PowerPoint/Outlook/OneNote/Loop/Copilot Chatの最終活動日系列が含まれる。
- GraphのCopilot usage APIはMicrosoft 365 Copilotライセンスユーザー対象である。

スクリプトで検証すること:

1. Graph tokenを使い、`period='D7'` のJSON取得を試す。
2. `$format=text/csv` の取得も試し、CSV列を確認する。
3. userPrincipalName/displayNameが匿名化されるテナント設定か確認する。
4. `lastActivityDate` を `active` と `last_activity_at` に変換できるか確認する。

人手で残ること:

- tenant admin consent。
- 実ユーザー識別子の匿名化設定確認。
- Copilot Chat未ライセンス利用をこのプロジェクトで扱うかの判断。

暫定判定:

- `READY_FOR_API_PROBE`

### 8.3 ユーザーローカル ChatAI

公開情報で分かっていること:

- 法人向け生成AIプラットフォームで、ChatGPT、Gemini、Claude、Perplexityなど複数LLMに対応している。
- 従量課金なしの固定料金・無制限利用を前提としているため、課金額やtoken消費より、利用頻度・利用者・部署別状況の確認が主目的になる。
- 社員が生成AIをいつ、どれくらい、どのように使っているかを管理できるダッシュボードがある。
- ユーザーごとの機能制御や部署ごとの利用状況分析に対応している。
- 公開情報上、利用状況取得APIやCSV export仕様は確認できていない。

スクリプトで検証すること:

1. 管理画面からCSV exportできる場合、匿名化CSVを `experiments/userlocal-chatai-csv-probe.cs` で読み取る。
2. CSV exportがない場合、画面の列名と数値だけを手動転記CSVにする。
3. 部署別、個人別、日別、機能別、モデル別のどれが取得できるかを記録する。
4. `event_count` は利用回数、メッセージ数、または画面上の代表指標へ割り当てる。

人手で残ること:

- 管理者権限でダッシュボードにアクセスする。
- CSV exportの有無を確認する。
- 個人別/部署別の粒度、日付範囲、モデル別利用有無を確認する。
- 会話本文やプロンプト本文を含むログがある場合は、このMVPには取り込まない。

暫定判定:

- `MANUAL_DASHBOARD_FIRST`

### 8.4 ChatGPT Business

公開情報で分かっていること:

- ChatGPT Businessはstandard ChatGPT seatとCodex seatの2種類のseatを持つ。
- standard ChatGPT seatはChatGPTとCodexのbaseline accessを含む。
- Codex seatはCodex-onlyのusage-based seatで、ChatGPT accessは含まない。
- ChatGPT Business subscriptionはOpenAI API Platform利用を含まず、API usageは別課金である。
- Business workspace dataは学習に使われない。
- Business workspaceでは、各ユーザーのchat/Codex historyは各自のものとして扱われ、管理者が全員のprivate chatを自動的に読めるわけではない。
- Business workspaceの一般データexportは利用できない。
- spend controlsとusage analyticsは、chat transcript accessとは別の運用情報として扱われる。

スクリプトで検証すること:

1. 管理画面から取得できるBilling/usage/creditsの列を確認する。
2. CSV export相当がある場合は匿名化CSVを読み取る。
3. CSV exportがない場合は、seat数、利用者、usage/creditsの画面値を手動転記CSVにする。
4. `service=chatgpt-business` として、ChatGPT自体のseat/active/usageを共通CSVへ寄せる。

人手で残ること:

- Business workspace owner/admin権限でBilling/usage/credits画面を確認する。
- ChatGPT利用状況がユーザー単位で取れるか、seat/credits単位に留まるか確認する。
- Business workspaceでユーザー別usage exportまたは同等の管理画面表示が使えるか確認する。公開情報上はBusiness前提の同等exportは確認できていない。

暫定判定:

- `MANUAL_ADMIN_SCREEN_FIRST`

### 8.5 Codex（ChatGPT Business契約）

公開情報で分かっていること:

- CodexはChatGPT Business planで利用できる。
- ChatGPT Businessのstandard ChatGPT seatはCodex baseline accessを含む。
- ChatGPT BusinessのCodex seatはCodex-onlyで、usage-basedかつworkspace creditsが必要である。
- Business workspaceではseat typeや特定ユーザーごとのmonthly credit usage limitを設定できる。
- Codex seatは製品アクセスを変えるだけで、他メンバーがそのユーザーのprivate chat/Codex activityを自動的に読めるようにはならない。

スクリプトで検証すること:

1. Business workspaceのBilling/credits/spend controlsから、Codex seat数、Codex credits、user/seat別limitを確認する。
2. CSV export相当があれば匿名化CSVを読み取る。
3. CSV exportがない場合は、画面上のCodex credits/seat/limit/usageを手動転記CSVにする。
4. `service=codex-chatgpt-business` として共通CSVへ寄せる。

人手で残ること:

- standard ChatGPT seatだけでCodexを使っているのか、Codex seatを追加しているのか確認する。
- per-userのCodex usageまたはcredit消費が画面上で確認できるか確認する。
- Codex利用量をChatGPT Business本体と分けて集計できるか確認する。

暫定判定:

- `MANUAL_ADMIN_SCREEN_FIRST`

## 9. 将来課題

次のサービスは、対象組織で現在使っているサービスとして扱わないため、このRunbookの実験対象から外す。

| サービス | 将来課題に回す理由 |
| --- | --- |
| OpenAI API Platform | ChatGPT Business subscriptionとは別課金であり、現対象サービスに含めない。 |
| Anthropic Claude API / Claude Console | 対象組織の利用サービス一覧に含まれない。 |
| Claude Code | 対象組織の利用サービス一覧に含まれない。 |
| Google Gemini for Workspace | 対象組織の利用サービス一覧に含まれない。 |
| Gemini API / Google AI Studio | 対象組織の利用サービス一覧に含まれない。 |
| Cursor Team / Enterprise | 対象組織の利用サービス一覧に含まれない。 |
| JetBrains AI / AI Enterprise | 対象組織の利用サービス一覧に含まれない。 |
| Notion AI / Notion Credits | 対象組織の利用サービス一覧に含まれない。 |

## 10. 変換判定基準

| 判定 | 条件 | 次アクション |
| --- | --- | --- |
| `READY_FOR_API_PROBE` | 公開情報でAPI、権限、取得粒度が確認できた。 | APIキー/同意だけ人手で取得し、疎通スクリプトを作る。 |
| `MANUAL_DASHBOARD_FIRST` | 公開情報で管理ダッシュボードは確認できたが、API/CSV仕様は不明。 | 人手で画面とCSV export有無を確認し、変換用サンプルを作る。 |
| `MANUAL_ADMIN_SCREEN_FIRST` | 管理画面のBilling/usage/credits確認が主経路。 | 画面上の列やCSV export可否を確認し、必要なら手動転記CSVで検証する。 |
| `MANUAL_CSV_FIRST` | 公開情報でCSV exportは確認できたが、APIは未確認。 | 人手でCSVを出し、変換スクリプトを作る。 |
| `NEEDS_MAPPING_SPIKE` | API/CSVはあるが、ユーザー識別子、期間、event unitなどの解釈が曖昧。 | `docs/source-mapping/<service>.md` を作る。 |
| `BLOCKED_BY_PERMISSION` | 管理権限または契約が不足する。 | 必要権限、担当者、代替手段を記録する。 |
| `OUT_OF_SCOPE_DATA` | 本文、プロンプト、生成結果、コード断片を含む取得しかできない。 | MVP対象外として扱い、利用量だけ取れる方法を再調査する。 |

## 11. サービス別記録テンプレート

```markdown
## <service>

- 調査日:
- 調査者:
- 契約/プラン:
- 管理権限:
- 取得方法:
- 公式ドキュメントURL:
- 取得期間:
- 取得粒度:
- CSV/API/画面:
- 保存場所:
- 生データ削除期限:

### 公開情報で確認済み

| 項目 | 内容 | 出典 |
| --- | --- | --- |

### 人手で確認したこと

| 項目 | 結果 | メモ |
| --- | --- | --- |

### API/CSV/画面検証結果

| 元列 | 型 | 例のカテゴリ | 共通列 | メモ |
| --- | --- | --- | --- | --- |

### 共通スキーマ変換

| 共通列 | 設定値または変換式 | confidence |
| --- | --- | --- |

### 判定

- 判定:
- 理由:
- 次アクション:
- human_required:
```

## 12. 最初の実験順序

推奨順:

1. GitHub Copilot: APIと権限が明確で、ユーザー単位利用の検証に向く。
2. Microsoft 365 Copilot: Graph APIとCSV exportの両方があり、最終活動日を共通スキーマへ寄せやすい。
3. ユーザーローカル ChatAI: 実際の利用ダッシュボードを確認し、CSV export有無と部署/個人別粒度を早めに把握する。
4. ChatGPT Business: BusinessのBilling/usage/credits画面で取れる粒度を確認する。
5. Codex（ChatGPT Business契約）: ChatGPT Businessのseat/credits/spend controlsから、ChatGPT本体と分けて集計できるか確認する。

## 13. 完了条件

この実験フェーズは、次を満たした時点で完了とする。

- 対象5サービスごとに公開情報で分かるAPI、権限、CSV/export、取得粒度が記録されている。
- APIがあるGitHub CopilotとMicrosoft 365 Copilotでは、疎通スクリプトが作成され、実レスポンスの列名と型が確認されている。
- ユーザーローカル ChatAI、ChatGPT Business、Codexでは、管理画面またはCSV exportから取得できる列と粒度が確認されている。
- 少なくとも1サービスで、匿名化済み共通CSVを作成し、`ai_usage_snapshot.cs` で実行できている。
- 実データをrepoへ保存しない運用ルールが確認されている。
- 次の実装sliceとして、サービス固有CSV/API変換を実装する対象が1つ以上選定されている。

## 14. 参考公式情報

- GitHub Docs: https://docs.github.com/en/rest/copilot/copilot-usage-metrics
- Microsoft Learn: https://learn.microsoft.com/en-us/microsoft-365/admin/activity-reports/microsoft-365-copilot-usage
- Microsoft Learn: https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/api/admin-settings/reports/copilotreportroot-getmicrosoft365copilotusageuserdetail
- Microsoft Learn: https://learn.microsoft.com/en-us/graph/api/reportroot-getmicrosoft365copilotusageuserdetail
- ユーザーローカル ChatAI: https://chat-ai.userlocal.jp/
- OpenAI Help: https://help.openai.com/en/articles/8792828-what-is-chatgpt-business
- OpenAI Help: https://help.openai.com/en/articles/8792536-managing-billing-and-seats-in-chatgpt-business
- OpenAI Help: https://help.openai.com/en/articles/20001155-managing-credits-and-spend-controls-in-chatgpt-business
- OpenAI Help: https://help.openai.com/en/articles/8798634-managing-data-sharing-and-privacy-in-chatgpt-business
- OpenAI Help: https://help.openai.com/en/articles/11369540-using-codex-with-your-chatgpt-plan
