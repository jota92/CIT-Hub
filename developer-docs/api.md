# 拡張機能API v46

## TypeScriptの実行時処理

SDKのホスト要求は失敗理由ごとに `PermissionError`、`UserInteractionError`、`UnsupportedError`、`NetworkError`、`ConflictError`、`NotFoundError`、`RateLimitError`、`StorageError`、`ValidationError` を返します。未知のホストエラーは `ExtensionError`（`code === 'HOST_ERROR'`）として扱います。UIやデータ更新では、競合を自動で上書きせず最新データを読み直してください。

`defineProgram()` の `render()` はアプリ内の隔離されたQuickJSで動きます。`interactive.button()` のコールバック内で状態更新、計算、`Promise`、`async/await` を使用できます。生成した画面は既存のネイティブコンポーネントで表示します。DOM・Node.js・直接のネットワーク・ネイティブコードにはアクセスできません。1処理250ms、メモリ16MB、未完了のホスト要求32件を上限にします。

`defineExtension()` は従来どおり静的な画面定義です。defineProgramの直接エクスポートは、ソースを実行せずリテラルのメタデータを解析してビルドします。従来のdefineExtension方式は作者のPC上でソースを実行するため、信頼できないソースをビルドしないでください。サーバーは提出コードを実行しません。

使用例は `examples/interactive-counter.ts`、`persistent-counter.ts`、`runtime-form.ts`、`runtime-utilities.ts`、`interactive-select.ts`、`interactive-multi-select.ts` です。状態変更で再描画し、`interactive.textField()`、`interactive.textArea()`、`interactive.toggle()`、`interactive.select()`、`interactive.multiSelect()` は入力値をStateに反映します。入力用イベントIDはStateとキーに結び付け、再描画中の連続入力を失わないようにしています。

### 接続済みホストAPI

- `secureStorage.get/set/delete`: 本人・拡張機能・開発/公開環境に分離した保護保存です。iOS/Macは専用Keychainサービス、Androidは専用EncryptedSharedPreferencesを使います。CIT Hub本体のKeychainや認証情報は参照できません。JSON値は1キー32KBまでです。読めない場合はエラーを返し、既存データを置き換えません。
- `storage.get/set/has/remove/clear/keys`: JSON値を端末へ保存します。同じアカウント・拡張機能・開発/公開環境ごとに分離し、更新版でも継続します。他端末へ同期しません。合計256KB、キーは英字開始の英数字・ピリオド・ハイフン・アンダースコア100文字以内です。保存に失敗した場合はPromiseを失敗させます。
- `cit.courses.list()`: `timetable.read` が必要です。取得済み授業データを返します。
- `cit.assignments.list()`: `assignments.read` が必要です。元データにない提出状況は生成しません。
- `cit.todo.list()`: `todos.read` が必要です。本人のToDoを返します。
- `cit.settings.appearance/notifications/enabledServices()`、`cit.services.list()` と `cit.user.preferences()`: `settings.read` が必要です。`cit.services.list()` は本人が有効にしているタブ名を `{title, enabled:true}` の配列で返します。テーマ色、通知の有効状態、自動ログイン・表示モードも返します。氏名・学籍番号・メール・秘密情報は返しません。任意のサービスを自動で開くAPIはありません。
- host API v31でiOS・macOS・Androidに `cit.services.list()` を追加しました。`settings.read` を宣言した拡張機能に限り、端末の設定スナップショットにある有効なタブ名を `{title, enabled:true}` の配列で返します。Webページ・セッション情報は返さず、サービスへの自動遷移もしません。SDKビルダーが `minimumHostAPI: 31` を設定するため、v30以前のホストは起動前に非互換として拒否します。
- host API v32で `interactive.select(key, label, options, state)` を追加しました。ネイティブ選択UIの変更は文字列値としてTypeScriptのStateへ反映され、値の更新に応じて画面を再描画します。選択肢は1〜100件、各200文字以内で重複不可です。ビルダーはこのAPIを使うパッケージに `minimumHostAPI: 32` を設定します。
- host API v33で `network.uploadFile()` / `network.downloadFile()` を追加しました。両APIとも利用者のボタン操作中のみ、宣言済みHTTPSホストと `network.fetch`・`files.storage` の両権限で利用できます。ファイルは拡張機能専用ストレージのみを扱い、各転送は256 KiBまでです。
- host API v34で `interactive.multiSelect(key, label, options, state)` を追加しました。選択変更は文字列配列としてTypeScriptのStateへ反映されます。選択肢は1〜30件、各120文字以内で重複不可です。
- host API v35で `interactive.dateTime(key, label, state.create<number | null>(null))` を追加しました。OS標準の日付・時刻選択UIの値はUnix epochミリ秒のnumberとしてStateへ反映され、未設定はnullです。SDKが `minimumHostAPI: 35` を設定します。例は `extensions/examples/interactive-date-time.ts` です。
- host API v42で `ui.barChart(title, categories, series)` を追加しました。1〜12カテゴリ、1〜4系列、各値0〜1,000,000の検証済みデータをiOS・macOS・Androidのネイティブ横棒表示で描画します。自由なCanvas描画やスクリプト描画は行いません。各系列の値数はカテゴリ数と一致させ、SDKビルダーは `minimumHostAPI: 42` を設定します。
- `cit.bus.*`: `bus.read` が必要です。端末に保存された公開バス時刻表のみを読みます。時刻表データを取得するには、アプリで一度バスダイヤを表示して取得してください。
- `app.version/extensionId/environment/hostVersion/capabilities`: 同期的に実行環境を確認します。
- `capabilities.has(name)`: 同期的に対応を確認します。未実装APIはfalseです。OS権限の許可とは別です。
- `permissions.openSettings()`: `settings.open`を宣言した拡張機能が、画面上のボタン操作中に呼ぶとCIT HubのOSアプリ設定を開きます。拒否済み権限を利用者自身が設定から変更するための導線です。拡張機能が設定値を書き換えるAPIではありません。

授業データなどが未取得の場合はnullになることがあります。秘密情報・他ユーザー・他拡張機能のデータは要求できません。単一要求10秒、要求内容60KBまでです。

`app.onLaunch/onAppear/onDisappear/onResume/onSuspend/onOpenURL/onNotification/onMemoryWarning` はコールバック登録と購読解除に対応しています。URLイベントは拡張機能画面が表示されている間に限って届けます。ローカル通知タップは対象の追加済み拡張機能を開き、ランタイム準備完了後に通知イベントを一度届けます。payloadは通知ID・操作・dataなど許可済み文字列だけに制限します。メモリ警告もOSが通知した場合だけです。終了時・中断時の非同期処理完了やバックグラウンド実行は保証しません。タイマーは同時32件、10ms以上、最長24時間です。OSによる遅延や停止があります。`utils.sleep/debounce/throttle` は実行時にも使用できます。日時の書式化は隔離された文字列専用ブリッジを通じてホストのIntlを利用します。

`text.regex(pattern, flags)` は最大256文字のパターンと重複のない正規表現フラグを検証して `RegExp` を返します。パターン実行はQuickJSの処理時間制限内に限られ、制限超過時は拡張機能の実行が中断されます。例: `text.regex('^(CIT Hub)\\s+(\\d+\\.\\d+)$', 'i').exec(value)`。

`utils.uuid()` は暗号学的に安全な乱数からUUID v4を生成します。安全な乱数を利用できない実行環境では、予測可能な値にフォールバックせずエラーにします。

`settings.open`はAPI v14です。OS設定を勝手に開かないよう、利用宣言に加えて画面上の直接操作が必要です。アプリ設定から先のページやスイッチはOSごとに異なり、拡張機能から指定・変更できません。

## ローカル通知（API v15）

`notifications.schedule`権限を宣言した拡張機能は、`notification.permission()`で現在の許可状態を調べ、ユーザーが押した操作から`permissions.request('notifications.schedule')`を呼び出してOSの通知許可を要求できます。`notification.schedule({id,title,body,at,repeat,data})`、`cancel(id)`、`cancelAll()`、`listScheduled()`を使用できます。通知データは端末内に保存され、拡張機能・アカウント・開発/公開環境ごとに分離されます。拡張機能ごとに20件、アプリ全体で最大62件です。

通知を押すと拡張機能タブへ移動し、同じアカウントにその拡張機能が追加済みなら対象ランタイムを開いて`notification.onOpen`と`app.onNotification`でイベントを届けます。アプリ起動中・終了中のどちらでも、ランタイムの準備ができるまでイベントを端末内に保留します。アカウント・拡張機能・環境のスコープが一致しないイベントは渡しません。対象が未追加または別アカウントの通知なら、勝手にインストールせず案内を表示します。iOS/macOSはUserNotifications、AndroidはAlarmManagerの不正確な予約を使うため、表示時刻はOSの省電力制御・通知設定により遅れる場合があります。アプリが強制停止された場合などの表示も保証されません。通知はサーバーへ送信されず、端末間同期も行いません。

実行エンジンの停止を検知する監視を設けています。AndroidのWebViewが未対応の場合は更新を案内します。

本SDKは添付要件の全APIを実装済みではありません。端末側の接続済みAPIと制約は本書の各節、未実装範囲は「APIの追加履歴と未実装範囲」を参照してください。未対応APIを利用可能と装わず、ホストCapabilityにも追加しません。

## データAPI（端末内）

TypeScript SDKの `campus` は読み取り要求を生成し、`ui.data()` に配置します。アプリが表示時に取得済みデータを読み取り、標準のUIで描画します。30秒ごとに表示を再評価します。サーバーへ時間割・課題の情報を送信しません。

| 呼び出し | 要求 | 権限 | 内容 |
| --- | --- | --- | --- |
| `cit.bus.routes()` / `stops()` | bus.schedule | bus.read | 公開時刻表の路線・停留所 |
| `cit.bus.timetable(date?, route?)` | bus.schedule | bus.read | 指定日の出発便 |
| `cit.bus.next(from?, to?, at?)` | bus.schedule | bus.read | 指定時刻以降の次便（最大7日先まで） |
| `cit.bus.between(start, end, route?)` | bus.schedule | bus.read | 指定期間の便（最大366日） |
| `cit.bus.serviceStatus(date?)` | bus.schedule | bus.read | 掲載時刻表の有効期間・曜日区分・路線数 |
| `cit.cafeteria.locations()` | cafeteria.locations | cafeteria.read | 食堂の場所とキャンパス・階 |
| `cit.cafeteria.menu()` | cafeteria.menu | cafeteria.read | 各食堂の公式メニューPDFリンク |
| `cit.cafeteria.cameraStatus()` | cafeteria.cameraStatus | cafeteria.read | 公開カメラの稼働曜日・時間・現在状態・画像URL |
| `campus.currentClass()` | courses.current | timetable.read | 現在の授業・教室 |
| `campus.nextClass()` | courses.next | timetable.read | 次の週間授業枠 |
| `campus.today()` | courses.today | timetable.read | 今日の授業 |
| `campus.week()` | courses.week | timetable.read | 選択学期の週間授業 |
| `campus.enrollment()` | enrollment.summary | timetable.read | 授業名の重複を除いた科目数・週の授業枠数 |
| `campus.pendingAssignments()` | assignments.pending | assignments.read | 期限前・期限なしの取得済み課題 |
| `campus.workload()` | assignments.summary | assignments.read | 課題件数・期限超過件数 |
| `campus.overdue()` | assignments.overdue | assignments.read | 期限超過の取得済み課題 |
| `campus.dueSoon()` | assignments.dueSoon | assignments.read | 現在から7日以内に期限が来る課題 |
| `campus.todos()` | todos.summary | todos.read | 自分で追加したToDoの件数 |

```typescript
import { defineExtension, campus, ui } from '../sdk/index';
export default defineExtension({
  name: '授業の確認', version: '1.0.0', description: '次の授業と課題を確認します。',
  permissions: ['timetable.read', 'assignments.read'],
  content: [ui.data(campus.nextClass(), '次の授業'), ui.data(campus.workload(), '課題')]
});
```

サンプルは `extensions/examples/study-status.ts`。静的なcampus/ui.data方式では取得結果をホストが描画します。v9のdefineProgramではcit APIから本人のJSONデータを取得し、TypeScriptで処理できます。宣言したホストへのHTTPS通信は`network.fetch`で利用できます。バックエンドコード実行はできません。

バス便は `cit.bus.routes/stops/timetable/next/between/serviceStatus` から参照できます。日付と便時刻は `Asia/Tokyo` として処理します。曜日区分は公開JSONの平日・土曜・日祝区分に従い、祝日カレンダーや運休・遅延などのリアルタイム情報は含みません。`serviceStatus` は公開時刻表の掲載期間内かを示すもので、実際の運行を保証しません。公開JSONがアプリにまだキャッシュされていない場合は `data_unavailable` になります。キャッシュデータは端末内にとどまり、拡張機能APIサーバーへ送信されません。これらを使う拡張機能はホストAPI v19以降を要求します。

`cafeteria.read` 権限を宣言すると、`cit.cafeteria.locations()` と `menu()` で新習志野1F・2Fおよび津田沼の場所と公式PDFリンクを取得できます。`cameraStatus()` は公開カメラの画像URLと、稼働曜日・11:00〜14:00の時間帯に基づく現在状態を返します。返すのは公開画像のURLと稼働メタデータであり、アプリ内カメラを起動したり、映像を中継したりはしません。カメラ状態の曜日・時刻は `Asia/Tokyo` です。これらを利用する拡張機能はホストAPI v20以降が必要です。

## 判断できること・できないこと

- 日本時間（Asia/Tokyo）で判定します。時刻情報がない授業は授業中・次の授業を断定しません。
- 休講・祝日・臨時変更は元データにない場合判断できません。次の授業は週間時間割に基づきます。
- 履修数は授業名による推定です。単位数・正式な履修登録状況ではありません。
- 課題の提出済み・未提出を区別する情報が取得元にない場合、期限のみで分類します。
- 未取得の時間割は空き日と区別して表示します。課題は「取得済み一覧」に限った情報です。
- 教室・授業名・期限のみを扱い、教師情報、認証情報、Cookie、OTP、サポート会話、添付ファイルは渡しません。

## 権限と公開

データ要求には対応する権限をmanifestへ宣言する必要があります。未宣言・未知の要求はアップロード時に拒否します。利用者は追加・更新時に権限の説明を確認して許可します。開発プレビューは本人だけに限定されます。カメラなどOS権限は実際の機能操作時にOSの確認を表示し、拒否済みなら端末設定を案内します。写真やファイルはOSの選択画面で利用者が選んだ項目だけを扱います。古いホストは互換性のない版のインストール・起動を拒否します。

公開されるのは運営が承認した固定版だけです。保存は利用者・拡張機能・開発版/公開版ごとに分離し、更新競合はrevisionで拒否します。サーバーはアップロードされたソースを実行しません。

### 外部HTTPS通信

外部API通信にはmanifestに`permissions: ['network.fetch']`と`network: ['api.example.net']`の両方を指定します。ホスト名は小文字の完全一致で最大20件です。ワイルドカード、IPアドレス、localhost、ローカル用ドメインは使用できません。利用者には追加前に通信先ホストを表示します。

```ts
import { defineExtension, network } from '@cithub/extensions-sdk';

const result = await network.get('https://api.example.net/v1/status', { Accept: 'application/json' });
if (!result.ok) throw new Error(`HTTP ${result.status}`);
const status = JSON.parse(result.body);
```

`network.fetch/get/post/put/patch/delete`を提供します。GET/POST/PUT/PATCH/DELETEとテキスト本文、最大20個のヘッダーに対応します。Cookie・Host・接続制御系ヘッダーは禁止し、HTTPS標準ポートのみ、リダイレクトは追従せず、許可済みホストのDNSがプライベート/予約済みIPを返す場合も拒否します。リクエスト本文は32 KiB、応答本文は256 KiB、通信待ち時間は最大20秒です。レスポンスはstatus/ok/許可した応答ヘッダー/body(UTF-8文字列)です。

host API v33のファイル転送は次のとおりです。

- `network.downloadFile(url, destinationName, headers?)` は宣言済みHTTPSホストから最大256 KiBを取得し、拡張機能専用ストレージへ保存します。戻り値はHTTP状態と `{name,size}` です。内容は `files.read(name)` で読み取れます。
- `network.uploadFile(url, sourceName, {method?,headers?})` は専用ストレージ内の最大256 KiBのファイルをPOSTまたはPUTします。端末の任意パスやユーザーの未選択ファイルは読みません。
- APIはユーザーが明示的に押したボタン処理からのみ実行できます。両APIには `network.fetch` と `files.storage` が必要で、許可ホストはmanifestの完全一致宣言に限られます。転送先には端末のIPが伝わり、アップロードしたファイル内容も渡るため、公開前審査の対象です。

## Mac miniサーバーAPI

`POST /api` にJSONを送信します。開発者サイトはBearerセッション、アプリは既存のアカウント管理で確認される端末情報を使用します。開発者から学番を指定して別ユーザーの情報を読めるAPIはありません。

- `register`: アプリからの本人端末確認、username・passwordによる登録。
- `login` / `logout`: 開発者ログイン・現在のセッション失効。
- `logout-all`: 自分の全開発者セッションを失効。
- `password-change`: current_password・new_passwordで変更。旧セッションを全失効。
- `capabilities`: 認証不要の固定メタデータ。対応ホストAPIバージョン・権限・読み取り要求だけを返し、個人データは含みません。
- `profile-save` / `mine`: 公開プロフィール・自分の開発版。
- `upload` / `submit`: 開発版保存・審査申請。
- `catalog` / `developer`: 承認済み版・公開作者ページ。
- `install` / `open` / `uninstall`: 追加・起動・削除。v40ホストはhost_api_version=40を送信し、使用APIに応じた最低互換性を確認します。
- `data-save`: 拡張機能専用の入力値をrevision付きで保存。
- `list` / `review`: 別の管理キーでのみ審査・公開停止。

セッションは24時間、1ユーザー最大8個。ログイン関連は15分間8回/ユーザー・40回/接続元。一般APIは15分間120回/接続元。混雑時は503、制限時は429を返し、クライアントは連打や無限リトライをしないでください。

## API v3: 標準UIによる対話画面

`ui.heading(text)`、`ui.divider()`、`ui.select(key,label,options)`、`ui.when(key,equals,children)`、`ui.link(label,httpsURL)`を利用できます。選択状態をプログラムから扱う場合は `interactive.select(key,label,options,state.create(''))` を使います。ネイティブ選択の変更値をStateへ反映し、保存権限を付けると入力値と一緒に保存されます。v32未満のホストでは動作しないため、ビルダーが `minimumHostAPI: 32` を設定します。リンクは外部ブラウザで開きます。URLに認証情報を含めることやHTTPS以外は許可しません。静的な `ui.select` は引き続き旧APIにも対応します。

複数の値を選択する場合はhost API v34の `interactive.multiSelect(key,label,options,state.create<string[]>([]))` を使います。iOS・iPadOS・macOSとAndroidでネイティブの複数選択UIを表示し、選択変更を宣言順に整えた文字列配列としてStateへ返します。選択肢は1〜30件、各120文字以内で重複不可です。SDKビルダーが `minimumHostAPI: 34` を付与し、旧ホストへの公開/起動をサーバーが拒否します。例は `extensions/examples/interactive-multi-select.ts` です。

日時を選ぶ場合はhost API v35の `interactive.dateTime(key,label,state.create<number | null>(null))` を使用します。ネイティブ日付・時刻UIの選択値はUnix epochミリ秒、未設定は `null` です。端末をまたいだ永続化には拡張側で `data-save` / `data-load` を呼びます。例は `extensions/examples/interactive-date-time.ts` です。

開始・終了の両方を扱う場合はhost API v36の `interactive.dateRange(key,label,state.create({start:null,end:null}))` を使います。iOS・iPadOS・macOS・Androidのネイティブ日時選択UIを使い、各値はUnix epochミリ秒または未設定の `null` です。開始日時が終了日時より後になる値はSDKとホストで拒否し、OSのUI上では範囲が逆転しないよう補正します。例は `extensions/examples/interactive-date-range.ts` です。


これは宣言型画面APIであり、本体と同等の任意処理を実行する基盤ではありません。実行型拡張機能では本人データの取得、ToDo操作、外部HTTPS通信、端末内ファイル、選択ファイル読み取り、クリップボード操作、ローカル通知予約に対応しています。バックグラウンドでの任意コード実行、任意画面遷移などは未対応です。認証情報・Cookie・OTPは公開APIに含めません。

## API v22: テキストUI部品

`ui.label(text)`、`ui.markdown(text)`、`ui.code(text)`、`ui.caption(text)`、`ui.badge(text)`、`ui.icon(name, accessibilityLabel)`を使用できます。アイコン名はSDKの安全な固定リストから選び、Markdown内のリンクは実行しません。これらを使う拡張機能はhost API v22以降を要求します。

## API v23: 複数行入力

`ui.textArea(key, label)` はネイティブの複数行テキスト入力を表示します。実行型拡張機能では `interactive.textArea(key, label, state.create(''))` を使うと入力のたびにStateが更新され、画面に反映されます。入力は4,000文字までで、iOS/Macでは縦方向に伸びる入力欄、Androidでは高さを持つ複数行入力として表示されます。審査・配布サーバーはこの部品を使う拡張機能にhost API v23を要求します。

`permissions.request(permission)`はユーザー操作に応じて呼び出してください。ユーザー操作なしの起動時要求は拒否されます。カメラと通知はOSの許可ダイアログを出し、写真・ファイルはOSの選択UIでユーザーが選んだ対象だけを返します。

## スタイル指定

`ui.styled(node, { tone: 'accent', textSize: 'title', align: 'center', padding: 16, spacing: 12 })`で標準部品を調整できます。

- 色: `primary` / `secondary` / `accent`。ホストのテーマ・ダークモードに追従。
- 文字: `caption` / `body` / `title`。文字サイズのアクセシビリティ設定に追従。
- 配置: `leading` / `center` / `trailing`。
- 余白・部品間隔: 0〜32。間隔は対応するコンテナに適用。
- 横並びは狭い画面で縦並びに切り替わります。

任意CSS、絶対座標、文字と背景の固定色、アプリのナビゲーションの置換は許可しません。

## APIの追加履歴と未実装範囲

host API v30では拡張機能専用の端末内ファイル `copy` / `move`、v31では `settings.read` を使う本人の有効な学内サービス一覧 `cit.services.list()`、v32ではStateへ変更を返す `interactive.select()`、v33ではユーザー操作を必要とする隔離ストレージとのHTTPSファイル転送を追加しました。SDK ZIPに含まれるビルダーは必要な最低ホストバージョンを自動設定します。v9以降の追加履歴は各API節を参照してください。

添付されたAPI構想のすべてが実装済みではありません。特に、アップロード/ダウンロード進捗、WebSocket、OSバックグラウンドタスク、サーバーFunctions/KV/DB/Cron、ユーザー間共有データ・グループ・Realtime・Push、OAuth/OIDC、拡張機能間Intents、OSウィジェット/App Intents、地図ネイティブ表示、画像変換、音声録音、モーションセンサー、汎用ドラッグ&ドロップ、任意Canvas/グラフ、PDFページ操作は未実装または一部未接続です。権限・データ範囲・対応OS・失敗時の意味が確定しホスト実装とテストが揃うまでCapabilityとして公開しません。認証情報、OTP、Cookie、他ユーザーの情報、任意ネイティブコード、無制限バックグラウンド実行は提供しません。

## 構造化時間割（API v16）

`timetable.read` 権限で `cit.courses.list/get/search/byWeekday/forPeriod` と `cit.timetable.today/tomorrow/week/current/next/at/freePeriods` を利用できます。授業には端末間で一致する安定ID、選択中学期、曜日、時限、教室、開始・終了分、取得できる場合は担当教員・シラバス情報を含みます。`cit.timetable` の日付別APIは日本時間の実日付と開始・終了ISO日時を返し、`week()` は月曜から日曜までです。授業時間が不明なら開始・終了をnullにします。空き時限は時間割に存在する時限だけを対象にし、時間割未取得と「空き」を混同しません。大学の休講・臨時変更データは取得元から提供されていないため、曜日の定例時間割を返し、休講日判定はしません。時間割データはネイティブホスト内で処理され、拡張機能APIサーバーへ送信しません。

## 構造化課題（API v16）

`assignments.read` 権限で `cit.assignments.list/get/pending/overdue/dueSoon/search/byCourse/between` を利用できます。課題には取得できたID・種類・タイトル・授業名・提出開始・期限・リンクを含めます。ID指定は一致する課題がなければ `null` を返し、期間検索の端点は両方とも含みます。提出済み状態はmanaba等の取得元から確実に判定できないため追加・推測しません。データは端末上の取得済み一覧を使い、APIサーバーへ送信しません。

## ToDo完了操作（API v17）

`cit.todo.complete(id)` と `cit.todo.uncomplete(id)` は `todos.write` が必要です。変更前に本人のToDoを再取得し、全フィールドの一致を確認してから更新します。競合時は `todo_conflict` となり上書きしません。完了状態は端末内の本人ToDo保存データに記録されます。現状、端末間同期は保証しません。これらの操作を使う拡張機能はホストAPI v17以降を要求します。
アプリ本体のToDo一覧にも完了チェックを表示し、完了項目は取り消し線で区別します。

## 学年暦（API v18）

`calendar.read` 権限を宣言すると、`cit.academicCalendar.list()` で端末に保存されている学年暦を読み取れます。`cit.academicCalendar.on(date)` は指定日の予定、`between(start, end)` は指定期間と重なる予定を返します。イベントは `title`、`start`、`end`、`kind` を持ち、日付は日本時間の `YYYY-MM-DD` です。期間検索は開始日・終了日の重なりを含みます。

このデータはアプリが取得・キャッシュしている公開学年暦から端末内で提供し、開発者サーバーへは送信しません。学年暦キャッシュがない場合は本体の組み込み既定データを使います。最新情報の取得タイミングはアプリ本体に従います。学内の個人情報やポータル認証情報は含みません。`calendar.read` を使う拡張機能はホストAPI v18以降が必要です。

`cit.academicCalendar.isClassDay(date)` は休講・休暇イベントなら `false`、学年暦に「授業日」と明記された日または本人の取得済み時間割にその曜日の授業がある日なら `true`、根拠データが足りなければ `null` を返します。これは大学公式の祝日判定ではありません。`cit.semester.current()` と `cit.semester.list()` は `timetable.read` を必要とし、現在ホストが渡せる時間割スナップショットに含まれる学期名を返します。現在のスナップショットは選択中の学期に限られるため、全学期の一覧を保証しません。授業日判定は `calendar.read` と `timetable.read` の両方が必要です。

サンプル: `extensions/examples/runtime-academic-calendar.ts`、`extensions/examples/runtime-semester-days.ts`。

## API v4: ToDoとユーザーが選んだファイル

- `ui.date(key,label)`: OS標準の日時選択。設定をオフにすると日時は未設定になります。
- `actions.createTodo(label,titleField,detailField,deadlineField?,notificationField?)`: 確認画面の承認後、本体のToDo保存処理に渡します。タイトルは必須、通知は現在より後かつ期限以前です。本体の通知権限・通知処理に従います。
- `ui.file(key,label)`: 利用者が選んだ画像・PDF・テキスト等を10MB以内で取り込み、端末内に保存・共有できます。端末の任意のフォルダにはアクセスできません。ファイル本体はサーバーに送信しません。
- 権限はそれぞれ `todos.write` と `files.user`。各画面で利用許可が必要です。
- 選択ファイルの一時領域は選択UIの専用機能です。永続領域とは別で、ファイル本体をサーバーへ送りません。
- API v4非対応アプリでは、追加・起動を止めてアプリ更新を案内します。

## API v12: 選択したファイルの内容

`interactive.file(key,label,state)` は `files.user` で選択したファイル名を状態へ渡します。内容の読み取りには別途 `files.selection.read` 権限が必要です。`files.selectedInfo(key)` は名前・サイズ・MIMEタイプを返し、`files.readSelected(key,offset,maxBytes)` は最大48 KiBずつ読み取ります。対象は同じ実行画面で選択した最大10 MiBのファイルだけです。未選択なら `null`、範囲外の位置はエラーです。

内容は端末内で読み取り、SDKや管理サーバーへ自動送信しません。任意の端末パスや他拡張機能の領域にはアクセスできません。拡張機能が別途外部通信APIを使う場合は、そのコード次第で内容が送信され得ます。

サンプル: `extensions/examples/selected-file-reader.ts`。

## API v13: クリップボード

`clipboard.readText()` は `clipboard.read`、`clipboard.writeText(text)` は `clipboard.write` の権限が必要です。両APIとも拡張機能画面上の利用者操作（ボタンなど）から同期的に呼び出した場合だけ動作します。操作後に別の非同期処理を待ってから呼ぶ場合は拒否されます。読み書きは文字列のみ、最大16 KiBです。読取ではiOSのシステム通知が表示される場合があり、Android OS側もコピー通知を表示します。

クリップボードは共有端末領域です。権限を持つ拡張機能が他アプリからコピーされた文字を読み取る可能性を、公開審査時に説明・確認してください。読み取りは自動送信されず、拡張機能が別途許可済みの外部通信APIを使った場合のみ、その実装に従い送信されます。未対応OSでは機能を公開せず、capabilitiesに含めません。

サンプル: `extensions/examples/clipboard.ts`。

サンプル: `extensions/examples/todo-and-files.ts`。ビルド済みJSONは `extensions/dist/todo-and-files.json`。確認用で、公開・登録は行っていません。

## API v5: メディア・ブラウザ・本人データ

- `ui.file(key,label)`から動画・音声・各種文書も選択できます（10MB以内）。iOS/iPad/MacはQuick Look、Androidは画像・PDF・動画・音声・テキストの内蔵ビューを利用します。未知の形式は対応アプリへ渡します。全コーデック・全ファイル形式の再生を保証するものではありません。
- `ui.image(label,httpsURL)` / `ui.video(label,httpsURL)` / `ui.audio(label,httpsURL)`: `media.remote`許可後、指定先から直接表示・再生します。接続先にIP等の通信情報は伝わりますが、時間割・設定・認証情報は渡しません。自動再生しません。
- `ui.browser(label,httpsURL)`: `browser.open`許可後、Safari View Controller / Chrome Custom Tabsで表示します。拡張機能へDOM・Cookie・認証結果は返しません。本体の学内サービスWebViewとは別です。
- `ui.link(label,httpsURL)`: タップで外部ブラウザを開きます。
- `campus.courseData()` / `campus.assignmentData()` / `campus.todoData()`: ログイン中の本人の取得済み時間割・課題・ToDoをJSON文字列として表示します。課題データがない場合は空の配列です。取得済み課題には提出状態を保証する情報がありません。
- `campus.displaySettings()`: 色、表示中の学内サービスタブ名、授業/課題通知のオンオフ、自動ログイン・スマホ表示のオンオフ。`settings.read`権限が必要です。

任意の設定ストア全件やKeychain/EncryptedSharedPreferencesは公開しません。パスワード、OTP秘密鍵・履歴、Cookie、セッション、端末トークン、API管理キー、管理サーバーのユーザー情報は読めません。データAPIは端末内読み取り専用で、管理サーバーへ問い合わせません。Webページとメディアに端末データを渡すブリッジもありません。

ユーザー設定APIが返すのはCIT Hub画面の表示色、通知オン/オフ、学内サービスの表示設定、自動ログイン設定など明示した項目に限ります。CIT Hubに個別の言語設定がないため、端末言語をアプリ設定として扱いません。

JSONは宣言型ホストへの読み取り要求です。v9のdefineProgramでは隔離されたTypeScript実行環境で本人のJSONデータを処理できます。DOM自動操作とバックグラウンドコード実行は未対応です。

サンプル: `extensions/examples/media-and-data.ts` / `extensions/dist/media-and-data.json`。

## v6: 既存画面へのショートカット（非推奨）

`ui.boardCamera(label)` と `board.camera` 権限で本人の授業別写真画面を開けます。
撮影・写真取り込み・一覧・拡大・共有・端末保存・削除はホストの標準画面で行います。
撮影対応はiOS/iPadOS/Androidです。Macではホストが提供する閲覧機能のみです。
利用者の許可とOSの撮影権限が必要です。自動撮影や自動削除は行いません。
秘密鍵、認証情報、他ユーザーの写真にはアクセスできません。写真本体は拡張機能サーバーへ送信しません。
API v6未対応のアプリは追加・起動時に更新案内となります。

これは写真の生データやカメラ制御をTypeScriptへ渡すAPIではなく、ネイティブ画面を開く宣言APIです。
サンプルは `extensions/examples/board-camera.ts` です。一般の開発者と同じビルド・アップロード手順を使います。

## APIロードマップ

この節は未実装・未接続の構想であり、現在のSDKの提供機能ではありません。現在のファイル、写真、通知、ToDo、授業メモ、位置情報などの制約は各API節を参照してください。大規模な未実装群は「APIの追加履歴と未実装範囲」に列挙しています。追加時には権限、保存範囲、OS差、取り消し操作、サーバー側制限を定義し、ホスト・SDK・サーバー・テストを同時に整備します。

## v6: テキスト共有・コピー
`ui.shareText(label, text)` は標準の共有画面を開き、送信先は利用者が選びます。
`ui.copyText(label, text)` はボタン操作で指定テキストをクリップボードへ書き込みます。
最大4000文字。読み取り・自動送信はありません。`shareText` / `copyText` の本文は固定値です。入力欄の値を共有・コピーしたい場合は、実行型APIの入力イベントと利用者操作を組み合わせてください。

## v7: 写真アプリを一から組み立てる

既存の板書カメラの画面・アルバム・保存処理には依存しません。下記は汎用のネイティブ部品です。

| API | 役割 | 権限 |
| --- | --- | --- |
| `ui.coursePicker(key, label)` | 取得済み時間割の授業名を選択しkeyへ保存 | timetable.read |
| `ui.capturePhoto(collection, label, courseField?)` | 利用者操作でOSのカメラを開き専用アルバムに保存 | photos.user, camera.use |
| `ui.importPhotos(collection, label, courseField?)` | 選択した画像だけを専用アルバムに取り込む | photos.user |
| `ui.photoGrid(collection, label, courseField?)` | 写真一覧、拡大、保存・共有、確認付き削除 | photos.user |

同じcollectionとcourseFieldを指定した部品が同じアルバムを参照します。
courseFieldは同じ定義内のcoursePickerのkeyを指定します。省略すると授業に関連しないアルバムになります。
部品の配置・見出し・色・間隔は作者がSDKのレイアウト機能で指定します。
`extensions/examples/board-camera.ts` が通常のビルド・アップロード用サンプルです。
`ui.boardCamera()` は呼び出しません。撮影は標準のカメラであり、無音撮影を保証するAPIではありません。

画像は1枚20MB以内、長辺最大2560pxに変換して端末内へ保存し、位置情報などの画像メタデータを引き継ぎません。
取り込みは1操作20枚まで。写真アプリの元画像には変更を加えません。
アカウント・拡張機能ID・開発版/公開版・collection・選択授業で保存領域を分離します。
拡張機能のバージョンを更新しても拡張機能IDが同じなら写真を引き継ぎます。
選択授業のキーは現状授業名のため、授業名変更後の写真移動は未対応です。
Macは取り込み・閲覧・共有・削除に対応し、カメラ非対応端末では撮影を案内付きで停止します。
写真一覧の部品内レイアウト、画像加工、画像データのTypeScriptへの返却は現状未対応です。

## 拡張機能の削除

作者のコンソールと管理者の審査画面に削除ボタンを用意しています。
`delete-extension` にextension_idとconfirm=trueを指定する認証済み操作です。
作者本人または管理者のみ実行でき、全バージョン・審査履歴・サーバー保存データ・追加情報・利用集計をトランザクションで削除します。
公開・共有リンクも無効になります。端末内の写真を遠隔削除することはありません。

## 利用時の権限確認
公開版は追加・更新確認で承認した宣言権限を利用し、起動のたびに許可を求めません。
未審査の本人用開発プレビューは、アカウント・バージョンごとに初回の同意を端末に保存します。
開発版の新しいバージョンでは再確認します。OSのカメラ・写真等の許可は別途必要です。
審査通過はOS権限やユーザーによる追加確認を代替しません。

## v8: 画面・一覧・状態
| API | 内容 |
| --- | --- |
| ui.page(label, children) | 詳細画面。iOSはナビゲーション、Androidは全画面ダイアログ |
| ui.courseList(key, label, children) | 授業一覧から詳細へ移動しkeyへ授業名を設定。timetable.readが必要 |
| ui.footer(children) | page/courseList直下で下部に固定。他の場所は検証エラー |
| ui.grid(columns, children) | 1〜6列。Androidは狭い画面で列数を減らす |
| ui.disclosure(label, children) | 開閉する詳細説明 |
| ui.dataList(key, query, children, pageSize=20) | courses.data/assignments.data/todos.dataの行を表示。「さらに表示」で続きへ進む |
| ui.number(key, label, min, max, step) | 範囲付き数値操作 |
| ui.slider(key, label, min, max, step) | 範囲付きスライダー |
| actions.setField(label, field, value) | 宣言済みの入力項目を変更 |
| actions.increment(label, field, amount=1) | 数値項目を増減。上限・下限で止まる |

{{memo}} のような入力値をtext、heading、shareText、copyText、画面名に差し込めます。
データ一覧では {{item.title}} のように行の項目を差し込みます。テンプレートは読み取り・表示・共有・ページ構成のみです。
繰り返し内は入力・写真操作・状態変更ボタンに対応しません。
pageSizeは1〜100。元のデータを削除せず、利用者操作で続きを表示します。
外部URL・メディアもHTTPSと個別権限が必要です。

写真一覧は列数指定、枚数表示、複数選択、一括共有、一括確認付き削除に対応しました。
60枚ずつ表示を増やせます。保存済み写真を件数上限で削除しません。
examples/board-camera.ts: 授業一覧、写真グリッド、下部固定の撮影・取り込み。
examples/study-dashboard.ts: 目標入力、進み具合、状態変更、共有、保存、課題一覧。

## 実装範囲と拡張方針
実装済み: ネイティブ画面部品、フォームと状態、宣言的条件表示、本人データの表示、写真・ファイル・メディア、共有、ToDo作成、専用データ保存、開発プレビューと審査・配布。

未実装または一部未接続: 汎用Submit/フォーカス/画面間ナビゲーション、Upload/Download進捗、WebSocket、サーバーFunctions、音声録音、画像加工、無音撮影、生画像データの直接取得、自由なリスト編集/ドラッグ配置。通知予約、位置情報、ToDo CRUD、授業メモ書き込みは個別APIとして接続済みです（各節のバージョンと権限が必要）。

未実装機能を利用できるAPIとして公開しません。権限、利用者確認、保存範囲、キャンセル、対応OS、権限取消、テストを定義し、ホストの実装とセットで追加します。
認証情報・Cookie・OTP・管理サーバーや他ユーザーのデータは公開しません。

## v9: 授業・課題の検索と時刻判定

`cit.courses.list/search/today/current/next` は取得済み時間割の `Course` を返します。`today/current/next` の日時は日本時間として判定します。時刻不明の授業は `current/next` から除外します。`next` は週の繰り返し時間割であり、休講・休日・学期終了を保証しません。登録科目の単位数や履修数を推測しません。

`cit.assignments.list/search/overdue/dueSoon(days, at)` は取得済み課題の `Task` を返します。期限なし・解析できない期限は期限判定から除外します。提出完了状態が取得できないため、期限超過は未提出と同義ではありません。検索は全半角・空白・大小文字を正規化します。未取得の場合は空配列です。

本人の端末内キャッシュの読み取り権限が必要です。サーバーの他ユーザー情報は取得しません。検証用の `runtime-course-query.ts` は固定日時を使用するテスト例です。

## v9: 安全な乱数

`crypto.randomBytes(length)` はOSの暗号用乱数を `Uint8Array` として返します（1〜4096バイト）。`crypto.uuid()` は暗号用乱数によるUUID v4を返します。隔離された実行環境には文字列専用ブリッジで乱数だけを渡し、秘密鍵や認証情報は渡しません。`math.random()` は一般用途の疑似乱数であり、トークン生成には使わないでください。

Mac上のApple WebKitとAndroidエミュレーターのWebViewで検証しています。暗号用乱数を利用できない環境ではエラーになり、Math.randomへの代替は行いません。ハッシュ、署名、暗号化・復号はこのAPIには含まれません。

## v9: ローカルデータベース（部分実装）

`database.createTable/insert/update/delete/get/query/transaction/watch` を提供します。レコードは文字列 `id` を持つJSONデータです。保存領域は本人・拡張機能・開発版/公開版で分離され、既存のローカル保存容量制限が適用されます。`transaction(tx => ...)` は同期コールバックのみを受け付け、成功時に一括保存し、失敗時は保存しません。同じ実行画面内の処理は直列化します。watchはコミット後と別画面更新の確認時に通知し、購読解除関数を返します。

同時更新は下記の比較保存で競合を検出します。他端末からのwatch、SQL、インデックスは未対応です。条件検索チェーンは下記のAPIで対応します。SQLite互換を意味しません。これらが揃うまでは要件28を完成扱いにしません。

### 条件検索

`await database.table('study').where('minutes', '>', 60).orderBy('date', 'desc').limit(20).get()` で検索できます。条件はAND結合です。比較演算子は `== != > >= < <=`、並べ替えは複数フィールドに対応します。文字列と数値は暗黙変換しません。欠損値・null・オブジェクトは並べ替えで末尾になります。条件追加は新しい検索定義を返し、元の定義を変更しません。これは保存済みJSONに対する検索で、SQLやインデックスではありません。

### 競合検出

`storage.compareAndSet(key, expected, value)` は現在値がexpectedと一致するときだけ保存し、成功時はtrue、競合時はfalseを返します。JSONオブジェクトのキー順は比較に影響しません。未保存はnullと比較します。データベースのトランザクションもこの処理を使用し、競合時は `database_conflict` で終了します。コールバックを自動再実行しません。利用者・拡張機能・開発版/公開版の範囲は通常のstorageと同じです。

この比較保存はホストのメインスレッド上で読み取り・比較・書き込みをまとめて実行します。端末内の複数画面の上書きは防ぎますが、別端末へのデータ同期を提供するものではありません。watchは下記の別画面更新監視に対応します。

### 別画面での更新監視

`database.watch(table, onChange, onError?)` は購読中だけ端末内の専用保存領域を約2秒ごとに確認し、内容が変わったときに通知します。初回は現在の内容（未作成なら空配列）を通知します。画面全体の再読み込みやサーバー通信は行いません。同じ画面からのコミットは即時に通知します。

OS中断時は確認を停止し、再開時に最新値を読み込みます。購読解除または画面終了でタイマーを破棄します。読み取り失敗は指定したonErrorへ渡します。他端末との同期や、アプリを終了した後のバックグラウンド監視ではありません。

## v9: 本体の授業メモ読み取り

`cit.courseNotes.list()` と `cit.courseNotes.get(id)` は本体の設定に保存済みの本人の授業メモを返します。専用の `courseNotes.read` 権限が必要です。戻り値は `{ id, courseKey, content }`。IDは本体が授業名を正規化して保存するキーで、大学の科目コードではありません。存在しないIDはnull、未登録の一覧は空配列になります。

本体が既に同期した内容を読むAPIであり、拡張機能から管理サーバーや他ユーザー情報を読みません。作成・更新・削除は下記の書き込みAPIで対応します。授業名変更時のID引き継ぎは未対応です。要件19の全実装完了ではありません。

## v9: ハッシュとHMAC

`await crypto.hash(text, 'SHA-256' | 'SHA-512')`、`await crypto.hmac(text, key, algorithm)` はUTF-8文字列を計算し、小文字の16進数を返します。方式の既定値はSHA-256です。入力は16000文字まで、HMACのキーは1〜4096文字です。バイナリ入力やエンコードの暗黙推測は行いません。

処理は端末内のWeb Cryptoで行い、ネイティブの認証情報やサーバーへ転送しません。HMACのキーは拡張機能自身が指定するものであり、本体の秘密鍵ではありません。MD5・SHA-1・任意の方式は受け付けません。未対応環境ではエラーになり、独自アルゴリズムへの代替はしません。暗号化・復号は下記のAES-GCM APIで対応します。非対称署名は未実装です。

## v9: AES-GCM暗号化・復号

`crypto.generateKey()` は32バイトの暗号用乱数を小文字16進数で返します。`await crypto.encrypt(text,key,aad?)` は `{algorithm:'AES-256-GCM',iv,ciphertext}` を返し、`await crypto.decrypt(envelope,key,aad?)` でUTF-8文字列へ戻します。暗号文には128ビットの認証タグを含みます。96ビットのIVは暗号化ごとにOSの乱数から生成し、作者が指定・再利用する窓口はありません。

平文は4000文字、追加認証データaadは1000文字までです。暗号文・IVは小文字16進数。キー・aadの不一致や暗号文の改ざんはエラーになります。キーを失うと復元できません。キーの保管には拡張機能専用のsecureStorageを使用し、配布JSONへ埋め込まないでください。パスワードからのキー導出、バイナリファイル暗号化、非対称署名は提供していません。端末の本体認証情報・秘密鍵へはアクセスしません。

## 授業メモの作成・更新・削除

`cit.courseNotes.create(title,content)` は未登録の場合だけ作成します。`update(id,content,expectedContent)` と `delete(id,expectedContent)` は、取得時の本文をexpectedContentとして渡します。現在の本文と違う場合は `course_note_conflict` となり、保存・削除しません。専用の `courseNotes.write` 権限が必要です。読む場合には別途 `courseNotes.read` を宣言してください。

保存は本体の授業メモ設定処理へ渡します。空白のみの本文は本体の仕様に従って削除として扱います。更新はメモまたはnull、削除はnullを返します。本文は4000文字、授業名/IDは1000文字までです。ID正規化は本体と同じ処理を使います。授業名変更時のID移行、学内サーバーの科目コードとの紐付けは提供していません。

AndroidのWebViewとテスト専用保存領域で、作成・更新・古い本文による更新拒否・読み取り・削除を検証しています。本体の設定保存処理への接続はApple・Androidのビルドで確認済みです。別端末へのサーバー同期の実動作は未検証です。

## v21: ユーザー専用クラウドKV

`cloud.storage` 権限で `cloudUser.get(key)`、`set(key, expectedRevision, value)`、`delete(key, expectedRevision)` を使用できます。端末ローカルの `storage` / `database` と異なり、同一CIT Hubアカウント・拡張機能の公開環境では端末をまたいで共有されます。開発プレビューはバージョン別領域で、公開環境とは分離されます。サーバーは認証済みユーザー、対象バージョン、インストール状態、権限を各要求で検証します。要求本文のユーザーIDは領域選択に使用しません。SQLite内の値はAES-256-GCMで暗号化され、鍵はDB外のアクセス制限された`cloud-data.key`に保存します。作者向けAPI・管理画面から他ユーザーの値を取得できません。DBバックアップだけでは復元できないため、鍵もアクセス制限された暗号化バックアップへ含めてください。実行中は拡張機能自身へ平文を返すため、信頼できる拡張機能にのみ許可してください。

`get` は `{value, revision}` を返し、未保存値は `{value:null, revision:0}` です。`set` は取得したrevisionを渡し、成功時に新しいrevisionを返します。revisionが古いと `data_conflict` になり、暗黙に上書きしません。`delete` もrevisionを必要とし、存在しない場合はfalseを返します。キーは最大100文字、値はJSONで最大16 KiB、拡張機能・ユーザー・環境ごとに128キー/128 KiBまでです。アカウント間共有、グループ同期、リアルタイムpush、Functions/Cronは別APIであり未実装です。アプリ本体の資格情報やOTPはこのAPIで扱わないでください。

```ts
import { cloudUser } from './sdk/index';
const current = await cloudUser.get<{ theme: string }>('preferences');
const nextRevision = await cloudUser.set('preferences', current.revision, { theme: 'paper' });
```

このAPIはホストAPI v21以降が必要です。古いホストにはサーバーが拡張機能を起動させず、更新を要求します。

## 拡張機能専用ファイル

`files.read/write/copy/move/delete/exists/list/info` は `files.storage` 権限を必要とします。ファイル名は英数字で始まる英数字・`.`・`_`・`-`の1〜100文字で、パス区切りや`..`を拒否します。バイナリは `Uint8Array` で読み書きします。`copy(source,destination)` と `move(source,destination)` は同じ拡張機能専用ディレクトリ内だけで操作し、既存のコピー先を上書きせず `ConflictError` を返します。元ファイルがない場合は `false`、成功時は `true` です。1ファイル256 KiB、拡張機能・アカウント・実行環境ごとに最大5 MiB/100ファイルです。`info` の `modifiedAt` はUnix epochミリ秒です。

この領域はアプリ内の非公開・端末バックアップ対象外ディレクトリに保存し、公開版と開発プレビューを分離します。拡張機能のバージョンを更新しても同じ利用者のデータを引き継ぎますが、他端末へ同期しません。任意の端末ファイルを列挙したり、利用者が選択した添付ファイルを読み取るAPIではありません。選択ファイルUIは引き続き `files.user` 権限です。ホストAPI v11以降が必要です。

サンプル: `extensions/examples/local-file-storage.ts`。

## API互換性

SDKのビルド済み実行型拡張機能は利用APIに応じた `minimumHostAPI` を含みます。通常の実行型拡張機能はv9、授業メモ・外部通信はv10、拡張機能ファイル保存はv11、選択ファイル読み取りはv12、クリップボードはv13、OS設定への遷移はv14、ローカル通知はv15、構造化時間割・課題はv16、ToDo完了操作はv17、学年暦読み取りはv18、バスv19、食堂v20、本人専用Cloud KV v21、文字UI v22、複数行入力v23、位置情報v24、接続状態v25、レイアウトv26、文字スタイルv27、ネットワーク変更購読v28、独立スクロールv29、ファイル複製・移動v30、本人の有効サービス一覧v31、状態同期する選択UI v32、専用領域とのファイル転送v33、状態同期する複数選択v34、日時選択v35、開始・終了日時の範囲入力v36、State同期するStepper・Checkbox・Radio v37、ネイティブ進捗表示v38、読み込み・空・エラー状態v39、カード・グループ・レスポンシブコンテナv40、制限付きネイティブアニメーションv41、検証済みネイティブ横棒グラフv42、Safe Areaとレスポンシブ寸法v43、入力フォーカス・単一行送信イベントv44、アクセシブルなネイティブアイコン操作v45、状態同期するネイティブ検索欄v46です。配布サーバーは追加・起動時にアプリの対応APIを比較し、足りなければ `host_update_required` を返します。

`minimumHostAPI` は整数1〜46です。アプリ自身のバージョン番号とAPI番号は別です。対応していないアプリでAPIを呼び出してから失敗する状態を避けるため、新しいSDKは使用APIを検出してこの指定を自動で入れます。

## 権限状態の確認

`await permissions.status(permission)` は拡張機能の宣言とOSの状態を区別します。未宣言は `undeclared`、カメラと位置情報は実際のOS許可状態、写真・ファイルは利用者が選択したものだけ扱うため `user-selection` を返します。Appleのカメラ未選択状態は `prompt`。Androidでは現在カメラが許可されていなければ `denied` です。statusはOSの許可ダイアログを表示しません。

読み取り・書き込みなどの宣言済みの本体データ権限は `granted` ですが、データ未取得やAPI未接続を意味するものではありません。APIの利用可否は別途capabilities.hasで確認してください。`request()` は `camera.use`、`location.use`、`notifications.schedule` に対応し、OS権限は直接のボタン操作から要求し、必要に応じてOSの許可ダイアログを表示します。`openSettings()` は `settings.open` と直接操作を必要とします。どれもOSの実際の状態・対応範囲に従います。

### カメラ許可要求（検証中）

`await permissions.request('camera.use')` は宣言済みのカメラ権限だけを対象に、必要ならOSの確認を表示し、granted/deniedを返します。既に許可済みなら再表示しません。未宣言の要求はpermission_requiredです。写真・ファイルは選択式なので、このrequestの対象ではありません。

確認待ちは60秒まで。閉じた実行画面の応答は新しい画面へ転送しません。Apple・Androidでのビルドやテスト状況は機能ごとの実装追跡を参照してください。OSダイアログの許可・拒否や設定画面への遷移を全対応OS・実機で確認したことを意味しません。
# 触覚フィードバックとカメラ許可（Host API 10）

## 表示支援設定

`accessibility.status()` は動きを減らす設定、高コントラスト設定、読み上げ状態を返します。
`isReduceMotionEnabled()` と `isHighContrastEnabled()` でも個別取得できます。
AppleではOSの「視差効果を減らす」「コントラストを上げる」を参照します。
Androidの動作設定はOSのアニメーター無効設定を参照します。高コントラストの公開APIは
Android 16（API 36）以降で利用し、旧OSの `status().highContrast` は `null`、
個別取得は `UnsupportedError('high_contrast_unavailable')` です。
Androidの読み上げ状態はタッチガイドが有効かどうかです。
取得時点の値であり、設定変更後は再取得してください。OS設定自体は変更しません。
UI部品には `accessibility.label(node, text)`、`accessibility.hint(node, text)`、
`accessibility.role(node, role)` を使えます。文字・見出し・ボタン・切り替え・入力・リンクの部品に対応し、
役割が実際の部品と合わない指定、未対応部品、空または1000文字を超えるラベルやヒントは拒否します。
OS標準のクリック・入力セマンティクスを保ったまま読み上げ名とヒントを設定します。AndroidのComposeは
専用リンク役割を提供しないため、リンクは標準のクリック可能なコントロールの読み上げ表現になります。
見出しの階層レベル指定とVoiceOver/TalkBackでの手動読み上げ確認は未対応です。

```ts
accessibility.label(ui.text('読み上げる名前'), '今日の授業');
accessibility.hint(interactive.button('詳細', openDetails), '授業情報を表示します');
accessibility.role(ui.heading('時間割'), 'heading');
```


## キーボード

`interactive.secureField(key, label, state)` と `ui.secureField(key, label)` は、
入力文字を画面上で伏せて表示するOS標準の保護入力欄を描画します。入力値は拡張機能の実行中に
TypeScript側へ渡るため、保存する場合は拡張機能自身で保存先を明示してください。
画面上で隠す機能であり、拡張機能コードから入力値を秘匿する仕組みではありません。

`keyboard.dismiss()` は拡張機能画面の入力フォーカスを解除し、Androidでは
その画面のソフトウェアキーボードにも非表示を要求します。画面側が接続されている場合だけ
`capabilities.has('keyboard.dismiss')` が真になります。未接続の場合は
`UnsupportedError` です。Macでも入力フォーカスを解除しますが、物理キーボードの
有効・無効は変更しません。
`keyboard.focus(key)` は画面に表示されている入力欄のキーを指定します。
未表示・同一キー重複・操作不可の入力欄は `input_not_visible`、不正なキーは
`input_invalid`、画面側が未接続の場合は `unsupported` になります。
閉じたページを自動で開く機能ではありません。実際のフォーカス移動と
ソフトウェアキーボードの表示は、表示状態とOSの判断にも依存します。

`interactive.secureField(key, label, state)` と `ui.secureField(key, label)` は、
入力文字を画面上で伏せて表示するOS標準の保護入力欄です。入力値は実行中のTypeScript側へ
渡るため、画面上で隠す機能であり拡張機能コードから値を秘匿するものではありません。
保存先を選ぶ場合は、拡張機能側で処理を明示してください。


## 本体のToDoの読み取り

`cit.todo.list()`、`get(id)`、`search(query)` は `todos.read` 宣言が必要です。
検索はタイトルと本文を対象に、全角・半角や大文字・小文字を揃えて判定します。
返却値はID・タイトル・本文・URL・期限・通知方式・通知日時・作成日時・完了状態・添付のIDと表示名です。
添付の保存パス・ファイル内容は返しません。添付の追加や表示名変更も更新・削除時の競合判定に含みます。
日付はISO 8601文字列、未設定の期限と通知日時は `null`、通知方式は
`none`・`once`・`weekly` です。端末内で現在利用する本人のToDoだけを取得します。

`cit.todo.watch(callback, onError?)` は初回データと変更を非同期で通知し、解除関数を返します。
2秒間隔で取得するため即時Pushではありません。同じ内容を繰り返し通知せず、
画面終了時は解除、アプリが裏に回ると停止、復帰時は再取得します。
完了状態は本人のToDo保存データに記録され、`complete` と `uncomplete` で変更できます。これは端末内のToDo状態で、別端末への同期は現状保証しません。

`cit.todo.create(fields)`、`update(expected, fields)`、`complete(id)`、
`uncomplete(id)`、`delete(expected)` は
`todos.write` 宣言が必要です。`expected` には直前に取得したToDo全体を渡します。
現在の内容と異なると `todo_conflict` になり、上書き・削除しません。
作成・更新は保存されたToDo、削除は `null` を返します。
完了・未完了の変更は直前に読んだ値と競合しない場合に更新後のToDoを返し、IDがなければ `null` を返します。
`fields` はタイトル（必須）、本文、URL、期限、通知方式、通知日時です。
更新は全フィールドの置換なので、維持する値も渡してください。
添付ファイルは更新時に保持され、削除時は本体と同じく削除されます。
URLはHTTP/HTTPSのみ、期限は未来、毎週通知は期限なし、通知は未来かつ期限以前です。
本体の保存と通知再登録を使用します。拡張機能専用ストレージには保存しません。
完了・未完了APIを使う拡張機能はホストAPI v17以降が必要です。SDKビルダーが利用状況から設定します。


## 端末の表示情報

`device.info()` はOS・OSバージョン・端末区分・画面の論理サイズ・表示倍率・
文字倍率・言語・タイムゾーン・現在の明暗・対応API一覧を非同期で返します。
`platform()`、`osVersion()`、`deviceClass()`、`screen()`、`locale()`、`timezone()`、
`appearance()`、`capabilities()` でも個別取得できます。端末識別子・製造番号・
アカウント名・端末名は返しません。画面情報は取得時点の値です。回転やウィンドウの
変更後は再取得してください。iOS/Macの画面サイズは接続したウィンドウ、Androidは
現在の画面構成のdp値です。固定座標ではなくレスポンシブ配置の補助に使用してください。


`haptics.light()`、`medium()`、`heavy()`、`success()`、`warning()`、`error()` は
OSの触覚フィードバックを呼び出す非同期APIです。事前に `capabilities.has('haptics')`
で端末の対応を確認できます。Macと振動機能のない端末では `UnsupportedError` になります。
100ms未満の連続要求は `haptics_rate_limited` で拒否します。Androidの通知型振動は
端末標準の振動波形を使用するため、Apple端末と物理的な振動は同一ではありません。

`permissions.request('camera.use')` は宣言済みのカメラ権限についてOSの許可画面を
表示し、`granted` または `denied` を返します。未宣言の要求は拒否されます。
端末ですでに拒否された場合は自動で設定変更せず `denied` を返します。
許可を求める前に、画面上で用途を説明してください。許可画面の表示だけでは
撮影や写真の取得は行いません。カメラ許可要求の待機上限は60秒です。

## 位置情報（API v24）

`location.use` を宣言すると、直接のユーザー操作中に `permissions.request('location.use')` でOSの「使用中のみ」許可を要求できます。許可後、同じ操作から `location.current()` を呼ぶと、1回限りの位置情報（緯度・経度・精度・取得時刻・精度区分）を返します。バックグラウンド取得、常時追跡、位置の保存・サーバー送信は行いません。位置精度はOSの設定や端末の状態に従います。拒否・取得不可・タイムアウトはエラーとして返します。サンプルは `examples/foreground-location.ts` です。

## 接続状態（API v25）

`network.status` を宣言した拡張機能は `network.status()` で、その時点の接続有無、Wi-Fi/モバイル/有線/その他/未接続の種別、従量制限、低データモード相当の制約、OSが検証したインターネット到達性を取得できます。Wi-Fi名、IPアドレス、MACアドレス、基地局情報は返しません。iOS/macOSでは外部インターネット到達性をOS APIから確定できないため `internetValidated` は `null` です。これは端末内の状態照会で、追加のOS許可ダイアログはありません。SDKは宣言漏れを検査し、ホストも実行時に再確認します。サンプルは `examples/runtime-network-status.ts` です。

## レイアウト配置（API v26）

`ui.vstack(children)` / `ui.hstack(children)` は縦・横方向に並べ、`ui.zstack(children)` は同じ領域へ重ねて配置します。`ui.spacer(size)` は指定した辺長（0〜120 pt相当）の透明な固定間隔です。未指定時は12です。任意コードや任意の座標指定は許可せず、ネイティブの標準レイアウト内で表現します。v26部品を含む拡張機能は最低host API v26を要求し、古いアプリではインストール・起動できません。

## 文字スタイル（API v27）

`ui.styled(node, { weight, lineLimit, selectable })` で文字の太さ（`regular` / `medium` / `semibold` / `bold`）、表示行数（1〜100）、テキスト選択の可否を指定できます。未指定の値は各ネイティブ部品の標準表示を維持します。v27のスタイルを使う拡張機能は最低host API v27を要求します。

## ネットワーク状態の変更通知（API v28）

manifestに `network.status` を宣言した拡張機能は `network.onChange(callback)` で接続状態の変化を購読できます。戻り値の関数で購読解除します。アプリが中断・終了状態に入るとOS監視を止め、復帰時に再開します。状態のpayloadは `network.status()` と同じ接続状態情報で、Wi-Fi名やネットワークアドレスは含みません。権限がない場合は `PermissionError` になります。これを使う拡張機能は最低host API v28を要求します。

## 縦・横スクロール（API v29）

`ui.scroll(children)` は縦方向、`ui.horizontalScroll(children)` は横方向にスクロールするOS標準コンテナを返します。固定座標やコンテンツの強制サイズは設定せず、子要素の自然なサイズに合わせます。通常の `ui.page` 自体も縦スクロールするため、ページ内の独立したスクロール領域を作る場合に使用します。どちらかを使う拡張機能はhost API v29が必要です。

## 拡張機能ファイルの移動・複製（API v30）

`files.copy(source,destination)` と `files.move(source,destination)` は `files.storage` が許可された拡張機能専用領域の中だけで動作します。コピー先に既存ファイルがあれば上書きせず `ConflictError` になります。元ファイルがないときは `false`、成功したときは `true` を返します。拡張機能がこれらを使うとSDKビルダーはminimumHostAPI 30を付け、古いホストでの起動をサーバーが拒否します。

## 入力コントロール（Host API v37）

`interactive.stepper(key,label,state,min,max,step)`、`interactive.checkbox(key,label,state)`、`interactive.radio(key,label,options,state)` はOS標準のネイティブ入力部品を表示し、変更値を型付きイベントでTypeScriptのStateへ戻します。Stepperは範囲と刻みを検証し、Radioは1〜30個の一意な選択肢を受け付けます。SDKビルダーは最低Host API v37を自動設定します。例は `extensions/examples/interactive-form-controls.ts` です。

## 進捗表示（Host API v38）

`ui.progress(label, value)` はラベル付きのOSネイティブな線形進捗表示を返します。`value` は0（未開始）から1（完了）までの有限値です。範囲外や空のラベルはビルド・サーバー検証で拒否され、SDKビルダーは最低Host API v38を指定します。例は `extensions/examples/progress-status.ts` です。

## 読み込み・空・エラー状態（Host API v39）

`ui.loading(label)`、`ui.emptyState(title, message)`、`ui.errorState(title, message)` は通信中・該当データなし・回復可能なエラーを区別して伝える標準状態UIです。iOS・iPadOS・macOS・Androidでネイティブ描画されます。ラベルや説明文が空の状態はビルド・サーバー検証で拒否されます。ビルダーは使用を検出し `minimumHostAPI: 39` を設定するため、旧ホストでは起動できません。

```ts
ui.loading('時間割を読み込み中');
ui.emptyState('課題はありません', '新しい課題が届くとここに表示されます');
ui.errorState('読み込めませんでした', '通信状態を確認して、もう一度お試しください');
```

## レスポンシブなカードとコンテナ（Host API v40）

`ui.card(children, label?)` はOS標準のグループ面として内容をまとめ、`ui.group(children)` は背景を追加せず要素間の間隔だけを管理します。`ui.container(children)` は画面幅に合わせて利用可能な幅を使うレイアウト領域です。いずれも固定座標を使わず、iOS・iPadOS・macOS・Androidでネイティブ描画されます。ビルダーは使用を検出して `minimumHostAPI: 40` を設定し、旧ホストはサーバー側で拒否します。

```ts
ui.container([
  ui.card([ui.heading('今週の授業'), ui.data('timetable.week')], '時間割'),
  ui.group([ui.caption('必要な操作'), ui.button('更新', 'refresh')])
]);
```

## Safe Area とレスポンシブ寸法（Host API v43）

`ui.safeArea(children)` はノッチ、システムバー、画面端の安全領域を避けるネイティブコンテナです。`ui.frame(node, { minWidth, maxWidth, minHeight, maxHeight, aspectRatio })` はポイント（iOS/macOS）またはdp（Android）単位で寸法を制約します。幅・高さは0〜2000、最小値は最大値以下、縦横比は0.1〜10です。固定座標の代わりに親幅と寸法制約を使い、端末サイズへ適応させてください。SDKビルダーは最低Host API v43を付け、範囲外・矛盾した値はビルドとサーバー検証で拒否します。

```ts
ui.safeArea([
  ui.frame(ui.column([
    ui.heading('今週の授業'),
    ui.data('timetable.periods'),
  ]), { minWidth: 240, maxWidth: 860 }),
]);
```

## 入力フォーカスと送信イベント（Host API v44）

`interactive.textField` と `interactive.secureField` は、OSネイティブ入力の `onFocus`、`onBlur`、`onSubmit` コールバックを受け取れます。イベントはフォーカスが実際に変化した時だけ通知し、単一行の送信はキーボードの完了操作で通知します。複数行 `textArea` は改行入力と送信操作を混同しないため `onSubmit` を提供しません。イベントはQuickJS内で処理し、入力値・フォーカス状態をサーバーへ送信しません。

```ts
interactive.textField('query', 'キーワード', query, {
  onFocus: () => status.set('入力中'),
  onBlur: () => status.set('入力終了'),
  onSubmit: () => status.set(`検索: ${query.value}`),
});
```

SDKビルダーはイベントを含む拡張機能に `minimumHostAPI: 44` を設定します。配信サーバーは宣言漏れ、無効なイベントID、非入力部品へのイベント付与を拒否し、v43以前のホストでの実行も拒否します。


## ネイティブアイコンボタン（Host API v45）

`interactive.iconButton(icon, label, onPress)`はOS標準アイコンを44pt/dp以上のタップ領域で表示し、VoiceOver/TalkBack向けのアクセシブルなラベルを提供します。許可アイコンは `book`、`calendar`、`bus`、`check`、`clock`、`info`、`map`、`person`、`photo`、`plus`、`search`、`edit`、`list`、`bell`。コールバックはユーザーのタップでQuickJS内から呼ばれ、無効なアイコン・イベントID・ラベルはSDKとホストで拒否します。SDKは `minimumHostAPI: 45` を自動設定し、v44以前のホストでは起動できません。

```ts
interactive.iconButton("plus", "回数を増やす", () => count.set(count.value + 1));
```

## ネイティブ検索欄（Host API v46）

`interactive.searchField(key, label, value, events?)` はiOS/macOSとAndroidのネイティブ入力を検索用に構成します。値は `State<string>` に同期され、4000文字までです。検索アイコン、入力を消す操作、検索用キーボードアクションを備え、任意で `onFocus`・`onBlur`・`onSubmit` を指定できます。`onSubmit` は検索キー／検索アクションでのみ呼び出されます。SDKビルダーはこのAPIを検出して `minimumHostAPI: 46` を設定し、Host API v45以前での実行を拒否します。

```ts
const query = state.create('');
interactive.searchField('query', '課題名を入力', query, {
  onSubmit: () => searchAssignments(query.value),
});
```

サンプル: `extensions/examples/interactive-search-field.ts`。
