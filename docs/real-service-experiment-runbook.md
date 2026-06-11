# 実サービス利用状況取得 実験手順書

- 作成日: 2026-06-11
- 対象: `ai_usage_snapshot.cs` の実サービス入力準備
- 関連Plan: `plans/issue-1-ai-usage-snapshot-mvp/residual-decision-gate.md`
- 目的: 実サービスから取得できる利用状況データを確認し、共通CSVスキーマへ変換できるかを判断する。

## 1. 結論

MVP実装後の次フェーズでは、実サービスごとに次を確認する。

1. 管理画面からCSVまたはExcelを手動エクスポートできるか。
2. 管理者向けAPIで利用状況、ライセンス、最終利用日、ユーザー単位の集計を取得できるか。
3. APIキーや管理権限が必要な場合、最小権限・読み取り専用・短期間で実験できるか。
4. 取得結果を共通CSVスキーマへ変換できるか。
5. 本文系データ、プロンプト本文、生成結果本文、会話履歴を保存せずに済むか。

このフェーズの成果物は「実サービスデータを恒久保存すること」ではなく、「各サービスで取得可能な列、粒度、制約、変換方針を確定すること」である。

## 2. 実験の原則

| 原則 | 内容 |
| --- | --- |
| 最小権限 | 読み取り専用、レポート閲覧、Usage/Billing閲覧など、必要最小限の権限で実施する。 |
| 機密値非保存 | APIキー、アクセストークン、Cookie、セッション情報をrepo、チャット、Markdown、CSVへ保存しない。 |
| 本文非保存 | プロンプト本文、会話本文、生成物本文、ソースコード断片、添付ファイル本文を保存しない。 |
| 実データ非コミット | 実サービスCSV、未匿名化CSV、APIレスポンスの生データをrepoへコミットしない。 |
| 匿名化優先 | repoに残すのは列名、型、サンプル値のカテゴリ、匿名化済み最小サンプルに限定する。 |
| 失敗を記録 | 取得不可、権限不足、列不足、unknown化が必要な項目も成果として記録する。 |

## 3. 実験成果物

repoに保存してよい成果物:

- `docs/source-csv-notes.md`: サービス別の列名、粒度、取得経路、制約の記録。
- `samples/input/<service>-common-schema-sample.csv`: 匿名化済み、または人工化した共通スキーマCSV。
- `docs/source-mapping/<service>.md`: サービス固有列から共通スキーマへの対応表。
- `out/<service>-experiment-*`: ローカル検証出力。必要に応じてgit管理外にする。

repoに保存しない成果物:

- 実ユーザー名、実メールアドレス、実部署名を含むCSV。
- APIキー、アクセストークン、認証ヘッダー、Cookie。
- 会話履歴、プロンプト本文、生成結果本文。
- 未加工のAPIレスポンス全文。

推奨するローカル作業場所:

```text
D:\Data\local-ai-usage-snapshot-experiments\<yyyyMMdd>\<service>\
```

## 4. 共通CSVスキーマへの対応

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
| `service` | `github-copilot`, `m365-copilot`, `chatgpt-enterprise` など固定名を入れる。 |
| `license_status` | 座席割当または契約ユーザーなら `licensed`、未割当なら `unlicensed`、不明なら `unknown`。 |
| `active` | 期間内利用が確認できるなら `true`、ライセンスありで利用ゼロなら `false`、取れなければ `unknown`。 |
| `event_count` | リクエスト数、メッセージ数、アクティブ日数、利用イベント数など、サービスで取れる代表値を入れる。取れない場合は `unknown`。 |
| `event_unit` | `requests`, `messages`, `active_days`, `tokens`, `credits`, `events`, `seats` など。 |
| `last_activity_at` | ユーザー単位の最終利用日が取れる場合だけ入れる。 |
| `collection_method` | 手動CSVは `manual_csv`、画面転記や集計値だけなら `manual_aggregate`。 |
| `source_confidence` | ユーザー単位かつ期間が明確なら `high`、集計値や一部欠損ありなら `medium`、画面転記・推定なら `low`。 |
| `imported_at` | 実験で取り込んだ日付。 |
| `notes` | 制約だけを書く。本文やプロンプト内容は書かない。 |

## 5. 共通実施手順

1. 対象サービスの管理者またはレポート閲覧者を確認する。
2. 公式ドキュメントで、CSVエクスポート、管理者API、利用状況ダッシュボードの有無を確認する。
3. APIを使う場合は、読み取り専用または用途限定の資格情報を発行できるか確認する。
4. 取得前に、保存場所、匿名化要否、削除期限、共有範囲を決める。
5. 1サービスにつき、まず1期間、少数ユーザー、最小列で取得する。
6. 取得した列名、型、期間、粒度、権限、注意点を `docs/source-csv-notes.md` に記録する。
7. 共通CSVスキーマへ手動または一時変換で寄せる。
8. 次のコマンドで実行する。

```powershell
dotnet run --file ai_usage_snapshot.cs -- --input <common-schema.csv> --output <out-dir> --as-of <yyyy-MM-dd>
```

9. `data-quality-report.md` を確認し、`unknown`、`low_confidence`、`manual_aggregate`、`old_data` の出方を見る。
10. サービス別に「正式対応する」「変換スパイクが必要」「当面は手動集計のみ」のいずれかを判定する。

## 6. サービス別チェックリスト

### 6.1 GitHub Copilot

参照情報:

- GitHub Docs: REST API endpoints for Copilot usage metrics
- GitHub Docs: GitHub Copilot usage metrics

確認すること:

- OrganizationまたはEnterpriseのCopilot管理権限があるか。
- Copilot metrics APIを利用できるプラン・権限か。
- ユーザー単位の日次利用メトリクスを取得できるか。
- CSV/NDJSONダウンロードまたはAPIレスポンスから、ユーザー、日付、機能別利用、アクティブ状態を抽出できるか。
- メールアドレスまたはGitHub loginを共通の `user_key` として扱ってよいか。

実験手順:

1. 管理画面でCopilot usage metricsが有効か確認する。
2. APIを使う場合、必要スコープと管理権限を確認する。
3. 1日または7日分のユーザー単位レポートを取得する。
4. `service=github-copilot` として共通CSVへ変換する。
5. `event_count` は取得できる代表イベント数にし、単位を `event_unit` に明記する。

判定観点:

- ユーザー単位の利用有無が取れるなら `source_confidence=high`。
- 機能別内訳だけで横断合算が必要な場合は、変換仕様を別途作る。
- メールアドレスが取れない場合、GitHub loginと社内ユーザーIDの対応表が必要になる。

### 6.2 Microsoft 365 Copilot / Microsoft Copilot Chat

参照情報:

- Microsoft Learn: Microsoft 365 Copilot Usage Report
- Microsoft Learn: Microsoft 365 Copilot Chat Usage Report

確認すること:

- Microsoft 365 admin center のReports閲覧権限があるか。
- Microsoft 365 Copilot usage reportをCSVエクスポートできるか。
- Copilot Chat usage reportをCSVエクスポートできるか。
- ユーザー別の最終アクティビティ日、利用サービス別の活動、ライセンス割当を取得できるか。
- 表示名やメールアドレスの匿名化設定が有効かどうか。

実験手順:

1. Microsoft 365 admin centerで対象レポートを開く。
2. CSVエクスポート可能な表を確認する。
3. ユーザー単位の行、最終利用日、期間、アクティブ状態を確認する。
4. `service=m365-copilot` または `service=m365-copilot-chat` として共通CSVへ変換する。
5. ライセンス割当情報が別CSVの場合、結合可否を記録する。

判定観点:

- ユーザー名が匿名化されている場合、集計には使えるが横断ユーザー軸には追加対応が必要。
- アプリ別利用が取れる場合、`service` を細分化するか、`event_unit=events` で合算するかを決める。

### 6.3 ChatGPT Enterprise / Edu / Business

参照情報:

- OpenAI Help: Workspace analytics for ChatGPT Enterprise and Edu
- OpenAI Academy: ChatGPT Enterprise workspace analytics guide
- OpenAI Enterprise privacy

確認すること:

- Workspace owner、admin、analytics viewerのどれで閲覧できるか。
- Workspace Analyticsでユーザー単位またはグループ単位の利用状況を確認できるか。
- CSVエクスポートまたはAPI取得が利用可能か。
- Business/Team相当プランで同じ粒度が取れるか。
- 会話内容を含まず、利用メトリクスだけを取得できるか。

実験手順:

1. Workspace Analyticsの閲覧権限を確認する。
2. 期間、グループ、アクティブユーザー、利用量指標を確認する。
3. エクスポート機能がある場合は最小期間で取得する。
4. APIまたはCompliance系機能を使う場合は、利用可能な契約・権限・スコープを確認する。
5. `service=chatgpt-enterprise` または契約名に応じたサービス名で共通CSVへ変換する。

判定観点:

- グループ集計だけの場合は `collection_method=manual_aggregate` とし、ユーザー単位の候補抽出には使わない。
- 会話本文を含むエクスポートはこの実験対象外とする。
- API Platformの利用量とは別物として扱う。

### 6.4 OpenAI API Platform

確認すること:

- API Usage Dashboardから月次利用明細をエクスポートできるか。
- Project、API key、user、model、日付など、どの粒度で利用量を確認できるか。
- Admin APIまたはUsage/Costs系APIを使える契約・権限があるか。
- API keyと個人ユーザーの対応が取れるか。

実験手順:

1. Usage Dashboardでエクスポート可能な粒度を確認する。
2. API経由取得を使う場合は、読み取り専用相当の管理キーを発行できるか確認する。
3. 1日または1か月分の利用量を取得する。
4. ユーザー単位にできない場合は `user_key=unknown` またはProject単位の別スキーマ扱いにする。
5. `service=openai-api` として共通CSVへ変換する。

判定観点:

- API key単位の利用量は、個人利用状況ではなくシステム利用状況として扱う。
- ユーザー単位に結びつかない場合、ライセンス未利用候補には使わない。

### 6.5 Anthropic Claude API / Claude Console

参照情報:

- Anthropic Docs: Usage and Cost API
- Anthropic Docs: Pricing

確認すること:

- Usage & Cost Admin APIを利用できる組織権限があるか。
- Admin API keyの権限範囲と失効手順を確認できるか。
- Console画面からCSVまたは利用明細を取得できるか。
- API key、workspace、userなど、どの単位で利用量が取れるか。

実験手順:

1. ConsoleでUsage/Cost画面を確認する。
2. APIを使う場合はAdmin API keyの発行、保管、削除手順を事前に決める。
3. 最小期間の利用量を取得する。
4. 個人に紐づく場合のみ `user_key` へ設定し、紐づかない場合は `unknown` またはサービス集計のみとする。
5. `service=anthropic-claude-api` として共通CSVへ変換する。

判定観点:

- コスト・トークンは取れても、個人のアクティブ判定に使えない場合がある。
- Admin API keyの権限が広い場合は、本格運用前にキー管理方針が必要。

### 6.6 Google Gemini for Workspace

参照情報:

- Google Workspace: Gemini for Workspace log events
- Google Developers: Gemini in Workspace Apps Activity Events

確認すること:

- Admin consoleでGemini for Workspace log eventsを検索できるか。
- Reports APIで `applicationName=gemini_in_workspace_apps` のイベントを取得できるか。
- ユーザー、イベント種別、日時、Workspaceアプリ種別を取得できるか。
- ログ保持期間と利用可能なWorkspaceエディションを確認する。

実験手順:

1. Admin consoleでGemini関連ログの表示可否を確認する。
2. APIを使う場合、Reports APIの権限とサービスアカウント/管理者委任の要否を確認する。
3. 最小期間のイベント一覧を取得する。
4. ユーザー別、日別にイベント数を集計する。
5. `service=gemini-workspace` として共通CSVへ変換する。

判定観点:

- イベントログは「利用イベント」であり、課金額やクレジット消費とは別に扱う。
- イベント本文や対象ドキュメント本文は保存しない。

### 6.7 Gemini API / Google AI Studio

参照情報:

- Google AI for Developers: Gemini API Billing

確認すること:

- API key単位、Cloud project単位、請求アカウント単位のどれで利用量が取れるか。
- ユーザー単位に紐づけられる運用か。
- Billing export、Cloud Logging、利用ダッシュボードのどれを使うか。

実験手順:

1. Google AI StudioまたはGoogle Cloud側で利用量確認方法を確認する。
2. API keyやProject単位の利用量を取得する。
3. ユーザー単位にできない場合は、個人別レポートではなくサービス別集計として扱う。
4. `service=gemini-api` として共通CSVへ変換する。

判定観点:

- 個人利用ではなくアプリケーション利用として扱う可能性が高い。
- `event_unit=tokens` または `event_unit=requests` を明確にする。

### 6.8 Cursor Team / Enterprise

参照情報:

- Cursor Docs: Analytics

確認すること:

- Team/Enterprise管理画面でAnalyticsを閲覧できるか。
- CSVエクスポートまたは管理者APIが利用できるか。
- ユーザー別、日別、リクエスト別、コスト別のどの粒度で取れるか。
- 個人メールアドレスを保存してよいか。

実験手順:

1. CursorのTeam/Enterprise管理画面でAnalyticsを確認する。
2. Export機能またはAPIの有無を確認する。
3. 最小期間の利用量を取得する。
4. `service=cursor` として共通CSVへ変換する。
5. リクエスト明細にプロンプト本文が含まれる場合は保存対象から除外する。

判定観点:

- コード生成・チャット・エージェント利用など機能別に分かれる場合、`service` を分けるか `event_unit` に寄せる。
- 本文やコード断片が含まれるエクスポートはこのMVPに取り込まない。

### 6.9 JetBrains AI / AI Enterprise

参照情報:

- JetBrains Docs: AI plans and usage
- JetBrains Docs: Manage AI Enterprise

確認すること:

- JetBrains AccountまたはAI Enterprise管理画面で利用レポートを確認できるか。
- Top-up AI Credits usage reportなど、月次レポートの取得可否を確認する。
- ユーザー単位か、組織/クレジット単位かを確認する。
- 外部LLMプロバイダー連携時に、利用量がどこへ記録されるかを確認する。

実験手順:

1. JetBrains AccountまたはAI Enterprise管理画面を確認する。
2. 月次レポートまたは利用レポートの列を確認する。
3. ユーザー単位にできる場合は `user_key` へ対応させる。
4. `service=jetbrains-ai` として共通CSVへ変換する。

判定観点:

- 月次集計だけの場合は `collection_method=manual_aggregate` とする。
- 個人単位が取れない場合は候補抽出ではなく品質付きサービス別集計に限定する。

### 6.10 Notion AI / Notion Credits

参照情報:

- Notion: AI product information
- Notion Help: Custom Agents / credits usage guidance

確認すること:

- Workspace管理者がAI利用量またはNotion credits dashboardを閲覧できるか。
- ユーザー単位、Agent単位、Workspace単位のどの粒度で取れるか。
- CSVエクスポートまたはAPI取得が可能か。
- 本文やページ内容を含まずに利用量だけ確認できるか。

実験手順:

1. Workspace設定またはNotion credits dashboardで利用状況を確認する。
2. 取得できる列、期間、粒度を記録する。
3. ユーザー単位にできる場合は共通CSVへ変換する。
4. 集計値だけの場合は `collection_method=manual_aggregate` とする。
5. `service=notion-ai` として共通CSVへ変換する。

判定観点:

- credits消費はサービス横断比較では単位が異なるため、`event_unit=credits` と明記する。
- ページ本文やAI出力本文は保存しない。

## 7. 変換判定基準

| 判定 | 条件 | 次アクション |
| --- | --- | --- |
| `READY_FOR_COMMON_SCHEMA` | ユーザー単位、期間、利用有無、イベント数または最終利用日が取れる。 | 匿名化サンプルを作成し、共通CSVでCLI検証する。 |
| `NEEDS_MAPPING_SPIKE` | 列は取れるが、ユーザーID、日付、イベント単位、ライセンス情報の解釈が曖昧。 | `docs/source-mapping/<service>.md` を作る。 |
| `MANUAL_AGGREGATE_ONLY` | 部署別/サービス別など集計値しか取れない。 | 候補抽出には使わず、品質レポートに低信頼として載せる。 |
| `BLOCKED_BY_PERMISSION` | 管理権限または契約が不足する。 | 必要権限、担当者、代替手段を記録する。 |
| `OUT_OF_SCOPE_DATA` | 本文、プロンプト、生成結果、コード断片を含む取得しかできない。 | MVP対象外として扱い、利用量だけ取れる方法を再調査する。 |

## 8. サービス別記録テンプレート

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

### 取得できた列

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

## 9. 最初の実験順序

推奨順:

1. GitHub Copilot: 管理者API/レポートが比較的明確で、ユーザー単位利用の検証に向く。
2. Microsoft 365 Copilot / Copilot Chat: CSVエクスポートと最終活動日の確認に向く。
3. ChatGPT Enterprise / Edu / Business: Workspace Analyticsの粒度確認を優先する。
4. Gemini for Workspace: イベントログとして取得できるかを確認する。
5. API系サービス（OpenAI API、Anthropic Claude API、Gemini API）: 個人利用ではなくProject/API key単位になりやすいため、サービス別集計として扱えるかを確認する。
6. Cursor、JetBrains AI、Notion AI: 管理画面エクスポートまたは月次レポートの粒度確認を行う。

## 10. 完了条件

この実験フェーズは、次を満たした時点で完了とする。

- 対象サービスごとに取得可能な方法、権限、粒度、保存可否が記録されている。
- 少なくとも1サービスで、匿名化済み共通CSVを作成し、`ai_usage_snapshot.cs` で実行できている。
- 実データをrepoへ保存しない運用ルールが確認されている。
- 変換仕様が必要なサービスと、手動集計に留めるサービスが分類されている。
- 次の実装sliceとして、サービス固有CSV変換を実装する対象が1つ以上選定されている。

## 11. 参考公式情報

- GitHub Docs: https://docs.github.com/rest/copilot/copilot-usage-metrics
- GitHub Docs: https://docs.github.com/en/copilot/concepts/copilot-usage-metrics/copilot-metrics
- Microsoft Learn: https://learn.microsoft.com/en-us/microsoft-365/admin/activity-reports/microsoft-365-copilot-usage
- Microsoft Learn: https://learn.microsoft.com/en-us/microsoft-365/admin/activity-reports/microsoft-copilot-usage
- OpenAI Help: https://help.openai.com/en/articles/10875114-workspace-analytics-for-chatgpt-enterprise-and-edu
- OpenAI Academy: https://academy.openai.com/public/clubs/admins-6o6xf/resources/chatgpt-enterprise-user-analytics-guide
- Anthropic Docs: https://platform.claude.com/docs/en/manage-claude/usage-cost-api
- Anthropic Docs: https://platform.claude.com/docs/en/about-claude/pricing
- Google Workspace: https://knowledge.workspace.google.com/admin/reports/gemini-for-workspace-log-events
- Google Developers: https://developers.google.com/workspace/admin/reports/v1/appendix/activity/gemini-in-workspace-apps
- Google AI for Developers: https://ai.google.dev/gemini-api/docs/billing
- Cursor Docs: https://cursor.com/docs/account/teams/analytics
- JetBrains Docs: https://www.jetbrains.com/help/ai-assistant/licensing-and-subscriptions.html
- JetBrains Docs: https://www.jetbrains.com/help/ide-services/manage-aie.html
- Notion: https://www.notion.com/product/ai
