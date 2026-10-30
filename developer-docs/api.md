# Host API v80: サーバーファイル

2026年10月29日現在、CloudflareのAndroid Play Integrity検証Workerと拡張機能サーバー本番サーバーの稼働を確認しています。拡張機能サーバーはrelease v87.1、公開ヘルスチェックHTTP 200、Host API v80、`authenticated`登録モードです。開発者登録ポリシーは有効ですが、実ユーザーによる新規登録E2Eは未確認です。OS別のテスト範囲と制約は[API監査一覧](api-inventory.md)を参照してください。

`server.files` は拡張機能サーバーのCIT Hub拡張機能サーバーに、ファイルをユーザー・拡張機能・環境別に保存します。`cloud.storage`を宣言してください。開発環境と本番環境は分離され、他ユーザーのファイルを参照できません。内容は保存時にサーバー側でAES-256-GCM暗号化されます。

```ts
const files = await server.files.list();
const created = await server.files.put('notes', {
  name: 'notes.txt', mimeType: 'text/plain', bytes: new TextEncoder().encode('Hello')
}, 0);
const saved = await server.files.get('notes');
if (saved) console.log(new TextDecoder().decode(saved.bytes));
await server.files.delete('notes', created.revision);
```

1ファイル256 KiB、環境ごとに合計5 MiB・100ファイルです。新規作成はexpectedRevision=0、その後の更新・削除は最新revisionを指定します。競合時は`data_conflict`です。ファイルIDは英数字・`_ . -`のみ、ファイル名にパス区切りを指定できません。任意の端末ファイルシステムへはアクセスしません。

# Host API v65: 食堂営業時間

`cit.cafeteria.hours()` はCITサービス公式掲載の通常期・休暇期の営業時間を返します。`cafeteria.read` が必要で、休業日・臨時変更・当日の営業状態は判定しません。返却値には公式参照URLが含まれます。

`cit.campus.facilities/buildings/rooms/room/search/roomAvailability` は `timetable.read` で本人の取得済み時間割に含まれる教室情報のみを検索します。3桁教室は津田沼、4桁教室は新習志野、それ以外は不明と分類します。`roomAvailability` は本人の時間割上の授業だけを返し、大学全体の空室情報ではありません。

# Host API v64: 本人プロフィール・書類スキャン

Host API v64で追加した機能です。`cit.user.displayName()` は `profile.read` 権限で本人の端末内にキャッシュされたmanaba表示名だけを返します。`scanner.document()` と `image.scanDocument()` は利用者の操作で書類をスキャンし、PDFを拡張機能専用領域へ保存します。両方とも `camera.use` と `files.storage` が必要です。PDFは最大5 MiBで既存の5 MiB保存枠を共有します。iOSはVisionKit、AndroidはGoogle ML KitのOS提供スキャナーを利用し、Mac Catalystでは提供しません。保存先にアクセスできるのは同じ拡張機能だけです。本番サーバーへの配備と実機操作確認は別途必要です。

```ts
const result = await scanner.document({ name: 'lecture.pdf', maxPages: 8 });
if (result) await files.export(result.name, result.name);
```

`image.scanDocument()` は同じスキャン処理の別名です。キャンセル時は `null`、スキャンサイズ超過時はエラーになります。ビルダーとサーバーはこのAPIを検出し、Host API 64と両権限を要求します。

| API | 返却値 | 必要な宣言 |
| --- | --- | --- |
| `navigation.openHostPage(page)` | Promise<void> | host.navigate |
| `cit.services.open(service)` | Promise<void> | host.navigate |
| `cit.user.displayName()` | Promise<string|null> | profile.read |
| `cit.cafeteria.openCamera()` | Promise<void> | host.navigate, cafeteria.read |
| `extensions.isInstalled(id)` | Promise<boolean> | extensions.read |
| `extensions.open(id)`, `navigation.openExtension(id)` | Promise<void> | extensions.read, host.navigate |
| `extensions.request(id)`, `link.open(url)` | Promise<void> | extensions.read, host.navigate |
| `link.create(id)` | Promise<string> | extensions.read |

画面移動は直接のボタン操作からのみ実行できます。pageはtimetable / todo / services / extensions / settings。serviceはmanaba / portal / cafeteria / bus。学内サービスはアプリの既存画面を選択するので、既存のWebセッションと表示設定を維持します。非表示サービスはservice_unavailableとなり、勝手に有効化しません。食堂カメラは既存の3カメラ一覧を開き、11:00〜14:00の稼働時間案内・時間外更新停止を維持します。

拡張機能IDは小文字16進32文字。isInstalledは認証済みの本人のインストール記録だけを参照します。カタログの上位100件を検索する方式ではなく、ID指定のinstalled-infoを使用します。openは追加済みの版を開き、未追加はextension_not_installed。公開停止・削除済みは対象外です。開発プレビューは対象外です。起動先でも既存のサーバー起動検査が働きます。

requestは公開中の拡張機能の追加確認画面を開きます。自動インストールや他の拡張機能へのデータ受け渡しはしません。link.createは公開状態を確認して https://developer.cithub.app/extensions/<id> を返します。このページにはCIT Hubで追加するボタンとストア導線があります。インストール後の自動復元ではなく、同じリンクを再度開く方式です。link.openはこのHTTPS形式またはcithub://extensions?id=<id>だけを受理し、任意URLは受理しません。移動APIのPromise完了は要求の受付を意味し、遷移先の読み込み完了は保証しません。通信失敗はnetwork_extension_lookup_failed。例: examples/host-navigation.ts。

---

# Host API v61: 撮影・音声録音・写真保存・QR生成

対応済みのネイティブHost APIはv80です。要件433メソッドすべてにSDKの型付き呼び出し口がありますが、これは実行可能なネイティブ機能433件や各APIの個別E2Eを意味しません。2026-10-29の監査ではiOS 27.1 Simulator XCTest 32件、Android API 33 AVD計装テスト116件、Mac Catalystスモーク14系統を実行しました。SDK契約433/433、Node focused suite 405要件行、Android 433行、iOS 276行、Catalyst 255行に通過証拠があり、3環境すべてに対応するのは250/433要件行です。これらはテスト群とAPI要件の対応表であり、独立APIごとの網羅や実機ハードウェア動作を証明しません。APIごとの通過fixture対応表は[`api-inventory.md`](api-inventory.md) / [`api-inventory.json`](api-inventory.json)を参照してください。`background.register/unregister`はHost API v71以降で拡張機能サーバーの承認済み`serverFunctions`へ接続し、サーバー稼働中のベストエフォートで実行します。任意の端末OSコードは実行しません。検索APIとアプリ内タイル、Androidの動的ランチャーショートカット、AppleのApp Intentsは実装済みです。`widget.define/reload`はAndroidの計装テストおよびiOS Simulator / Mac CatalystのQuickJSネイティブホストテストで定義の保存と更新時刻を確認済みです。カードの画面描画・更新連携は未確認です。ウィジェットはCIT Hub内の拡張機能画面に表示する端末内機能で、OSホーム画面ウィジェットではなく、他端末にも同期しません。`host.registerAction`はSDKで遷移先を必須化し、許可ページ・サービス名だけを受け付けるアプリ内遷移ボタンです。`shortcuts.register`は`host.registerShortcut`と同じアプリ内タイルを登録します。`push.sendToCurrentUser`はiOS/iPadOS/Android/Mac CatalystとCloudflare Workerへ接続済みですが、Pushの実配信は確認していません。通知許可・APNs/FCM端末登録が必要です。OAuth 2.0 PKCEとOIDC RS256検証は実装済みですが、外部プロバイダーとの実認証フローは確認していません。

監査上、SDKの86個のリテラルHostブリッジ操作名はSwift/Kotlinソース双方への静的照合で不一致0です。これは文字列の存在確認であり、433メソッドすべてのランタイム動作を証明しません。詳細な未監査項目は実装一覧を参照してください。

| API | 返却値 | 必要な宣言 |
| --- | --- | --- |
| `camera.capturePhoto()` | `Promise<PickedFileInfo\|null>` | `camera.use`, `files.selection.read` |
| `camera.captureVideo(maxSeconds=30)` | `Promise<PickedFileInfo\|null>` | 上記 + `microphone.use` |
| `audio.record(maxSeconds=60)` | `Promise<PickedFileInfo\|null>` | `microphone.use`, `files.selection.read` |
| `audio.stopRecording()` | `Promise<boolean>` | `microphone.use`, `files.selection.read` |
| `photos.save(key)` | `Promise<boolean>` | `photos.user`, `files.selection.read` |
| `code.generateQR(text,name,size=512)` | `Promise<{name,size,mimeType:'image/png',width,height}>` | `files.storage` |

## 撮影

直接のボタン操作からOSの撮影画面を開きます。秘密撮影・無音撮影のAPIではありません。キャンセルはnull、OSの許可拒否は`camera_permission_denied`／`microphone_permission_denied`です。写真・動画は拡張機能の選択ファイル領域へ保存し、同じ拡張機能からファイル情報・プレビュー・共有・再生に使用できます。自動アップロードはしません。撮影データは10MiB以内。iOSの写真は向き補正・最大2048pxのJPEG、Androidは端末のカメラアプリが生成した画像です。動画は1〜60秒を指定し、Androidではカメラアプリにも時間・サイズ制限を渡したうえで取得後の動画長も検査します。要求を超えた動画は切り捨てずエラーにします。iOSはQuickTime、Androidはカメラアプリの形式です。

Mac Catalystには撮影Capabilityを提供せず、呼び出しは`unsupported`になります。カメラのない端末も非対応です。`capabilities.has('camera.capturePhoto')`／`camera.captureVideo`を確認してください。SDK例は`examples/capture-and-record.ts`です。

## 録音

直接操作から録音専用画面を開き、必要時にOSのマイク権限を求めます。画面内の「録音を開始」を押すまで録音しません。「停止して保存」で返却し、キャンセルはnullです。1〜300秒、AAC/MP4、モノラル44.1kHz・64kbpsで録音します。録音開始前の画面が60秒操作されない場合は閉じます。上限時間・バックグラウンド移行・画面離脱でも終了し、有効な録音を保存します。拡張機能終了はキャンセルし一時ファイルを削除します。録音機器が使えない場合は失敗を返し、空の録音を成功扱いにしません。

`audio.record()`のPromiseは録音終了時に解決します。`audio.stopRecording()`は実行中の録音を終了し、存在すればtrue、存在しなければfalseを返します。停止は追加のジェスチャー不要で、アプリを閉じた後の録音やバックグラウンドサービスは提供しません。`permissions.status/request('microphone.use')`も使用できます。Macには音声入力のSandbox entitlementを追加しています。AndroidはRECORD_AUDIOをOSに宣言し、必要時に実行時許可を求めます。

## 写真保存とQR生成

`photos.save(key)`は本人が選択・撮影したJPEG/PNGのみを対象とし、端末内の確認画面で保存を承認した後、写真ライブラリへ追加します。キャンセルはfalseです。iOSは追加専用のPhotoKit権限、Android 10以降はMediaStore、旧Androidは必要時に書き込み許可を求めます。失敗した未完成の追加データを削除します。任意の端末パスや他の拡張機能のファイルは指定できません。

QR生成は外部通信不要です。UTF-8で1〜1000バイト、要求サイズ256〜1024px、誤り訂正M・四辺に4モジュールの余白、PNGを拡張機能専用ファイル領域へ保存します。iOSは整数モジュールで描画するため実寸が指定値以下になる場合があります。返却width/heightが実寸、sizeはバイト数です。出力名は通常の専用ファイル名の規則に従い、出力は256KiB以内。`files.read/export`で使用できます。生成ファイル名は写真の選択キーではないため、`photos.save`へそのまま渡しません。

主なエラー: `recording_busy`, `recording_start_failed`, `recording_failed`, `recording_save_failed`, `capture_options_invalid`, `capture_too_large`, `capture_duration_exceeded`, `capture_save_failed`, `photos_permission_denied`, `photos_save_failed`, `qr_options_invalid`, `qr_generation_failed`。エラーは表示して処理し、成功を仮装しません。

仕様確認先: [Apple AVAudioRecorder](https://developer.apple.com/documentation/avfaudio/avaudiorecorder)、[Android MediaRecorder](https://developer.android.com/media/platform/mediarecorder)、[Android MediaStore](https://developer.android.com/training/data-storage/shared/media)、[ZXing](https://github.com/zxing/zxing)。端末別の録音品質やカメラアプリの挙動は実機検証が必要です。

---

# 拡張機能API v60

## v60: センサー・経路・表示言語・グループ共有

ローカル実装の契約です。SDKは0.60.0、最低Host APIは60です。本番サーバーへの配備・実機での操作試験は別途必要で、全APIの完成を意味しません。

### 端末の動き

`sensors.motion`を宣言し、画面上のボタンから呼び出します。各呼び出しは新しい測定値1件を返し、連続ストリーミングではありません。読み取りは同時に1件、開始間隔は最低100ms、最長5秒です。画面を閉じるかアプリを離れると停止します。値をCIT Hubが自動保存・送信することはありません。

| API | 戻り値 |
| --- | --- |
| `sensors.accelerometer()` | `{x,y,z,unit:'m/s2',timestamp}`。重力を含む加速度。Appleの重力単位と符号を変換 |
| `sensors.gyroscope()` | `{x,y,z,unit:'rad/s',timestamp}` |
| `sensors.magnetometer()` | `{x,y,z,unit:'microtesla',timestamp}` |
| `sensors.orientation()` | `{x,y,z,w,reference,timestamp}`。単位クォータニオン |

座標軸は端末本体の軸で、表示の回転に追従する画面座標ではありません。姿勢のreferenceは原点の方位が任意の`arbitrary`、Androidで磁北基準センサーへフォールバックした場合は`magneticNorth`です。両者を同じ方位として扱わないでください。timestampは測定時刻のISO 8601文字列です。Macやシミュレーターなどセンサーがない端末ではCapabilityを公開せず`unsupported`を返します。`sensor_busy / sensor_timeout / sensor_cancelled / sensor_unavailable / sensor_permission_denied / rate_limited`を捕捉できます。OSによる制限やハードウェアの欠如をゼロ値で偽装しません。

[Apple Core Motion](https://developer.apple.com/documentation/coremotion/cmmotionmanager)と[Android Sensors](https://developer.android.com/develop/sensors-and-location/sensors/sensors_motion)を使用します。iOSのMotion利用説明文をアプリに追加し、Androidは低頻度の通常センサー読み取りで高頻度センサー権限を要求しません。

### 経路と表示言語

`map.openDirections({latitude,longitude},{name?,mode?})`は、利用者操作からAppleのマップ、またはAndroidの地図対応アプリ／ブラウザを開きます。`browser.open`が必要です。modeは`walking`（既定）、`driving`、`transit`。緯度±90、経度±180、名称120文字以内です。自動で現在地を取得せず、経路表示先への目的地の送信は利用者操作によるものです。拡張機能内の地図部品とは別機能です。地図アプリが開けなければ`map_open_failed`を返します。

`cit.settings.language()`は`settings.read`で、現在ホストが使用している優先言語をBCP 47形式で返します。アプリが未提供の言語へ翻訳されていることを意味しません。

### 招待制グループと共有データ

`cloud.shared`を宣言します。グループは拡張機能・公開版／プレビュー版ごとに分離します。本人の時間割、OTP、Cookie、他ユーザーの非公開情報や管理サーバーへアクセスするAPIではありません。共有するvalueは拡張機能が明示的に指定した内容のみです。個人情報や秘密情報を共有valueに入れないでください。

| API | 戻り値・動作 |
| --- | --- |
| `groups.create(name)` | `ExtensionGroup`。名称1〜80文字。作成者はowner |
| `groups.list()` | 本人が参加済みのグループ一覧。別端末からも取得可能 |
| `groups.invite(groupID,{expiresInHours?,maxUses?})` | `{invitation,expiresAt,maxUses}`。ownerのみ発行 |
| `groups.join(invitation)` | ホスト側で参加確認。承認後に`ExtensionGroup`、取消時はnull |
| `groups.members(groupID)` | `{id,role,joinedAt}[]`。参加者のみ。idはグループ固有の匿名ID |
| `groups.leave(groupID)` | ホスト側で退出確認。承認時`{left:true}`、取消時null |
| `cloud.shared.create<T>(groupID,key,value)` | `SharedValue<T>`。既存キーなら競合 |
| `cloud.shared.read<T>(groupID,key)` | `SharedValue<T>`またはnull。参加者のみ読み取り可能 |
| `cloud.shared.update(groupID,key,value,expectedRevision)` | 更新後のSharedValue。全置換、本人の記録またはownerのみ |
| `cloud.shared.delete(groupID,key,expectedRevision)` | `{deleted:true}`。本人の記録またはownerのみ |

ExtensionGroupは`{id,name,role,memberID,createdAt}`、SharedValueは`{key,value,revision,authorID,updatedAt}`です。実名・メール・学番をメンバーAPIから取得できません。匿名IDはグループごとに異なり、サーバーの秘密鍵によるHMACで生成します。名称と共有valueは暗号化保存、招待コードはハッシュのみ保存します。

招待は既定24時間・1人、最大168時間・100人、同時に有効な招待はグループ5件までです。参加と招待使用回数の消費は同じトランザクションで行います。1グループ100人、本人の参加先は1拡張機能・環境あたり20件まで。valueはJSON、16KiB・深さ12、グループ全体は200件／1MiBまでです。グループ内でrevisionが単調増加するため、削除して同じキーを作り直しても古い更新は拒否されます。競合時は再取得して確認し、無条件で上書きしないでください。

取得・一覧以外の操作は直接のユーザー操作が必要です。参加・退出確認は拡張機能が省略できないホストの画面です。退出すると本人が作成した共有記録も削除され、ownerは他の参加者がいる間は通常の退出ができません。アンインストール時に保存データ削除を選ぶと本人の共有記録・参加状態も削除し、ownerだった場合は残った最古の参加者に所有権を移します。誰も残らないグループは削除し、以前ownerが発行した招待も取り消します。

代表エラーは`not_allowed / invitation_invalid / invitation_limit / group_not_found / group_full / group_limit / group_owner_cannot_leave / shared_conflict / shared_not_found / shared_value_invalid / storage_quota`です。サーバーは実行版・インストール・作者プレビュー権限も毎回確認します。

作成例は`examples/motion-directions.ts`、`examples/group-notes.ts`、`examples/realtime.ts`を参照してください。Realtimeは招待制グループの最新イベントをHTTPSポーリングで取得します。Pushは要求元端末への通知要求、Cronは拡張機能サーバー上の承認済みServer Functionの定期実行です。PushはCloudflareの既存通知配信経路を使用し、Cronは拡張機能サーバーが稼働している間のベストエフォート実行です。各APIの権限・制限は対応する節を参照してください。

## v59: サーバー保存・文書・読み取り・再生・開発ログ

以下はローカル実装の契約です。本番サーバーの配備と実機の操作確認は別途必要です。SDK 0.59.0を使ってビルドすると、対象APIを使用するプログラムには最低Host API 59が設定されます。全要件の完了を意味しません。

### 本人専用のサーバー保存

`cloud.storage`を宣言します。ユーザー、拡張機能、公開版／プレビュー版ごとに隔離され、別ユーザーを指定する引数はありません。公開版はインストール済みの承認版、プレビュー版は作者本人だけが呼び出せます。データは拡張機能サーバーのSQLiteへ暗号化して保存します。DBの暗号鍵もバックアップ対象です。

| 呼び出し | 戻り値と意味 |
| --- | --- |
| `server.kv.get<T>(key)` | `{value:T|null, revision:number}`。既存Cloud User KVの`server.kv.`名前空間を使用 |
| `server.kv.set(key,value,expectedRevision)` | 新しいrevision。未作成は0を指定 |
| `server.kv.delete(key,expectedRevision)` | 削除したかどうか |
| `server.db.insert<T>(table,id,value)` | `{id,value:T,revision,updatedAt}` |
| `server.db.query<T>(table,options?)` | `{rows:ServerRecord<T>[],nextCursor:string|null}` |
| `server.db.update<T>(table,id,value,expectedRevision)` | 更新後のレコード。部分更新ではなくvalue全体を置換 |
| `server.db.delete(table,id,expectedRevision)` | `{deleted:boolean}` |
| `server.db.transaction(operations)` | 各操作の結果の配列。1つでも失敗すると全操作を取り消す |

DBのvalueはJSONオブジェクトです。テーブル名は英字開始の英数字・アンダースコア64文字以内、IDは英数字開始の英数字・`_.-`128文字以内です。SQL、サーバー上で実行する関数、任意のユーザー指定は受け付けません。DB更新のrevisionはスコープ内で単調増加し、削除したIDを作り直しても古い更新要求は通りません。アンインストール時にデータ削除を選ぶとレコード内容は削除されますが、古い更新の誤適用を防ぐカウンタは保持します。KVは従来のCloud User KVのrevision方式です。競合を捕捉した場合は再取得して利用者に確認し、無条件で再送・上書きしないでください。

検索のoptionsは`{filters?,limit?,cursor?}`です。filterは`{field,operator,value}`の配列で最大16個、全条件のANDです。最上位フィールドに対し`== != > >= < <=`を使用できます。比較で型変換は行わず、大小比較は文字列または数値だけです。IDの昇順で返し、続きはnextCursorをcursorへ渡します。limitは1〜100、既定50。1ページは最大128KiBです。1レコード16KiB、1スコープ合計1000レコード／1MiB、一括操作は1〜32件です。ホスト要求自体のサイズ制限も適用されます。これらはサーバー保護のための制限で、他ユーザーとの共有DBではありません。

```ts
const created = await server.db.insert('notes','lesson-1',{text:'資料を読む'});
const updated = await server.db.update('notes',created.id,{text:'資料を確認した'},created.revision);
const page = await server.db.query('notes',{limit:20});
await server.db.transaction([
  {action:'delete',table:'notes',id:updated.id,expectedRevision:updated.revision},
  {action:'insert',table:'notes',id:'lesson-2',value:{text:'次回の準備'}}
]);
```

### PDFとサムネイル

`pdf.create(name,pages)`は`files.storage`を宣言し、拡張機能専用領域へA4縦PDFを生成します。pagesは1〜20個の`{title?:string,text:string}`です。タイトル120文字、本文4000文字／ページ、本文合計16000文字まで。内容が収まらない場合や256KiBを超える場合はエラーになり、黙って切り捨てません。結果は`{name,size,mimeType,pageCount}`です。

`pdf.export(name,suggestedName?)`は生成済みPDFをOSの保存画面へ渡し、保存したらtrue、キャンセルならfalseを返します。直接操作が必要です。生成PDFのnameは専用ファイル名であり、ファイル選択で返るkeyとは別です。`pdf.open(key)`へnameを渡さないでください。

`pdf.thumbnail(key,name,page=0,maxDimension=512)`と`video.thumbnail(key,name,seconds=0,maxDimension=512)`は選択済みファイルを読み、専用領域へJPEGを生成します。`files.storage`と`files.selection.read`が必要です。PDFのpageは0始まり、動画のsecondsは秒、最大寸法は64〜1024です。結果は`{name,size,mimeType,width,height}`。入力ファイルは最大10MiB、出力は最大256KiBです。既存ファイル名への保存は既存の専用領域ルールに従います。

### QR・バーコード

`camera.scanQRCode()`／`code.scanQR()`と`camera.scanBarcode()`／`code.scanBarcode()`はカメラの読み取り画面を開き、`{value,format}`またはキャンセル時のnullを返します。`camera.use`と直接操作が必要です。カメラがない端末では利用できず、AndroidはGoogle Code Scannerが使用可能な端末が対象です。必要なモジュールの初回取得に通信が必要な場合があります。iOSでは必要時にOSのカメラ許可を求めます。読み取り内容は自動保存・自動送信しません。セッションは最大約2分です。撮影APIやQR生成APIとは別機能です。

### 音声・動画再生

`audio.play(key?)`、`audio.pause()`、`audio.stop()`、`audio.duration()`、`audio.position()`、`video.play(key?)`、`video.pause()`を提供します。`files.selection.read`が必要です。keyは利用者が選択したファイルのキーです。key付きplayはそのファイルを再生し、keyなしplayは既存プレイヤーを再開します。playは直接操作が必要です。動画はネイティブの再生画面を開きます。音声のduration／positionは秒数、再生ファイルがない場合はnullです。停止後の再開にはkeyを再指定してください。端末内ファイルのみ、最大10MiB。URL再生には従来の`ui.audio/video()`を使います。

画面から離れる／アプリがバックグラウンドに入ると一時停止し、拡張機能を閉じると解放します。自動再開、バックグラウンド再生、録音は提供しません。対応するコーデックは各OSのプレイヤーに従います。

### 開発ログ

`console.log(...values)`、`console.warn(...values)`、`console.error(...values)`は本人用プレビューでのみ記録します。標準のグローバルconsoleも使用可能です。公開版でログは送信されません。`debug.inspect(value)`は循環参照、深さ、文字数を制限して文字列化し、パスワード・秘密鍵・トークン等のキーとメールアドレス・学番パターンを伏せます。任意の文章中の個人情報を完全に検出できるものではないため、利用者の情報や認証情報をログへ渡さないでください。

ログはアプリ内の本人用プレビューでのみ確認できます。端末内の一時表示であり、サーバーへ送信・保存する機能はありません。

使用例: `examples/server-records.ts`、`document-tools.ts`、`media-playback.ts`、`developer-logs.ts`。SDK/runtime/backendのテスト、Androidエミュレーター計装テスト、iOS SimulatorとMac Catalystのビルドを実施しています。例のすべてを各OSの実機で操作したことを意味するものではありません。

## v58 の一括追加

検証状況: SDK/runtime/backendテスト、iOS Simulatorビルド、Apple Silicon向けMac Catalystビルドを実施しました。Mac Catalystアプリは起動し初期画面を表示します。拡張機能サーバー本番サーバーの `/health` と `capabilities` をローカル・公開URLの両方から確認し、Host API v80を応答しました。これらは433件すべてのAPIをOS別に個別操作した証明ではありません。カメラ等の機器依存機能やMac Catalystで未確認の機能は、能力応答と該当APIの制約を参照してください。全呼び出し名のSDK提供状況と未監査項目は `EXTENSION_API_INVENTORY.md` に記録します。

- 入力: `ui.textField/searchField/textArea/secureField/toggle/stepper/checkbox/radio/multiSelect/date/time/dateTime/dateRange/filePicker/photoPicker` に状態を接続できます。`field.create(key, initialValue)` は値の変更・リセット・フォーカス操作を持つ入力ハンドルです。
- 画面: `navigation.register({name,title,render})` で登録し、`push/pop/popToRoot/sheet/fullScreen/dismiss/tabs` で切り替えます。ルートの引数はJSON値の辞書で、他の拡張機能やホスト画面への遷移はこの版には含みません。
- 操作: `ui.menu/contextMenu/swipeActions/toolbar/floatingButton`、`ui.alert/confirm/prompt/actionSheet`、`ui.toast/banner` を提供します。ネイティブダイアログは利用者の操作から呼び出します。
- リスト: `list.create(items,{key,render,...})` の返すハンドルに絞り込み・並び替え・検索・選択・複数選択・更新・追加読み込み・移動・削除・操作メニューを設定し、`ui.list/lazyList/sectionedList` で描画します。`onMove` を渡すと未絞り込み・未ソート時に行内の「上へ移動／下へ移動」操作を表示し、`onDelete` を渡すと「削除」を表示します。移動は元データ順に対する隣接移動です。OS標準のドラッグ並べ替え、無制限な一括編集UIはありません。
- ブラウザ: `browser.open(url)` / `webView.open(url)` はアプリ内で、`browser.openExternal(url)` は外部でHTTPSページを開きます。`browser.open` 権限と直接操作が必要です。ホストのポータルCookieは拡張機能へ渡しません。
- 画像加工: `image.resize/crop/rotate/flip/compress/convert/thumbnail/removeMetadata` は端末内で処理します。入力は `{key: selectedFile.key}` または `{bytes: Uint8Array}`、出力先は拡張機能専用ファイル名です。`files.storage` が必要で、選択ファイルを読む場合は `files.selection.read` も必要です。`crop` の座標は0〜1、回転は90度単位です。JPEG/PNG、最大2048px、出力256KiB以内です。メタデータ削除は画素の再エンコードで行います。
- 描画: `canvas.create(width,height,label)` の返すハンドルに `line/rect/circle/text/image/path` を追加し、`node()` を画面ツリーへ渡します。論理座標から表示幅へ拡大縮小します。色は `primary/secondary/accent/surface` または `#RRGGBB`、画像はHTTPSと `media.remote` 権限が必要です。任意のネイティブ描画コードは実行しません。
- グラフ: `chart.line/area/bar(label,categories,series)`、`chart.pie/donut(label,categories,values)`、`chart.scatter(label,series)` を提供します。各系列の名前と数値もテキストで表示します。円グラフは非負値で合計が正である必要があります。
- 写真: `photos.pick/pickMultiple` は利用者が選択した画像だけを返します。`files.user` と `files.selection.read` が必要です。`photos.info(key)` は画像の寸法を、`share.image(key)` はOS共有画面を提供します。写真ライブラリの全列挙や自動送信はしません。
- メディア: `video.info(key)` は寸法・秒数、`pdf.pageCount(key)` はページ数を返します。`pdf.open(key)` はネイティブのPDFプレビューを開き、`pdf.share(key)` はOS共有画面を開きます。メタデータ取得には `files.selection.read`、表示・共有にはさらに `files.user` と直接操作が必要です。
- カメラ: `camera.available()` は搭載状況、`camera.permissions()` は `camera.use` の許可状態を返します。この2つは撮影を開始せず、`camera.capturePhoto/captureVideo` の実装完了を意味しません。

使用例はSDK内の `examples/runtime-foundation.ts` と `examples/graphics-and-media.ts` です。既存のファイル容量・描画複雑度・外部通信先の制限と、ユーザー×拡張機能単位の隔離は維持します。

## TypeScriptの実行時処理

SDKのホスト要求は失敗理由ごとに `PermissionError`、`UserInteractionError`、`UnsupportedError`、`NetworkError`、`ConflictError`、`NotFoundError`、`RateLimitError`、`StorageError`、`ValidationError` を返します。未知のホストエラーは `ExtensionError`（`code === 'HOST_ERROR'`）として扱います。エラーを捕捉しない場合も、エラー型のcodeをQuickJSからiOS/Androidホストへ伝え、通信・権限・競合・保存・入力等に応じた説明を表示します。UIやデータ更新では、競合を自動で上書きせず最新データを読み直してください。

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
- host API v33で `network.uploadFile()` / `network.downloadFile()` を追加しました。host API v55では両APIに `onProgress` を追加し、転送方向・処理済みbyte数・総byte数（不明ならnull）を受け取れます。更新はおよそ100ms間隔で間引かれ、完了時にも通知されます。
- host API v34で `interactive.multiSelect(key, label, options, state)` を追加しました。選択変更は文字列配列としてTypeScriptのStateへ反映されます。選択肢は1〜30件、各120文字以内で重複不可です。
- host API v35で `interactive.dateTime(key, label, state.create<number | null>(null))` を追加しました。OS標準の日付・時刻選択UIの値はUnix epochミリ秒のnumberとしてStateへ反映され、未設定はnullです。SDKが `minimumHostAPI: 35` を設定します。例は `extensions/examples/interactive-date-time.ts` です。
- host API v42で `ui.barChart(title, categories, series)` を追加しました。1〜12カテゴリ、1〜4系列、各値0〜1,000,000の検証済みデータをiOS・macOS・Androidのネイティブ横棒表示で描画します。自由なCanvas描画やスクリプト描画は行いません。各系列の値数はカテゴリ数と一致させ、SDKビルダーは `minimumHostAPI: 42` を設定します。
- `cit.bus.*`: `bus.read` が必要です。端末に保存された公開バス時刻表のみを読みます。時刻表データを取得するには、アプリで一度バスダイヤを表示して取得してください。
- `app.version/extensionId/environment/hostVersion/capabilities`: 同期的に実行環境を確認します。`app.version()`は拡張機能の公開版、`app.hostVersion()`はCIT Hub拡張機能Host APIの整数バージョンを文字列で返し、アプリのリリース番号とは異なります。
- `capabilities.has(name)`: 同期的に対応を確認します。未実装APIはfalseです。OS権限の許可とは別です。
- `permissions.openSettings()`: `settings.open`を宣言した拡張機能が、画面上のボタン操作中に呼ぶとCIT HubのOSアプリ設定を開きます。拒否済み権限を利用者自身が設定から変更するための導線です。拡張機能が設定値を書き換えるAPIではありません。

授業データなどが未取得の場合はnullになることがあります。秘密情報・他ユーザー・他拡張機能のデータは要求できません。単一要求10秒、要求内容60KBまでです。

`app.onLaunch/onAppear/onDisappear/onResume/onSuspend/onOpenURL/onNotification/onMemoryWarning` はコールバック登録と購読解除に対応しています。URLイベントは拡張機能画面が表示されている間に限って届けます。ローカル通知タップは対象の追加済み拡張機能を開き、ランタイム準備完了後に通知イベントを一度届けます。payloadは通知ID・操作・dataなど許可済み文字列だけに制限します。メモリ警告もOSが通知した場合だけです。終了時・中断時の非同期処理完了やバックグラウンド実行は保証しません。タイマーは同時32件、10ms以上、最長24時間です。OSによる遅延や停止があります。`utils.sleep/debounce/throttle` は実行時にも使用できます。日時の書式化は隔離された文字列専用ブリッジを通じてホストのIntlを利用します。

`text.regex(pattern, flags)` は最大256文字のパターンと重複のない正規表現フラグを検証して `RegExp` を返します。パターン実行はQuickJSの処理時間制限内に限られ、制限超過時は拡張機能の実行が中断されます。例: `text.regex('^(CIT Hub)\\s+(\\d+\\.\\d+)$', 'i').exec(value)`。

`utils.uuid()` は暗号学的に安全な乱数からUUID v4を生成します。安全な乱数を利用できない実行環境では、予測可能な値にフォールバックせずエラーにします。

`settings.open`はAPI v14です。OS設定を勝手に開かないよう、利用宣言に加えて画面上の直接操作が必要です。アプリ設定から先のページやスイッチはOSごとに異なり、拡張機能から指定・変更できません。

## 要求元端末へのリモート通知要求（API v78）

`notifications.schedule`権限と公開・追加済み拡張機能が必要です。ボタンなどのユーザー操作から `push.sendToCurrentUser({title,body,data})` を呼ぶと、現行実装はリクエストに含まれる登録端末1台にだけ即時通知を要求します。端末間配信や任意のユーザーID指定はしません。共有アプリトークンとクライアント指定IDは本人性の証明ではなく、端末登録のアカウント紐付けにも強い暗号学的本人確認はありません。このためアカウント認証済みの通知として扱わず、通知本文に機微情報を含めないでください。サーバー本人確認が実装されるまで、アカウント全端末への配信は無効です。Apple端末はAPNsトークンの登録が必要です。Mac Catalystにもホスト経路がありますが、APNsトークンが必要で、Apple実配信は未検証です。FCMを含むプロバイダー実配信もE2E未確認です。1拡張機能・1申告アカウントあたり3回/分、20回/日のサーバー制限があり、dataは10項目以内、各300文字以内、JSON全体800バイト以内です。開発プレビューは未対応です。端末登録、通知許可、Apple/FCM配信状態により失敗することがあり、受信・表示時刻は保証されません。Cloudflare Worker経由の移行中の配信で、ローカル通知とは別の仕組みです。

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
| `cit.cafeteria.hours()` | cafeteria.hours | cafeteria.read | 公式掲載の通常期・休暇期の標準営業時間 |
| `cit.campus.facilities()` / `buildings()` / `rooms()` | courses.data | timetable.read | 利用者自身の時間割に現れる教室のみ |
| `cit.campus.room(id)` / `search(query)` | courses.data | timetable.read | 利用者自身の時間割に現れる教室の検索 |
| `cit.campus.roomAvailability(id, at?)` | courses.data | timetable.read | 本人の時間割上で授業中か。大学全体の空室情報ではない |
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

`network.fetch/get/post/put/patch/delete`を提供します。GET/POST/PUT/PATCH/DELETEとテキスト本文、最大20個のヘッダーに対応します。Cookie・Host・接続制御系ヘッダーは禁止し、HTTPS標準ポートのみ、リダイレクトは追従せず、許可済みホストのDNSがプライベート/予約済みIPを返す場合も拒否します。Androidは検査したDNS応答を接続時にも使い、別の名前解決による接続先の差し替えを防ぎます。iOS/iPadOS/macOSは接続前にDNSを検査しますが、接続時の名前解決はOSのURLSessionが行うため、Androidと同じDNS固定の保証はありません。両方の実装でシステムプロキシは使いません。リクエスト本文は32 KiB、応答本文は256 KiB、通信待ち時間は最大20秒です。レスポンスはstatus/ok/許可した応答ヘッダー/body(UTF-8文字列)です。

host API v33のファイル転送は次のとおりです。

- `network.downloadFile(url, destinationName, options?)` は宣言済みHTTPSホストから最大256 KiBを取得し、拡張機能専用ストレージへ保存します。戻り値はHTTP状態と `{name,size}` です。内容は `files.read(name)` で読み取れます。既存の `headers` オブジェクト形式を維持しつつ、`{headers,onProgress}` も指定できます。
- `network.uploadFile(url, sourceName, {method?,headers?,onProgress?})` は専用ストレージ内の最大256 KiBのファイルをPOSTまたはPUTします。進捗コールバックは `{direction,loadedBytes,totalBytes,fraction}` を受け取り、総量が不明なとき`totalBytes`と`fraction`はnullです。端末の任意パスやユーザーの未選択ファイルは読みません。
- APIはユーザーが明示的に押したボタン処理からのみ実行できます。両APIには `network.fetch` と `files.storage` が必要で、許可ホストはmanifestの完全一致宣言に限られます。転送先には端末のIPが伝わり、アップロードしたファイル内容も渡るため、公開前審査の対象です。
- host API v55で両転送に`onProgress`を追加しました。約100ms間隔で通知し、完了・失敗時にコールバックを解除します。対応アプリは`network.transferProgress` capabilityを公開します。SDKビルダーは利用を検出して`minimumHostAPI: 55`を設定し、配布サーバーも宣言と実装の不一致を拒否します。転送キャンセルやバックグラウンド継続機能ではありません。

## 拡張機能サーバーAPI

`POST /api` にJSONを送信します。開発者サイトはBearerセッション、アプリは既存のアカウント管理で確認される端末情報を使用します。開発者から学番を指定して別ユーザーの情報を読めるAPIはありません。

- `register`: アプリからの本人端末確認、username・passwordによる登録。
- `login` / `logout`: 開発者ログイン・現在のセッション失効。
- `logout-all`: 自分の全開発者セッションを失効。
- `password-change`: current_password・new_passwordで変更。旧セッションを全失効。
- `capabilities`: 認証不要の固定メタデータ。対応ホストAPIバージョン・権限・読み取り要求に加えて、主要なネイティブAPIの`available`/`partial`/`unavailable`状態と対応プラットフォームを返します。個人データは含みません。SDK宣言があるだけで実機接続済みとは限らないため、`native_api_status`も確認してください。
- `profile-save` / `mine`: 公開プロフィール・自分の開発版。
- `support-list` / `support-send`: 開発者本人の運営チャット。メッセージはアカウントごとに分離されます。
- `upload` / `submit`: 開発版保存・審査申請。
- `catalog` / `developer`: 承認済み版・公開作者ページ。
- `install` / `open` / `uninstall`: 追加・起動・削除。v59ホストはhost_api_version=59を送信し、使用APIに応じた最低互換性を確認します。
- `server-data`: 本人専用DBの検索・追加・更新・削除・一括処理。Host API v59とcloud.storageが必要。
- `support-inbox` / `support-thread` / `support-reply`: 運営管理キー専用の開発者チャット管理API。
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

`background.register/unregister`は端末ローカルのOSタスクではなく、`server.functions`権限で拡張機能サーバーの承認済みサーバー関数をスケジュールします。IDは同名の`serverFunctions`関数に対応し、間隔は15分〜30日です。サーバー停止中やネットワーク断では実行されず、時刻・頻度は保証されません。`chargingRequired: true`および`networkRequired: false`は実行環境で保証できないため拒否します。`search.registerItems/removeItems` は端末内アカウント別索引を登録し、公開済み現行版のみ検索対象にします。`widget.define/reload` はCIT Hub内ウィジェットを登録・更新し、宣言済み権限で許可された本人データを表示します。定義は端末内保存で値は保存しません。1拡張10個・1アカウント50個・128 KiBまで、プレビューは禁止です。OSホーム画面ウィジェットではなく、他端末へ同期しません。`host.registerShortcut` は`host.navigate`権限を必要とし、CIT Hubの拡張機能画面に端末内・アカウント別の永続タイルを登録します。タップ時は`app.onOpenURL`へ`source: "hostShortcut"`、ID、任意routeを渡します。1拡張8件・1アカウント50件・64 KiBまで、公開版のみ利用でき、追加済み現行版だけを表示し、アンインストール時に削除します。`shortcuts.register`は`host.registerShortcut`と同じCIT Hub内の永続タイルを登録し、OSホーム画面やSiriショートカットは作成しません。検索・ウィジェット・アプリ内ショートカットはiOS/Androidにコード接続済みですが、実機およびMac Catalyst動作は未検証です。`host.registerAction` は拡張機能実行画面内のアプリ内遷移ボタンとして接続済み・実機検証保留です。routeをSDKで必須化し、許可されたページまたは学内サービス名のみを受け付けます。端末内の任意バックグラウンド実行は提供しません。定期実行は承認済みサーバー関数に限り、サーバー稼働中のベストエフォートです。`push.sendToCurrentUser`はiOS/iPadOS/Android/Mac Catalystのホスト経路に接続済みですが、Apple実配信は未検証です。現行の本人確認はアカウント所有者の証明にならないため、要求元端末1台への配信に限定し、Mac CatalystではAPNsトークン登録を要求します。OIDCは`auth.openIDConnect`でissuer discovery、HTTPSのJWKS取得、RS256署名、issuer/audience/authorized-party/nonce/iat/exp検証を行います。プロバイダー別token revocation、ID tokenの永続保存、認証情報の端末間同期は行いません。信頼できる学内データソースのない空き教室情報は生成しません。認証情報、OTP、Cookie、本人に共有されていない他ユーザー情報、任意ネイティブコード、無制限バックグラウンド実行は提供しません。

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

SDK 0.51.0では、上記と同じデータをより一貫した `cit.calendar` 名前空間からも利用できます。`events()` は学年暦全体、`holidays(start, end)` は休日・休暇の候補、`classDays(start, end)` は本人の時間割を含めて根拠が確認できた授業日の日付一覧、`isClassDay(date)` は単日の判定、`weekNumber(date)` はISO週番号を返します。`holidays` は期間を指定する場合、開始日と終了日の両方を指定してください。`classDays` は366日以内で、`calendar.read` と `timetable.read` の両方を必要とします。旧 `cit.academicCalendar` APIも引き続き利用できます。

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

実行型TypeScriptでは `files.pick()`、`files.pickImage()`、`files.pickVideo()`、`files.pickAudio()`、`files.pickDocument()` を利用者が押したボタンから呼び出せます。OS標準ピッカーで一度に1ファイルを選ぶ `files.pick()` は `{key,name,size,mimeType}`、キャンセルは `null` です（Host API v49）。`files.pickMultiple(kind?)` は最大10ファイルの配列（キャンセル時 `null`）を返します（Host API v50）。両方とも `files.user` と `files.selection.read` が必要で、各ファイル10 MiB、複数選択合計20 MiBまで拡張機能専用の端末内領域に保持します。`files.readSelected(key,offset,maxBytes)` で最大48 KiBずつ読み取れます。`files.export(name,suggestedName?)` と同義の `files.saveAs(name,suggestedName?)` は拡張機能専用領域のファイルをOS標準の保存UIへ渡し、利用者が保存先を決めます（Host API v51、`files.storage` と利用者操作が必要）。任意ファイル一覧やバックグラウンドでのファイル操作には対応しません。

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
- Host API v57の `browser.openExternal(url)` は、実行型拡張機能から端末の既定ブラウザを開きます。`browser.open`権限とボタン操作が必要です。HTTPS・標準ポートのみ許可し、URL長は2048文字までです。ユーザー名・パスワードを含むURLは拒否します。ブラウザのDOM、Cookie、ログイン状態は取得できません。ビルダーは利用を検出して `minimumHostAPI: 56` を設定します。
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

未提供または制約付き: 無音撮影、生画像データの直接取得、任意ネイティブコード、端末ローカルでの任意バックグラウンド実行、OSホーム画面へのウィジェット/ショートカット、OS標準のリスト行ドラッグ並べ替え／無制限な一括編集。画面間ナビゲーションとサーバーFunctionsは個別APIとして接続済みです。通知予約、位置情報、ToDo CRUD、授業メモ書き込み、制限付きWSSも利用できます（各節のHost APIバージョン・権限・OS制約に従います）。

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

`get` は `{value, revision}` を返し、未保存値は `{value:null, revision:0}` です。`set` は取得したrevisionを渡し、成功時に新しいrevisionを返します。revisionが古いと `data_conflict` になり、暗黙に上書きしません。`delete` もrevisionを必要とし、存在しない場合はfalseを返します。キーは最大100文字、値はJSONで最大16 KiB、拡張機能・ユーザー・環境ごとに128キー/128 KiBまでです。このKV API自体はアカウント間共有・グループ同期・Realtime・Push・Functions/Cronを行いません。それらは別APIとして、それぞれの権限と制約のもとで提供します。アプリ本体の資格情報やOTPはこのAPIで扱わないでください。

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

SDKのビルド済み実行型拡張機能は利用APIに応じた `minimumHostAPI` を含みます。転送進捗コールバックはv55、外部ブラウザ起動はv56です。通常の実行型拡張機能はv9、授業メモ・外部通信はv10、拡張機能ファイル保存はv11、選択ファイル読み取りはv12、クリップボードはv13、OS設定への遷移はv14、ローカル通知はv15、構造化時間割・課題はv16、ToDo完了操作はv17、学年暦読み取りはv18、バスv19、食堂v20、本人専用Cloud KV v21、文字UI v22、複数行入力v23、位置情報v24、接続状態v25、レイアウトv26、文字スタイルv27、ネットワーク変更購読v28、独立スクロールv29、ファイル複製・移動v30、本人の有効サービス一覧v31、状態同期する選択UI v32、専用領域とのファイル転送v33、状態同期する複数選択v34、日時選択v35、開始・終了日時の範囲入力v36、State同期するStepper・Checkbox・Radio v37、ネイティブ進捗表示v38、読み込み・空・エラー状態v39、カード・グループ・レスポンシブコンテナv40、制限付きネイティブアニメーションv41、検証済みネイティブ横棒グラフv42、Safe Areaとレスポンシブ寸法v43、入力フォーカス・単一行送信イベントv44、アクセッシブルなネイティブアイコン操作v45、状態同期するネイティブ検索欄v46、HTTPSのネイティブリンクボタンv47、セキュアWebSocket v48、単一ファイルピッカー v49、複数ファイルピッカー v50、OS標準の保存画面への書き出し v51、OS標準の共有シート v52、利用者が選択したファイルの共有 v53、選択済み画像の端末内縮小・JPEG変換 v54、既定ブラウザ起動v56です。配布サーバーは追加・起動時にアプリの対応APIを比較し、足りなければ `host_update_required` を返します。

`minimumHostAPI` は整数1〜80です。アプリ自身のバージョン番号とAPI番号は別です。対応していないアプリでAPIを呼び出してから失敗する状態を避けるため、新しいSDKは使用APIを検出してこの指定を自動で入れます。Host API v68は`background.status()`による読み取り専用のOS状態確認です。Host API v70は招待制グループRealtimeを追加します。`background.register/unregister`はHost API v71以降、`server.functions`権限でサーバー関数の定期実行を登録・解除します。端末側のバックグラウンド処理ではありません。

## 権限状態の確認

`await permissions.status(permission)` は拡張機能の宣言とOSの状態を区別します。未宣言は `undeclared`、カメラ・位置情報・通知などのOS権限は未要求なら `prompt`、要求後に未許可なら `denied`、許可済みなら `granted` です。写真・ファイルは利用者が選択したものだけ扱うため `user-selection` を返します。statusはOSの許可ダイアログを表示しません。

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

## ネイティブリンクボタン（Host API v47）

`ui.linkButton(label, httpsURL)`は、HTTPS URLをOSのリンク処理に渡す、アクセシブルな強調ボタンです。リンク先はユーザー情報を含まないHTTPS URLに限定し、最大2,000文字です。iOS・iPadOS・macOSではSwiftUIのネイティブLink、AndroidではMaterialボタンからシステムのリンクハンドラーを開きます。ラベルは必須で120文字以内。SDKビルダーは静的UI・実行型UIの両方を検出して`minimumHostAPI: 47`を設定し、Host API v46以前は起動時に互換性エラーになります。

`ui.linkButton('大学公式サイトを開く', 'https://www.it-chiba.ac.jp/')`

## ネイティブ検索欄（Host API v46）

`interactive.searchField(key, label, value, events?)` はiOS/macOSとAndroidのネイティブ入力を検索用に構成します。値は `State<string>` に同期され、4000文字までです。検索アイコン、入力を消す操作、検索用キーボードアクションを備え、任意で `onFocus`・`onBlur`・`onSubmit` を指定できます。`onSubmit` は検索キー／検索アクションでのみ呼び出されます。SDKビルダーはこのAPIを検出して `minimumHostAPI: 46` を設定し、Host API v45以前での実行を拒否します。

```ts
const query = state.create('');
interactive.searchField('query', '課題名を入力', query, {
  onSubmit: () => searchAssignments(query.value),
});
```

サンプル: `extensions/examples/interactive-search-field.ts`。

## セキュアWebSocket（Host API v48）

`network.websocket.connect(url)`は`network.fetch`権限とmanifestの完全一致ホスト宣言を使い、標準ポート443の`wss://`にのみ接続します。Cookieや端末内Webセッションは共有せず、接続先のDNSがプライベート／予約済みアドレスを指す場合は拒否します。Androidは検査済みDNS応答を接続にも固定します。iOS/iPadOS/macOSは接続前検査で、接続時のDNS固定は行いません。1拡張機能につき同時接続は1つ、テキストメッセージは送受信各4 KiB、送受信各30フレーム/分です。バイナリフレーム、任意ポート、リダイレクト、バックグラウンド接続は未対応で、中断・画面離脱時に切断します。

```ts
const socket = await network.websocket.connect('wss://stream.example.net/events');
socket.onMessage(text => state.set(text));
socket.onClose(event => status.set(`切断: ${event.code}`));
socket.onError(code => status.set(`通信エラー: ${code}`));
await socket.ready;
await socket.send('subscribe');
await socket.close();
```

接続は利用者の画面操作から開始してください。接続先には端末のIP等が伝わります。SDKビルダーは`network.websocket.connect()`を検出し、最低Host API v48を設定します。実装例: `extensions/examples/runtime-websocket.ts`。

## OS標準の保存画面への書き出し（Host API v51）

`files.export(name, suggestedName?)` と同義の `files.saveAs(name, suggestedName?)` は、拡張機能の専用保存領域にあるファイルをOS標準の保存画面へ渡します。iOS・iPadOS・macOSはシステム書き出しUI、AndroidはCreate Document UIを表示します。利用者が保存先を選び、保存完了で `true`、キャンセルで `false` を返します。`files.storage` 権限と利用者の直接操作が必要で、対象は専用領域内の通常ファイルに限り、最大256 KiBです。任意の端末パスを指定したり、利用者に代わって保存先へ書き込むことはできません。

```ts
const bytes = new TextEncoder().encode('CIT Hub extension export');
await files.write('notes.txt', bytes);
const saved = await files.saveAs('notes.txt', '授業メモ.txt');
```

OSの保存画面は直接のタップ中に開く必要があります。保存対象ファイルの作成・更新と書き出しは別々のボタンに分けてください。先にファイルを作り、その完了後に利用者が書き出しボタンを押す構成は `extensions/examples/runtime-file-export.ts` を参照してください。

SDKビルダーと配信サーバーはこのAPIの使用を検出し、`minimumHostAPI: 51` を要求します。実装例: `extensions/examples/runtime-file-export.ts`。


## OS標準の共有シート（Host API v52）

実行型TypeScriptでは `share.text(text)`、`share.url(url)`、`share.items(items)` を利用者が押したボタンから呼び出せます。iOS・iPadOS・macOSではシステム共有シート、Androidでは共有先選択画面を表示します。テキストとHTTPS URLを1〜10項目、テキスト1項目8KiB以下、合計16KiB以下で共有します。URLはHTTPS、認証情報なし、ポートなしまたは443に限ります。直接の利用者操作が必要です。共有画面を開けたことは返しますが、送信完了までは保証されません。

```ts
import { interactive, share, state, ui } from '../sdk/index';
const status = state.create('共有先を選べます');
interactive.button('リンクを共有', async () => {
  await share.items([
    { text: 'CIT Hub' },
    { url: 'https://cit-hub.vercel.app/' }
  ]);
  status.set('共有画面を開きました');
});
ui.text(status.value);
```

## 選択済みファイルの共有（Host API v53）

`share.file(key)` は `files.pick()` または `files.pickMultiple()` で利用者が明示的に選択したファイルをOS標準の共有画面へ渡します。`files.user` と `files.selection.read` の両権限、および直接の利用者操作が必要です。任意の端末パスは受け付けず、1ファイル10MiB・共有合計20MiBまでです。CIT Hubのサーバーへはアップロードしません。共有先がファイルを読み取れる範囲はOSの共有機構に従います。

```ts
import { defineProgram, files, interactive, share, state, ui } from '../sdk/index';
const status = state.create('画像を選んでください');
const key = state.create('');
export default defineProgram({
  name: '選択画像を共有', version: '1.0.0', description: '選択した画像を共有します。',
  permissions: ['files.user', 'files.selection.read'],
  render: () => [
    ui.text(status.value),
    interactive.button('画像を選ぶ', async () => {
      const selected = await files.pickImage();
      if (!selected) return;
      key.set(selected.key);
      status.set('選択しました。共有ボタンを押してください。');
    }),
    interactive.button('共有する', async () => {
      if (!key.value) return;
      await share.file(key.value);
    })
  ]
});
```

## 選択済み画像の縮小・JPEG変換（Host API v54）

`files.transformImage(key, outputName, options?)` は `files.pickImage()` などで利用者が選択した画像を端末上で縮小し、JPEGとして拡張機能専用ストレージへ保存します。`files.selection.read` と `files.storage` が必要で、直接操作中にのみ呼び出せます。画像は10MiB以下、最大辺256〜2048px、品質0.35〜0.9、出力256KiB以下です。EXIF方向を補正します。元画像は変更せず、通信は発生しません。GIF等の複数フレーム画像は先頭フレームを変換します。戻り値は名前・MIME・バイト数・変換後寸法を含みます。

```ts
import { defineProgram, files, interactive, state, ui } from '../sdk/index';
const status = state.create('画像を選択してください');
const selectedKey = state.create('');
export default defineProgram({
  name: '画像を軽量化', version: '1.0.0', description: '選択画像を軽量なJPEGとして保存します。',
  permissions: ['files.user', 'files.selection.read', 'files.storage'],
  render: () => [
    ui.text(status.value),
    interactive.button('画像を選ぶ', async () => {
      const image = await files.pickImage();
      if (!image) return;
      selectedKey.set(image.key);
      status.set(`${image.name}を選択しました。`);
    }),
    interactive.button('縮小して保存', async () => {
      if (!selectedKey.value) return;
      const result = await files.transformImage(selectedKey.value, 'optimized.jpg', { maxDimension: 1600, quality: 0.82 });
      status.set(`${result.width}×${result.height} / ${result.size} bytes`);
    })
  ]
});
```

例: `extensions/examples/runtime-image-transform.ts`。


## 匿名イベント集計（Host API v57）

`analytics.collect`をmanifestに宣言し、利用者が押したボタンの処理から`await analytics.event('task.open')`のように記録します。イベント名は`^[a-z][a-z0-9_.-]{0,47}$`に一致する48文字以内の固定名です。任意データ、ユーザーID、端末ID、時刻の細粒度情報は受け付けません。サーバーには拡張機能ID・バージョンID・UTC日付・イベント名・件数のみを保存し、90日を超えた集計を削除します。拡張機能ごとに1日100イベント名、各イベント最大1000件までです。審査済みかつインストール済みの公開版のみ計測でき、作者プレビュー・未承認版は拒否されます。開発者コンソールでは直近30日の日別集計を表示します。個人や特定端末の行動は追跡できません。

```ts
import { analytics, defineProgram, interactive, ui } from './sdk';
export default defineProgram({
  name: '利用状況の例', version: '1.0.0', description: '集計イベントの例',
  permissions: ['analytics.collect'],
  render: () => [interactive.button('詳細を開く', async () => {
    await analytics.event('detail.open');
  })]
});
```

# Host API v66: 拡張機能内のテキストドラッグ＆ドロップ

`drag.source(label, text)`と`drop.target(key, label)`で、同じ拡張機能画面内に限り、拡張機能が明示したプレーンテキストをOS標準のドラッグ操作で渡せます。受け取った値は拡張機能の画面状態`values[key]`に入り、`{{key}}`で表示できます。追加権限は不要です。テキストは4,000文字以内、ラベルは120文字以内です。ファイル、他拡張機能の値、端末内データ、アプリ間ドラッグは対象外です。Host API v66以降が必要です。例は`extensions/examples/native-drag-drop.ts`です。

# Host API v67: 地図

`map.view(options, overlays)` は、指定座標を中心とする操作可能な地図を表示します。`map.marker(location, options)`、`map.polyline(points, options)`、`map.circle(center, radiusMeters, options)` で地図注釈を追加し、`map.fitBounds(points, padding)` で表示範囲を合わせます。緯度・経度や点数、拡大率、表示高さ、線幅、円半径はSDK・ビルダー・サーバーで検証されます。位置情報は自動取得せず、現在地表示もしません。`map.openDirections` は別途 `browser.open` 権限を必要とします。

iOS/iPadOS/macOSはMapKitを使います。AndroidはOSMタイルを使用し、地図内にOpenStreetMap帰属を常時表示します。Androidでは表示範囲内のタイルだけを通常のHTTPキャッシュ規則で取得し、事前一括取得・オフライン地図・タイル保存の明示的な制御は提供しません。OSM公開タイルはベストエフォートで、可用性保証はありません。 [OpenStreetMap tile usage policy](https://operations.osmfoundation.org/policies/tiles/)

```ts
import { defineProgram, map } from './sdk';
export default defineProgram({
  name: 'キャンパスマップ', version: '1.0.0', description: '注釈付き地図',
  permissions: [],
  render: () => [map.view({center:{latitude:35.6812,longitude:139.7671},zoom:14,height:320}, [
    map.marker({latitude:35.6812,longitude:139.7671},{title:'駅'}),
    map.polyline([{latitude:35.6812,longitude:139.7671},{latitude:35.6840,longitude:139.7730}],{tone:'accent',width:5}),
    map.circle({latitude:35.6812,longitude:139.7671},500,{tone:'secondary'}),
    map.fitBounds([{latitude:35.6790,longitude:139.7650},{latitude:35.6860,longitude:139.7750}],32)
  ])]
});
```

# Host API v68: バックグラウンド更新状態

`background.status()`はOSが報告する現在のバックグラウンド更新状態を読み取り専用で返します。返却値は`{availability, mayRun, platform, note}`です。iOS/iPadOSは`UIApplication.backgroundRefreshStatus`、Android API 28以降はOSのバックグラウンド制限状態を使用し、古いAndroidや判定不能時は`unknown`を返します。Mac Catalystは`unsupported`です。`available`は設定上許可されている意味に限られ、実行時刻・頻度・実行成功は保証されません。このAPIは端末OSの状態確認専用です。サーバー定期実行APIの`background.register/unregister`とは別機能です。SDKビルダーは利用時に`minimumHostAPI: 68`を設定し、サーバーは宣言漏れを拒否します。

```ts
import { background } from './sdk';
const status = await background.status();
console.log(status.availability, status.mayRun);
```

# Host API v69: サーバー関数

`server.function(name, input)`はビルド時に同梱された`serverFunctions`だけを拡張機能サーバー上のQuickJS隔離コンテキストで実行します。関数にはJSON入力と拡張機能ID・環境・呼出時刻のみを渡し、Node.js、ファイル、任意ネットワーク、CIT Hubアカウント識別子は公開しません。入力16KiB、結果32KiB、コード150KiB、実行100ms、ユーザー・拡張機能ごとに30回/15分です。`server.functions`権限が必要です。

```ts
import { defineProgram, server, ui } from "../sdk/index";
export const serverFunctions = { add: async (input: { a:number; b:number }) => input.a + input.b };
export default defineProgram({ name:"Server function example", version:"1.0.0", description:"QuickJS isolated function", permissions:["server.functions"], render:()=>[ui.button("Run",async()=>{ const result=await server.function<number>("add",{a:2,b:3}); })] });
```

# Host API v70: グループRealtime

`realtime.publish(groupID, topic, value)`は招待制グループ内のトピックにJSONイベントを送ります。`realtime.subscribe(groupID, topic, callback, options)`は最後に保存されたイベントを定期取得します。低遅延WebSocketではなく、既定2.5秒（指定1〜30秒）のHTTPSポーリングです。サーバーが連番を採番し、同時投稿で順序が壊れないようにします。現在値のみ保持するため履歴キューではありません。`cloud.shared`権限とグループ参加が必要です。topicは拡張機能・環境・グループごとに分離され、ペイロードは12KiB以下です。購読解除関数を呼ぶと次のpollを停止します。

```ts
import { realtime } from './sdk';
const unsubscribe = realtime.subscribe(groupID, 'room-state', message => render(message.value));
await realtime.publish(groupID, 'room-state', { open: true });
// 画面を閉じる時
unsubscribe();
```

# Host API v71: 承認済み関数の定期実行

`background.register({id, intervalMinutes})` と `background.unregister(id)` は端末OSではなく、認証済み拡張機能サーバー上の `server.cron` に接続します。`id` は同じ拡張機能で提出・承認された `serverFunctions` の関数名と一致させます。実行は15分〜30日間隔、1ユーザー・拡張機能あたり最大5ジョブです。サーバーがオンラインの間にベストエフォートで実行し、時刻の正確性、停止中の実行やキャッチアップは保証しません。`server.functions` 権限が必要です。端末の充電状態を検査できないため `chargingRequired: true`、サーバー自体のオンラインが前提のため `networkRequired: false` は拒否します。端末OSの可否を返す `background.status()` とは独立しています。

```ts
import { background } from '../sdk';

// Server function `sync` must be included in the approved manifest.
await background.register({ id: 'sync', intervalMinutes: 60 });
// Later:
await background.unregister('sync');
```

未登録IDの解除は `cron_not_found` になります。開発版と公開版のジョブは環境別に分離されます。

# Host API v72: サーバー定期実行結果

`server.cron.register(name, input, intervalSeconds)` / `unregister(name)`（Host API v71）と `status(name)`（v72）は、承認済み・インストール済み拡張機能の`serverFunctions`を拡張機能サーバー上で定期実行・確認します。`server.functions`権限が必要です。間隔は900秒〜30日、1拡張機能あたり有効登録5件までです。実行はサーバー稼働中に15秒ごとにdue jobを確認し、処理開始後に次回を予約します。サーバー停止中の実行は保証されず、再開後に過去分を一括実行しません。QuickJSの制限（実行100ms、入力16KiB、出力32KiB）を適用します。最新の成功結果はAES-GCMで暗号化してアカウント・拡張機能・環境別に保存し、`status`から状態・時刻・結果・直近エラーコードを取得できます。失敗時も直近の成功結果は保持されます。連続3回失敗すると停止し、再登録で再開します。拡張機能をアンインストールまたは更新して対象versionが無効になるとjobは実行されず、次回scheduler掃除で削除されます。個人ユーザーデータや端末資格情報は実行関数に渡しません。

## Extension SDK OAuth 2.0 (PKCE)

`auth.oauth2(provider)` は公開クライアント向け Authorization Code + PKCE (S256) を使用します。authorization/token endpointはHTTPS必須、redirect URIは登録済みの `cithub://extensions/oauth` に限定、`state` を完全一致で検証します。ブラウザを開くには `browser.open`、token endpointへ通信するには `network.fetch` と該当ホストのmanifest allowlistが必要です。ユーザーの明示操作から起動してください。access/refresh tokenは端末の暗号化secure storageに拡張機能・ユーザー単位で保存し、`tokenStatus()`は秘密値を返しません。`refresh()`は保存済みrefresh tokenを使い、`logout()`は端末の資格情報を削除します。公開クライアントのためclient secretは受け付けません。認可中にアプリプロセスがOSに終了された場合、未完了フローは中断され再試行が必要です。

`auth.openIDConnect({issuer, clientId, scopes})` はissuerのdiscovery metadataを取得し、Authorization Code + PKCEで認証します。ID tokenはHTTPSで取得したJWKSから`kid`一致のRSA署名鍵を選び、RS256署名とissuer、audience、複数audience時の`azp`、subject、nonce、iat、expを共有ランタイムで検証します。成功後は最小の`{subject, issuer}`だけを返し、ID tokenは永続化しません。検証失敗時はaccess/refresh tokenも保存しません。認証endpoint・token endpoint・JWKS hostすべてをmanifestのHTTPS allowlistに含めてください。署名鍵のrotation時はprovider JWKSを再取得します。プロバイダー別revocation endpointは未実装です。

```ts
import { server } from './sdk';
const job = await server.cron.register('dailySummary', { mode: 'compact' }, 86400);
const status = await server.cron.status('dailySummary');
// 不要になったら明示的に解除
await server.cron.unregister('dailySummary');
```

拡張機能サーバーを常時稼働できない環境では、定期実行の時刻・成功は保証されません。OS側の電池最適化に左右されない一方、ネットワーク・サーバー可用性が前提です。

# Intent: 拡張機能内イベント

`intent.register(name, handler)`で名前付き処理を登録し、`intent.perform(name, input)`で同じ拡張機能の実行コンテキスト内から呼び出します。複数ハンドラーは登録順に実行され、結果を配列で返します。解除関数を呼ぶと登録を解除します。未登録名・不正な名前・JSON化できない値・16KiBを超える入力はエラーになります。これはOSのIntentや他拡張機能との通信ではなく、アプリ外へデータを送信しません。

```ts
import { intent } from './sdk';
const unregister = intent.register('notes.save', async (input: { text: string }) => {
  return { saved: input.text.length > 0 };
});
const results = await intent.perform('notes.save', { text: '授業メモ' });
unregister();
```

完全な例は `extensions/examples/intents.ts` を参照してください。

## ボタンの長押し・ダブルタップ（Host API v79）

実行型拡張機能の `interactive.button` と `interactive.iconButton` は、通常タップとは別に `onLongPress` と `onDoublePress` を登録できます。TypeScript SDKがイベントIDを割り当て、ネイティブホストからQuickJSのコールバックへ配送します。

```ts
interactive.button('授業', openCourse, {
  onLongPress: () => showActions(),
  onDoublePress: () => toggleFavorite()
});
```

コールバックを使う拡張機能はHost API v79以上を必要とします。SDKビルドが最低バージョンを自動で設定し、サーバーは宣言不足を拒否します。ボタンが実行中で無効な間はイベントを配送しません。

### 2026-10-20 実装状況追記

`host.registerShortcut` / `shortcuts.register` は、従来のCIT Hub内タイルに加え、Androidで動的ランチャーショートカット、Apple各OSでShortcutsアプリのCIT Hub App Shortcutを提供します。iOS Simulator/Mac CatalystのビルドとApp Intentsメタデータ生成、Android API 33エミュレーター計装テスト52件を確認しました。Shortcutsアプリからの項目追加・実行とMac Catalystでの拡張機能UI操作は未確認です。起動時には項目IDを現在のアカウント・登録状態で照合し、無効な保留要求を破棄します。拡張機能サーバー本番サービスはrelease v87.1で稼働し、公開health、Host API v80、API HTML/Markdownの配信を確認しています。433 APIすべてのOS別実動作監査は未完了です。

<!-- GENERATED PUBLIC API INDEX START -->

## 全API署名索引（自動生成）

[API要件433件](api-inventory.json)のSDK呼び出し署名、実装位置、監査済みOSを示します。権限・データ範囲・制約は上記の機能別説明を参照してください。検証状態は個別APIの網羅的E2Eを意味しません。canvas.*の描画操作はcanvas.create()が返すインスタンスメソッドです。

| API | SDK呼び出し署名 | 実装位置 | 実行監査状態 |
| --- | --- | --- | --- |
| `app.onLaunch` | <code>app.onLaunch(callback: () =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:388 | verified-ios-android-mac |
| `app.onAppear` | <code>app.onAppear(callback: () =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:389 | verified-ios-android-mac |
| `app.onDisappear` | <code>app.onDisappear(callback: () =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:390 | verified-ios-android-mac |
| `app.onResume` | <code>app.onResume(callback: () =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:391 | verified-ios-android-mac |
| `app.onSuspend` | <code>app.onSuspend(callback: () =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:392 | verified-ios-android-mac |
| `app.onOpenURL` | <code>app.onOpenURL(callback: (payload: AppOpenURLPayload) =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:393 | verified-ios-android-mac |
| `app.onNotification` | <code>app.onNotification(callback: (payload: { identifier?: string | undefined; action?: string | undefined; data?: Record&lt;string, string&gt; | undefined; }) =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:394 | verified-ios-android-mac |
| `app.onMemoryWarning` | <code>app.onMemoryWarning(callback: () =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:395 | verified-ios-android-mac |
| `app.version` | <code>app.version(): string</code> | extensions/sdk/host.ts:397 | verified-ios-android-mac |
| `app.extensionId` | <code>app.extensionId(): string</code> | extensions/sdk/host.ts:398 | verified-ios-android-mac |
| `app.environment` | <code>app.environment(): "development" | "production"</code> | extensions/sdk/host.ts:398 | verified-ios-android-mac |
| `app.hostVersion` | <code>app.hostVersion(): string</code> | extensions/sdk/host.ts:399 | verified-ios-android-mac |
| `app.capabilities` | <code>app.capabilities(): string[]</code> | extensions/sdk/host.ts:399 | verified-ios-android-mac |
| `ui.text` | <code>ui.text(text: string): Node</code> | extensions/sdk/index.ts:137 | verified-ios-android-mac |
| `ui.heading` | <code>ui.heading(text: string): Node</code> | extensions/sdk/index.ts:131 | verified-ios-android-mac |
| `ui.label` | <code>ui.label(text: string): Node</code> | extensions/sdk/index.ts:98 | verified-ios-android-mac |
| `ui.markdown` | <code>ui.markdown(text: string): Node</code> | extensions/sdk/index.ts:99 | verified-ios-android-mac |
| `ui.code` | <code>ui.code(text: string): Node</code> | extensions/sdk/index.ts:100 | verified-ios-android-mac |
| `ui.caption` | <code>ui.caption(text: string): Node</code> | extensions/sdk/index.ts:101 | verified-ios-android-mac |
| `ui.badge` | <code>ui.badge(text: string, tone?: "primary" | "secondary" | "accent" | undefined): Node</code> | extensions/sdk/index.ts:102 | verified-ios-android-mac |
| `ui.icon` | <code>ui.icon(name: string, accessibilityLabel: string): Node</code> | extensions/sdk/index.ts:103 | verified-ios-android-mac |
| `ui.vstack` | <code>ui.vstack(children: Node[]): Node</code> | extensions/sdk/index.ts:147 | verified-ios-android-mac |
| `ui.hstack` | <code>ui.hstack(children: Node[]): Node</code> | extensions/sdk/index.ts:148 | verified-ios-android-mac |
| `ui.zstack` | <code>ui.zstack(children: Node[]): Node</code> | extensions/sdk/index.ts:149 | verified-ios-android-mac |
| `ui.grid` | <code>ui.grid(columns: number, children: Node[]): Node</code> | extensions/sdk/index.ts:112 | verified-ios-android-mac |
| `ui.lazyGrid` | <code>ui.lazyGrid(columns: number, children: Node[]): Node</code> | extensions/sdk/index.ts:94 | verified-ios-android-mac |
| `ui.scroll` | <code>ui.scroll(children: Node[]): Node</code> | extensions/sdk/index.ts:110 | verified-ios-android-mac |
| `ui.horizontalScroll` | <code>ui.horizontalScroll(children: Node[]): Node</code> | extensions/sdk/index.ts:111 | verified-ios-android-mac |
| `ui.spacer` | <code>ui.spacer(size?: number): Node</code> | extensions/sdk/index.ts:150 | verified-ios-android-mac |
| `ui.divider` | <code>ui.divider(): Node</code> | extensions/sdk/index.ts:132 | verified-ios-android-mac |
| `ui.card` | <code>ui.card(children: Node[], label?: string | undefined): Node</code> | extensions/sdk/index.ts:140 | verified-ios-android-mac |
| `ui.group` | <code>ui.group(children: Node[]): Node</code> | extensions/sdk/index.ts:141 | verified-ios-android-mac |
| `ui.section` | <code>ui.section(label: string, children: Node[]): Node</code> | extensions/sdk/index.ts:139 | verified-ios-android-mac |
| `ui.container` | <code>ui.container(children: Node[]): Node</code> | extensions/sdk/index.ts:142 | verified-ios-android-mac |
| `ui.safeArea` | <code>ui.safeArea(children: Node[]): Node</code> | extensions/sdk/index.ts:143 | verified-ios-android-mac |
| `ui.textField` | <code>ui.textField(key: string, label: string, value: State&lt;string&gt;, events?: { onFocus?: (() =&gt; unknown) | undefined; onBlur?: (() =&gt; unknown) | undefined; onSubmit?: (() =&gt; unknown) | undefined; }): Node</code> | extensions/sdk/index.ts:70 | verified-ios-android-mac |
| `ui.textArea` | <code>ui.textArea(key: string, label: string, value?: State&lt;string&gt; | undefined): Node</code> | extensions/sdk/index.ts:81 | verified-ios-android-mac |
| `ui.secureField` | <code>ui.secureField(key: string, label: string, value?: State&lt;string&gt; | undefined): Node</code> | extensions/sdk/index.ts:157 | verified-ios-android-mac |
| `ui.number` | <code>ui.number(key: string, label: string, min?: number, max?: number, step?: number): Node</code> | extensions/sdk/index.ts:104 | verified-ios-android-mac |
| `ui.slider` | <code>ui.slider(key: string, label: string, min?: number, max?: number, step?: number): Node</code> | extensions/sdk/index.ts:105 | verified-ios-android-mac |
| `ui.stepper` | <code>ui.stepper(key: string, label: string, value: State&lt;number&gt;, min: number, max: number, step?: number): Node</code> | extensions/sdk/index.ts:72 | verified-ios-android-mac |
| `ui.toggle` | <code>ui.toggle(key: string, label: string, value?: State&lt;boolean&gt; | undefined): Node</code> | extensions/sdk/index.ts:158 | verified-ios-android-mac |
| `ui.checkbox` | <code>ui.checkbox(key: string, label: string, value: State&lt;boolean&gt;): Node</code> | extensions/sdk/index.ts:73 | verified-ios-android-mac |
| `ui.radio` | <code>ui.radio(key: string, label: string, options: string[], value: State&lt;string&gt;): Node</code> | extensions/sdk/index.ts:74 | verified-ios-android-mac |
| `ui.select` | <code>ui.select(key: string, label: string, options: string[]): Node</code> | extensions/sdk/index.ts:133 | verified-ios-android-mac |
| `ui.multiSelect` | <code>ui.multiSelect(key: string, label: string, options: string[], value: State&lt;string[]&gt;): Node</code> | extensions/sdk/index.ts:75 | verified-ios-android-mac |
| `ui.date` | <code>ui.date(key: string, label: string, value?: State&lt;number | null&gt; | undefined): Node</code> | extensions/sdk/index.ts:129 | verified-ios-android-mac |
| `ui.time` | <code>ui.time(key: string, label: string, value: State&lt;number | null&gt;): Node</code> | extensions/sdk/index.ts:78 | verified-ios-android-mac |
| `ui.dateTime` | <code>ui.dateTime(key: string, label: string, value: State&lt;number | null&gt;): Node</code> | extensions/sdk/index.ts:76 | verified-ios-android-mac |
| `ui.dateRange` | <code>ui.dateRange(key: string, label: string, value: State&lt;{ start: number | null; end: number | null; }&gt;): Node</code> | extensions/sdk/index.ts:77 | verified-ios-android-mac |
| `ui.coursePicker` | <code>ui.coursePicker(key: string, label: string): Node</code> | extensions/sdk/index.ts:116 | verified-ios-android-mac |
| `ui.filePicker` | <code>ui.filePicker(key: string, label: string, value: State&lt;string&gt;): Node</code> | extensions/sdk/index.ts:79 | verified-ios-android-mac |
| `ui.photoPicker` | <code>ui.photoPicker(key: string, label: string, value: State&lt;string&gt;): Node</code> | extensions/sdk/index.ts:80 | verified-ios-android-mac |
| `ui.searchField` | <code>ui.searchField(key: string, label: string, value: State&lt;string&gt;, events?: { onFocus?: (() =&gt; unknown) | undefined; onBlur?: (() =&gt; unknown) | undefined; onSubmit?: (() =&gt; unknown) | undefined; }): Node</code> | extensions/sdk/index.ts:71 | verified-ios-android-mac |
| `field.set` | <code>field.set(value: T): void</code> | extensions/sdk/core.ts:67 | verified-ios-android-mac |
| `field.reset` | <code>field.reset(): void</code> | extensions/sdk/core.ts:67 | verified-ios-android-mac |
| `field.focus` | <code>field.focus(): Promise&lt;void&gt;</code> | extensions/sdk/fields.ts:3 | verified-on-some-hosts |
| `field.blur` | <code>field.blur(): Promise&lt;void&gt;</code> | extensions/sdk/fields.ts:3 | verified-on-some-hosts |
| `ui.button` | <code>ui.button(label: string, action: "save" | "reset" | (() =&gt; unknown)): Node</code> | extensions/sdk/index.ts:159 | verified-ios-android-mac |
| `ui.iconButton` | <code>ui.iconButton(icon: string, label: string, onPress: () =&gt; unknown, events?: ButtonEvents): Node</code> | extensions/sdk/index.ts:82 | verified-ios-android-mac |
| `ui.linkButton` | <code>ui.linkButton(label: string, url: string): Node</code> | extensions/sdk/index.ts:136 | verified-ios-android-mac |
| `ui.menu` | <code>ui.menu(label: string, actions: Node[]): Node</code> | extensions/sdk/index.ts:89 | verified-ios-android-mac |
| `ui.contextMenu` | <code>ui.contextMenu(content: Node, actions: Node[]): Node</code> | extensions/sdk/index.ts:90 | verified-ios-android-mac |
| `ui.swipeActions` | <code>ui.swipeActions(content: Node, actions: Node[]): Node</code> | extensions/sdk/index.ts:91 | verified-ios-android-mac |
| `ui.toolbar` | <code>ui.toolbar(children: Node[]): Node</code> | extensions/sdk/index.ts:92 | verified-ios-android-mac |
| `ui.floatingButton` | <code>ui.floatingButton(label: string, onPress: () =&gt; unknown, content?: Node[]): Node</code> | extensions/sdk/index.ts:93 | verified-ios-android-mac |
| `navigation.push` | <code>navigation.push(name: string, parameters?: RouteParameters): void</code> | extensions/sdk/navigation.ts:35 | verified-on-some-hosts |
| `navigation.pop` | <code>navigation.pop(): boolean</code> | extensions/sdk/navigation.ts:39 | verified-on-some-hosts |
| `navigation.popToRoot` | <code>navigation.popToRoot(): void</code> | extensions/sdk/navigation.ts:44 | verified-on-some-hosts |
| `navigation.present` | <code>navigation.present(name: string, parameters?: RouteParameters, mode?: "sheet" | "fullScreen"): void</code> | extensions/sdk/navigation.ts:48 | verified-on-some-hosts |
| `navigation.dismiss` | <code>navigation.dismiss(): boolean</code> | extensions/sdk/navigation.ts:57 | verified-on-some-hosts |
| `navigation.sheet` | <code>navigation.sheet(name: string, parameters?: RouteParameters): void</code> | extensions/sdk/navigation.ts:55 | verified-on-some-hosts |
| `navigation.fullScreen` | <code>navigation.fullScreen(name: string, parameters?: RouteParameters): void</code> | extensions/sdk/navigation.ts:56 | verified-on-some-hosts |
| `navigation.tabs` | <code>navigation.tabs(tabs: { id: string; title: string; render: () =&gt; Node[]; }[], selected: State&lt;string&gt;): Node</code> | extensions/sdk/navigation.ts:62 | verified-on-some-hosts |
| `navigation.openExtension` | <code>navigation.openExtension(id: string): Promise&lt;void&gt;</code> | extensions/sdk/navigation.ts:26 | verified-on-some-hosts |
| `navigation.openHostPage` | <code>navigation.openHostPage(page: HostPage): Promise&lt;void&gt;</code> | extensions/sdk/navigation.ts:27 | verified-on-some-hosts |
| `ui.alert` | <code>ui.alert(title: string, message?: string): Promise&lt;void&gt;</code> | extensions/sdk/index.ts:83 | verified-on-some-hosts |
| `ui.confirm` | <code>ui.confirm(title: string, message?: string): Promise&lt;boolean&gt;</code> | extensions/sdk/index.ts:84 | verified-on-some-hosts |
| `ui.prompt` | <code>ui.prompt(title: string, message?: string, initialValue?: string): Promise&lt;string | null&gt;</code> | extensions/sdk/index.ts:85 | verified-on-some-hosts |
| `ui.actionSheet` | <code>ui.actionSheet(title: string, items: ActionSheetItem[], message?: string): Promise&lt;string | null&gt;</code> | extensions/sdk/index.ts:86 | verified-on-some-hosts |
| `ui.toast` | <code>ui.toast(text: string, duration?: number): void</code> | extensions/sdk/index.ts:87 | verified-on-some-hosts |
| `ui.banner` | <code>ui.banner(text: string, duration?: number): void</code> | extensions/sdk/index.ts:88 | verified-on-some-hosts |
| `ui.loading` | <code>ui.loading(label: string): Node</code> | extensions/sdk/index.ts:153 | verified-ios-android-mac |
| `ui.progress` | <code>ui.progress(label: string, value: number): Node</code> | extensions/sdk/index.ts:151 | verified-ios-android-mac |
| `ui.emptyState` | <code>ui.emptyState(title: string, message: string): Node</code> | extensions/sdk/index.ts:154 | verified-ios-android-mac |
| `ui.errorState` | <code>ui.errorState(title: string, message: string): Node</code> | extensions/sdk/index.ts:155 | verified-ios-android-mac |
| `ui.list` | <code>ui.list&lt;T&gt;(items: ExtensionList&lt;T&gt;): Node</code> | extensions/sdk/index.ts:95 | verified-on-some-hosts |
| `ui.lazyList` | <code>ui.lazyList&lt;T&gt;(items: ExtensionList&lt;T&gt;): Node</code> | extensions/sdk/index.ts:96 | verified-on-some-hosts |
| `ui.sectionedList` | <code>ui.sectionedList&lt;T&gt;(sections: { title: string; list: ExtensionList&lt;T&gt;; }[]): Node</code> | extensions/sdk/index.ts:97 | verified-on-some-hosts |
| `list.filter` | <code>list.filter(predicate: (item: T) =&gt; boolean): ExtensionList&lt;T&gt;</code> | extensions/sdk/lists.ts:39 | verified-on-some-hosts |
| `list.sort` | <code>list.sort(compare: (a: T, b: T) =&gt; number): ExtensionList&lt;T&gt;</code> | extensions/sdk/lists.ts:40 | verified-on-some-hosts |
| `list.search` | <code>list.search(query: string, stringify?: ((item: T) =&gt; string) | undefined): ExtensionList&lt;T&gt;</code> | extensions/sdk/lists.ts:41 | verified-on-some-hosts |
| `list.select` | <code>list.select(key: string): void</code> | extensions/sdk/lists.ts:47 | verified-on-some-hosts |
| `list.multiSelect` | <code>list.multiSelect(keys: string[]): void</code> | extensions/sdk/lists.ts:48 | verified-on-some-hosts |
| `list.refresh` | <code>list.refresh(): Promise&lt;void&gt;</code> | extensions/sdk/lists.ts:53 | verified-on-some-hosts |
| `list.loadMore` | <code>list.loadMore(): Promise&lt;void&gt;</code> | extensions/sdk/lists.ts:59 | verified-on-some-hosts |
| `list.swipe` | <code>list.swipe(actions: { title: string; perform: (item: T) =&gt; unknown; }[]): ExtensionList&lt;T&gt;</code> | extensions/sdk/lists.ts:80 | verified-on-some-hosts |
| `list.contextMenu` | <code>list.contextMenu(actions: { title: string; perform: (item: T) =&gt; unknown; }[]): ExtensionList&lt;T&gt;</code> | extensions/sdk/lists.ts:81 | verified-on-some-hosts |
| `list.move` | <code>list.move(from: number, to: number): Promise&lt;void&gt;</code> | extensions/sdk/lists.ts:67 | verified-on-some-hosts |
| `list.delete` | <code>list.delete(key: string): Promise&lt;boolean&gt;</code> | extensions/sdk/lists.ts:73 | verified-on-some-hosts |
| `state.create` | <code>state.create&lt;T&gt;(initial: T): State&lt;T&gt;</code> | extensions/sdk/core.ts:68 | verified-ios-android-mac |
| `state.get` | <code>state.get&lt;T&gt;(value: State&lt;T&gt;): T</code> | extensions/sdk/core.ts:89 | verified-on-some-hosts |
| `state.set` | <code>state.set&lt;T&gt;(value: State&lt;T&gt;, next: T): void</code> | extensions/sdk/core.ts:90 | verified-ios-android-mac |
| `state.update` | <code>state.update&lt;T&gt;(value: State&lt;T&gt;, fn: (current: T) =&gt; T): void</code> | extensions/sdk/core.ts:91 | verified-on-some-hosts |
| `state.reset` | <code>state.reset&lt;T&gt;(value: State&lt;T&gt;): void</code> | extensions/sdk/core.ts:92 | verified-on-some-hosts |
| `state.watch` | <code>state.watch&lt;T&gt;(value: State&lt;T&gt;, fn: (next: T, previous: T) =&gt; void): () =&gt; void</code> | extensions/sdk/core.ts:93 | verified-on-some-hosts |
| `state.computed` | <code>state.computed&lt;T&gt;(dependencies: State&lt;unknown&gt;[], calculate: () =&gt; T): State&lt;T&gt; &amp; { dispose(): void; }</code> | extensions/sdk/core.ts:80 | verified-on-some-hosts |
| `date.now` | <code>date.now(): Date</code> | extensions/sdk/core.ts:123 | verified-ios-android-mac |
| `date.parse` | <code>date.parse(value: DateInput): Date</code> | extensions/sdk/core.ts:124 | verified-ios-android-mac |
| `date.format` | <code>date.format(value: DateInput, options?: DateTimeFormatOptions, locale?: string): string</code> | extensions/sdk/core.ts:124 | verified-ios-android-mac |
| `date.add` | <code>date.add(value: DateInput, milliseconds: number): Date</code> | extensions/sdk/core.ts:130 | verified-ios-android-mac |
| `date.subtract` | <code>date.subtract(value: DateInput, milliseconds: number): Date</code> | extensions/sdk/core.ts:131 | verified-ios-android-mac |
| `date.diff` | <code>date.diff(a: DateInput, b: DateInput): number</code> | extensions/sdk/core.ts:132 | verified-ios-android-mac |
| `date.startOfDay` | <code>date.startOfDay(value: DateInput): Date</code> | extensions/sdk/core.ts:133 | verified-ios-android-mac |
| `date.endOfDay` | <code>date.endOfDay(value: DateInput): Date</code> | extensions/sdk/core.ts:134 | verified-ios-android-mac |
| `date.startOfWeek` | <code>date.startOfWeek(value: DateInput, firstDay?: number): Date</code> | extensions/sdk/core.ts:135 | verified-ios-android-mac |
| `date.endOfWeek` | <code>date.endOfWeek(value: DateInput, firstDay?: number): Date</code> | extensions/sdk/core.ts:136 | verified-ios-android-mac |
| `date.isToday` | <code>date.isToday(value: DateInput): boolean</code> | extensions/sdk/core.ts:137 | verified-ios-android-mac |
| `date.isPast` | <code>date.isPast(value: DateInput): boolean</code> | extensions/sdk/core.ts:138 | verified-ios-android-mac |
| `date.isFuture` | <code>date.isFuture(value: DateInput): boolean</code> | extensions/sdk/core.ts:139 | verified-ios-android-mac |
| `date.timezone` | <code>date.timezone(): string</code> | extensions/sdk/core.ts:139 | verified-ios-android-mac |
| `cit.calendar.isClassDay` | <code>cit.calendar.isClassDay(date: Date): Promise&lt;boolean | null&gt;</code> | extensions/sdk/host.ts:756 | verified-ios-android-mac |
| `cit.calendar.currentSemester` | <code>cit.calendar.currentSemester(): Promise&lt;string | null&gt;</code> | extensions/sdk/host.ts:733 | verified-ios-android-mac |
| `cit.calendar.weekNumber` | <code>cit.calendar.weekNumber(date: Date): number</code> | extensions/sdk/host.ts:757 | verified-ios-android-mac |
| `math.round` | <code>math.round(x: number): number</code> | extensions/sdk/core.ts:97 | verified-ios-android-mac |
| `math.floor` | <code>math.floor(x: number): number</code> | extensions/sdk/core.ts:98 | verified-ios-android-mac |
| `math.ceil` | <code>math.ceil(x: number): number</code> | extensions/sdk/core.ts:98 | verified-ios-android-mac |
| `math.min` | <code>math.min(...values: number[]): number</code> | extensions/sdk/core.ts:98 | verified-ios-android-mac |
| `math.max` | <code>math.max(...values: number[]): number</code> | extensions/sdk/core.ts:98 | verified-ios-android-mac |
| `math.abs` | <code>math.abs(x: number): number</code> | extensions/sdk/core.ts:98 | verified-ios-android-mac |
| `math.random` | <code>math.random(): number</code> | extensions/sdk/core.ts:98 | verified-ios-android-mac |
| `math.clamp` | <code>math.clamp(value: number, min: number, max: number): number</code> | extensions/sdk/core.ts:98 | verified-ios-android-mac |
| `math.sum` | <code>math.sum(values: number[]): number</code> | extensions/sdk/core.ts:99 | verified-ios-android-mac |
| `math.average` | <code>math.average(values: number[]): number</code> | extensions/sdk/core.ts:100 | verified-ios-android-mac |
| `math.median` | <code>math.median(values: number[]): number</code> | extensions/sdk/core.ts:101 | verified-ios-android-mac |
| `utils.uuid` | <code>utils.uuid(): string</code> | extensions/sdk/core.ts:147 | verified-ios-android-mac |
| `utils.debounce` | <code>utils.debounce&lt;T extends unknown[]&gt;(callback: (...args: T) =&gt; void, milliseconds: number): ((...args: T) =&gt; void) &amp; { cancel(): void; }</code> | extensions/sdk/core.ts:167 | verified-ios-android-mac |
| `utils.throttle` | <code>utils.throttle&lt;T extends unknown[]&gt;(callback: (...args: T) =&gt; void, milliseconds: number): (...args: T) =&gt; void</code> | extensions/sdk/core.ts:173 | verified-ios-android-mac |
| `utils.sleep` | <code>utils.sleep(milliseconds: number, signal?: AbortSignal | undefined): Promise&lt;void&gt;</code> | extensions/sdk/core.ts:158 | verified-ios-android-mac |
| `text.format` | <code>text.format(template: string, values: Record&lt;string, string | number&gt;): string</code> | extensions/sdk/core.ts:104 | verified-ios-android-mac |
| `text.replace` | <code>text.replace(value: string, search: string, replacement: string): string</code> | extensions/sdk/core.ts:105 | verified-ios-android-mac |
| `text.split` | <code>text.split(value: string, separator: string): string[]</code> | extensions/sdk/core.ts:106 | verified-ios-android-mac |
| `text.join` | <code>text.join(values: string[], separator?: string): string</code> | extensions/sdk/core.ts:107 | verified-ios-android-mac |
| `text.contains` | <code>text.contains(value: string, search: string): boolean</code> | extensions/sdk/core.ts:107 | verified-ios-android-mac |
| `text.startsWith` | <code>text.startsWith(value: string, search: string): boolean</code> | extensions/sdk/core.ts:108 | verified-ios-android-mac |
| `text.endsWith` | <code>text.endsWith(value: string, search: string): boolean</code> | extensions/sdk/core.ts:108 | verified-ios-android-mac |
| `text.trim` | <code>text.trim(value: string): string</code> | extensions/sdk/core.ts:108 | verified-ios-android-mac |
| `text.lowercase` | <code>text.lowercase(value: string): string</code> | extensions/sdk/core.ts:109 | verified-ios-android-mac |
| `text.uppercase` | <code>text.uppercase(value: string): string</code> | extensions/sdk/core.ts:109 | verified-ios-android-mac |
| `text.regex` | <code>text.regex(pattern: string, flags?: string): RegExp</code> | extensions/sdk/core.ts:109 | verified-ios-android-mac |
| `json.parse` | <code>json.parse(value: string): unknown</code> | extensions/sdk/core.ts:118 | verified-ios-android-mac |
| `json.stringify` | <code>json.stringify(value: unknown): string</code> | extensions/sdk/core.ts:118 | verified-ios-android-mac |
| `cit.courses.list` | <code>cit.courses.list(): Promise&lt;Course[]&gt;</code> | extensions/sdk/host.ts:591 | verified-ios-android-mac |
| `cit.courses.get` | <code>cit.courses.get(id: string): Promise&lt;Course | null&gt;</code> | extensions/sdk/host.ts:592 | verified-ios-android-mac |
| `cit.courses.search` | <code>cit.courses.search(query: string): Promise&lt;Course[]&gt;</code> | extensions/sdk/host.ts:596 | verified-ios-android-mac |
| `cit.timetable.today` | <code>cit.timetable.today(at?: Date): Promise&lt;CourseOccurrence[]&gt;</code> | extensions/sdk/host.ts:619 | verified-ios-android-mac |
| `cit.timetable.tomorrow` | <code>cit.timetable.tomorrow(at?: Date): Promise&lt;CourseOccurrence[]&gt;</code> | extensions/sdk/host.ts:620 | verified-ios-android-mac |
| `cit.timetable.week` | <code>cit.timetable.week(at?: Date): Promise&lt;CourseOccurrence[]&gt;</code> | extensions/sdk/host.ts:622 | verified-ios-android-mac |
| `cit.timetable.current` | <code>cit.timetable.current(at?: Date): Promise&lt;CourseOccurrence[]&gt;</code> | extensions/sdk/host.ts:633 | verified-ios-android-mac |
| `cit.timetable.next` | <code>cit.timetable.next(at?: Date): Promise&lt;CourseOccurrence | null&gt;</code> | extensions/sdk/host.ts:637 | verified-ios-android-mac |
| `cit.timetable.at` | <code>cit.timetable.at(date: Date): Promise&lt;CourseOccurrence[]&gt;</code> | extensions/sdk/host.ts:621 | verified-ios-android-mac |
| `cit.timetable.freePeriods` | <code>cit.timetable.freePeriods(weekday?: number): Promise&lt;PeriodSlot[]&gt;</code> | extensions/sdk/host.ts:647 | verified-ios-android-mac |
| `cit.assignments.list` | <code>cit.assignments.list(): Promise&lt;Task[]&gt;</code> | extensions/sdk/host.ts:655 | verified-ios-android-mac |
| `cit.assignments.get` | <code>cit.assignments.get(id: string): Promise&lt;Task | null&gt;</code> | extensions/sdk/host.ts:656 | verified-ios-android-mac |
| `cit.assignments.pending` | <code>cit.assignments.pending(at?: Date): Promise&lt;Task[]&gt;</code> | extensions/sdk/host.ts:657 | verified-ios-android-mac |
| `cit.assignments.overdue` | <code>cit.assignments.overdue(at?: Date): Promise&lt;Task[]&gt;</code> | extensions/sdk/host.ts:661 | verified-ios-android-mac |
| `cit.assignments.dueSoon` | <code>cit.assignments.dueSoon(days?: number, at?: Date): Promise&lt;Task[]&gt;</code> | extensions/sdk/host.ts:662 | verified-ios-android-mac |
| `cit.assignments.search` | <code>cit.assignments.search(query: string): Promise&lt;Task[]&gt;</code> | extensions/sdk/host.ts:658 | verified-ios-android-mac |
| `cit.assignments.byCourse` | <code>cit.assignments.byCourse(course: string): Promise&lt;Task[]&gt;</code> | extensions/sdk/host.ts:659 | verified-ios-android-mac |
| `cit.assignments.between` | <code>cit.assignments.between(start: Date, end: Date): Promise&lt;Task[]&gt;</code> | extensions/sdk/host.ts:660 | verified-ios-android-mac |
| `cit.todo.list` | <code>cit.todo.list(): Promise&lt;PersonalTodo[]&gt;</code> | extensions/sdk/host.ts:668 | verified-ios-android-mac |
| `cit.todo.get` | <code>cit.todo.get(id: string): Promise&lt;PersonalTodo | null&gt;</code> | extensions/sdk/host.ts:674 | verified-ios-android-mac |
| `cit.todo.create` | <code>cit.todo.create(fields: TodoFields): Promise&lt;PersonalTodo&gt;</code> | extensions/sdk/host.ts:669 | verified-ios-android-mac |
| `cit.todo.update` | <code>cit.todo.update(expected: PersonalTodo, fields: TodoFields): Promise&lt;PersonalTodo&gt;</code> | extensions/sdk/host.ts:670 | verified-ios-android-mac |
| `cit.todo.complete` | <code>cit.todo.complete(id: string): Promise&lt;PersonalTodo | null&gt;</code> | extensions/sdk/host.ts:671 | verified-ios-android-mac |
| `cit.todo.uncomplete` | <code>cit.todo.uncomplete(id: string): Promise&lt;PersonalTodo | null&gt;</code> | extensions/sdk/host.ts:672 | verified-ios-android-mac |
| `cit.todo.delete` | <code>cit.todo.delete(expected: PersonalTodo): Promise&lt;null&gt;</code> | extensions/sdk/host.ts:673 | verified-ios-android-mac |
| `cit.todo.search` | <code>cit.todo.search(query: string): Promise&lt;PersonalTodo[]&gt;</code> | extensions/sdk/host.ts:678 | verified-ios-android-mac |
| `cit.todo.watch` | <code>cit.todo.watch(callback: (rows: PersonalTodo[]) =&gt; void, onError?: ((error: unknown) =&gt; void) | undefined): () =&gt; void</code> | extensions/sdk/host.ts:683 | verified-on-some-hosts |
| `cit.courseNotes.list` | <code>cit.courseNotes.list(): Promise&lt;{ id: string; courseKey: string; content: string; }[]&gt;</code> | extensions/sdk/host.ts:584 | verified-ios-android-mac |
| `cit.courseNotes.get` | <code>cit.courseNotes.get(id: string): Promise&lt;{ id: string; courseKey: string; content: string; } | null&gt;</code> | extensions/sdk/host.ts:585 | verified-ios-android-mac |
| `cit.courseNotes.create` | <code>cit.courseNotes.create(title: string, content: string): Promise&lt;{ id: string; courseKey: string; content: string; } | null&gt;</code> | extensions/sdk/host.ts:581 | verified-on-some-hosts |
| `cit.courseNotes.update` | <code>cit.courseNotes.update(id: string, content: string, expectedContent: string): Promise&lt;{ id: string; courseKey: string; content: string; } | null&gt;</code> | extensions/sdk/host.ts:582 | verified-on-some-hosts |
| `cit.courseNotes.delete` | <code>cit.courseNotes.delete(id: string, expectedContent: string): Promise&lt;null&gt;</code> | extensions/sdk/host.ts:583 | verified-on-some-hosts |
| `cit.semester.current` | <code>cit.semester.current(): Promise&lt;string | null&gt;</code> | extensions/sdk/host.ts:770 | verified-ios-android-mac |
| `cit.semester.list` | <code>cit.semester.list(): Promise&lt;string[]&gt;</code> | extensions/sdk/host.ts:767 | verified-ios-android-mac |
| `cit.calendar.events` | <code>cit.calendar.events(): Promise&lt;AcademicCalendarEvent[]&gt;</code> | extensions/sdk/host.ts:734 | verified-ios-android-mac |
| `cit.calendar.holidays` | <code>cit.calendar.holidays(start?: Date | undefined, end?: Date | undefined): Promise&lt;AcademicCalendarEvent[]&gt;</code> | extensions/sdk/host.ts:735 | verified-ios-android-mac |
| `cit.calendar.classDays` | <code>cit.calendar.classDays(start: Date, end: Date): Promise&lt;string[]&gt;</code> | extensions/sdk/host.ts:740 | verified-ios-android-mac |
| `cit.bus.routes` | <code>cit.bus.routes(): Promise&lt;BusRoute[]&gt;</code> | extensions/sdk/host.ts:545 | verified-ios-android-mac |
| `cit.bus.stops` | <code>cit.bus.stops(): Promise&lt;string[]&gt;</code> | extensions/sdk/host.ts:546 | verified-ios-android-mac |
| `cit.bus.timetable` | <code>cit.bus.timetable(at?: Date, routeFilter?: { from?: string | undefined; to?: string | undefined; } | undefined): Promise&lt;BusDeparture[]&gt;</code> | extensions/sdk/host.ts:547 | verified-ios-android-mac |
| `cit.bus.next` | <code>cit.bus.next(from?: string | undefined, to?: string | undefined, at?: Date): Promise&lt;BusDeparture | null&gt;</code> | extensions/sdk/host.ts:552 | verified-ios-android-mac |
| `cit.bus.between` | <code>cit.bus.between(start: Date, end: Date, routeFilter?: { from?: string | undefined; to?: string | undefined; } | undefined): Promise&lt;BusDeparture[]&gt;</code> | extensions/sdk/host.ts:563 | verified-ios-android-mac |
| `cit.bus.serviceStatus` | <code>cit.bus.serviceStatus(at?: Date): Promise&lt;BusServiceStatus&gt;</code> | extensions/sdk/host.ts:574 | verified-ios-android-mac |
| `cit.cafeteria.locations` | <code>cit.cafeteria.locations(): Promise&lt;CafeteriaLocation[]&gt;</code> | extensions/sdk/host.ts:539 | verified-ios-android-mac |
| `cit.cafeteria.menu` | <code>cit.cafeteria.menu(): Promise&lt;CafeteriaLocation[]&gt;</code> | extensions/sdk/host.ts:540 | verified-ios-android-mac |
| `cit.cafeteria.hours` | <code>cit.cafeteria.hours(): Promise&lt;CafeteriaHours[]&gt;</code> | extensions/sdk/host.ts:542 | verified-ios-android-mac |
| `cit.cafeteria.cameraStatus` | <code>cit.cafeteria.cameraStatus(): Promise&lt;CafeteriaCameraStatus[]&gt;</code> | extensions/sdk/host.ts:541 | verified-ios-android-mac |
| `cit.cafeteria.openCamera` | <code>cit.cafeteria.openCamera(): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:538 | verified-on-some-hosts |
| `cit.campus.facilities` | <code>cit.campus.facilities(): Promise&lt;CampusRoom[]&gt;</code> | extensions/sdk/host.ts:503 | verified-ios-android-mac |
| `cit.campus.buildings` | <code>cit.campus.buildings(): Promise&lt;{ campus: "新習志野" | "津田沼" | "不明"; building: string; rooms: CampusRoom[]; }[]&gt;</code> | extensions/sdk/host.ts:504 | verified-ios-android-mac |
| `cit.campus.rooms` | <code>cit.campus.rooms(): Promise&lt;CampusRoom[]&gt;</code> | extensions/sdk/host.ts:509 | verified-ios-android-mac |
| `cit.campus.room` | <code>cit.campus.room(id: string): Promise&lt;CampusRoom | null&gt;</code> | extensions/sdk/host.ts:510 | verified-ios-android-mac |
| `cit.campus.search` | <code>cit.campus.search(query: string): Promise&lt;CampusRoom[]&gt;</code> | extensions/sdk/host.ts:514 | verified-ios-android-mac |
| `cit.campus.roomAvailability` | <code>cit.campus.roomAvailability(roomID: string, at?: Date): Promise&lt;RoomScheduleStatus&gt;</code> | extensions/sdk/host.ts:519 | verified-ios-android-mac |
| `cit.services.list` | <code>cit.services.list(): Promise&lt;CampusService[]&gt;</code> | extensions/sdk/host.ts:530 | verified-ios-android-mac |
| `cit.services.open` | <code>cit.services.open(service: HostService): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:529 | verified-on-some-hosts |
| `cit.user.displayName` | <code>cit.user.displayName(): Promise&lt;string | null&gt;</code> | extensions/sdk/host.ts:711 | verified-ios-android-mac |
| `cit.user.preferences` | <code>cit.user.preferences(): Promise&lt;UserPreferences&gt;</code> | extensions/sdk/host.ts:710 | verified-ios-android-mac |
| `cit.settings.appearance` | <code>cit.settings.appearance(): Promise&lt;Pick&lt;UserPreferences, "accentColor"&gt;&gt;</code> | extensions/sdk/host.ts:705 | verified-ios-android-mac |
| `cit.settings.notifications` | <code>cit.settings.notifications(): Promise&lt;{ classStart: boolean; assignments: boolean; }&gt;</code> | extensions/sdk/host.ts:706 | verified-ios-android-mac |
| `cit.settings.language` | <code>cit.settings.language(): Promise&lt;string&gt;</code> | extensions/sdk/host.ts:704 | verified-ios-android-mac |
| `cit.settings.enabledServices` | <code>cit.settings.enabledServices(): Promise&lt;string[]&gt;</code> | extensions/sdk/host.ts:707 | verified-ios-android-mac |
| `storage.get` | <code>storage.get&lt;T = unknown&gt;(name: string): Promise&lt;T | null&gt;</code> | extensions/sdk/host.ts:316 | verified-ios-android-mac |
| `storage.set` | <code>storage.set(name: string, value: unknown): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:317 | verified-ios-android-mac |
| `storage.has` | <code>storage.has(name: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:318 | verified-ios-android-mac |
| `storage.remove` | <code>storage.remove(name: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:319 | verified-ios-android-mac |
| `storage.clear` | <code>storage.clear(): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:320 | verified-ios-android-mac |
| `storage.keys` | <code>storage.keys(): Promise&lt;string[]&gt;</code> | extensions/sdk/host.ts:321 | verified-ios-android-mac |
| `database.createTable` | <code>database.createTable(name: string): Promise&lt;void&gt;</code> | extensions/sdk/database.ts:130 | verified-ios-android-mac |
| `database.insert` | <code>database.insert(name: string, row: RecordValue): Promise&lt;void&gt;</code> | extensions/sdk/database.ts:131 | verified-ios-android-mac |
| `database.update` | <code>database.update(name: string, id: string, changes: Record&lt;string, unknown&gt;): Promise&lt;void&gt;</code> | extensions/sdk/database.ts:132 | verified-ios-android-mac |
| `database.delete` | <code>database.delete(name: string, id: string): Promise&lt;void&gt;</code> | extensions/sdk/database.ts:133 | verified-ios-android-mac |
| `database.get` | <code>database.get(name: string, id: string): Promise&lt;RecordValue | null&gt;</code> | extensions/sdk/database.ts:134 | verified-ios-android-mac |
| `database.query` | <code>database.query(name: string): Promise&lt;RecordValue[]&gt;</code> | extensions/sdk/database.ts:135 | verified-ios-android-mac |
| `database.transaction` | <code>database.transaction&lt;T&gt;(work: (transaction: Transaction) =&gt; T): Promise&lt;T&gt;</code> | extensions/sdk/database.ts:120 | verified-ios-android-mac |
| `database.watch` | <code>database.watch(name: string, callback: (rows: RecordValue[]) =&gt; void, onError?: ((error: unknown) =&gt; void) | undefined): () =&gt; void</code> | extensions/sdk/database.ts:136 | verified-on-some-hosts |
| `files.read` | <code>files.read(name: string): Promise&lt;Uint8Array&lt;ArrayBufferLike&gt; | null&gt;</code> | extensions/sdk/host.ts:281 | verified-ios-android-mac |
| `files.write` | <code>files.write(name: string, bytes: Uint8Array&lt;ArrayBufferLike&gt;): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:282 | verified-ios-android-mac |
| `files.delete` | <code>files.delete(name: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:285 | verified-ios-android-mac |
| `files.move` | <code>files.move(source: string, destination: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:284 | verified-ios-android-mac |
| `files.copy` | <code>files.copy(source: string, destination: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:283 | verified-ios-android-mac |
| `files.exists` | <code>files.exists(name: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:286 | verified-ios-android-mac |
| `files.list` | <code>files.list(): Promise&lt;string[]&gt;</code> | extensions/sdk/host.ts:287 | verified-ios-android-mac |
| `files.info` | <code>files.info(name: string): Promise&lt;ExtensionFileInfo | null&gt;</code> | extensions/sdk/host.ts:288 | verified-ios-android-mac |
| `files.pick` | <code>files.pick(kind?: FilePickerKind): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/host.ts:260 | verified-ios-android-mac |
| `files.pickMultiple` | <code>files.pickMultiple(kind?: FilePickerKind): Promise&lt;PickedFileInfo[] | null&gt;</code> | extensions/sdk/host.ts:270 | verified-ios-android-mac |
| `files.pickImage` | <code>files.pickImage(): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/host.ts:266 | verified-ios-android-mac |
| `files.pickVideo` | <code>files.pickVideo(): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/host.ts:267 | verified-ios-android-mac |
| `files.pickAudio` | <code>files.pickAudio(): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/host.ts:268 | verified-ios-android-mac |
| `files.pickDocument` | <code>files.pickDocument(): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/host.ts:269 | verified-ios-android-mac |
| `files.export` | <code>files.export(name: string, suggestedName?: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:275 | verified-on-some-hosts |
| `files.saveAs` | <code>files.saveAs(name: string, suggestedName?: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:280 | verified-on-some-hosts |
| `camera.capturePhoto` | <code>camera.capturePhoto(): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/camera.ts:4 | verified-on-some-hosts |
| `camera.captureVideo` | <code>camera.captureVideo(maxSeconds?: number): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/camera.ts:5 | verified-on-some-hosts |
| `camera.available` | <code>camera.available(): Promise&lt;boolean&gt;</code> | extensions/sdk/camera.ts:9 | verified-ios-android-mac |
| `camera.permissions` | <code>camera.permissions(): Promise&lt;PermissionStatus&gt;</code> | extensions/sdk/camera.ts:10 | verified-ios-android-mac |
| `camera.scanQRCode` | <code>camera.scanQRCode(): Promise&lt;ScannedCode | null&gt;</code> | extensions/sdk/camera.ts:11 | verified-on-some-hosts |
| `camera.scanBarcode` | <code>camera.scanBarcode(): Promise&lt;ScannedCode | null&gt;</code> | extensions/sdk/camera.ts:12 | verified-on-some-hosts |
| `photos.pick` | <code>photos.pick(): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/media.ts:14 | verified-ios-android-mac |
| `photos.pickMultiple` | <code>photos.pickMultiple(): Promise&lt;PickedFileInfo[] | null&gt;</code> | extensions/sdk/media.ts:15 | verified-ios-android-mac |
| `photos.save` | <code>photos.save(source: string): Promise&lt;boolean&gt;</code> | extensions/sdk/media.ts:13 | verified-on-some-hosts |
| `photos.info` | <code>photos.info(source: string): Promise&lt;ImageInfo&gt;</code> | extensions/sdk/media.ts:16 | verified-on-some-hosts |
| `image.resize` | <code>image.resize(input: ImageSource, name: string, width: number, height: number, options?: ImageOptions | undefined): Promise&lt;ImageResult&gt;</code> | extensions/sdk/image.ts:33 | verified-ios-android-mac |
| `image.crop` | <code>image.crop(input: ImageSource, name: string, rectangle: { x: number; y: number; width: number; height: number; }, options?: ImageOptions | undefined): Promise&lt;ImageResult&gt;</code> | extensions/sdk/image.ts:34 | verified-ios-android-mac |
| `image.rotate` | <code>image.rotate(input: ImageSource, name: string, degrees: 0 | 90 | 180 | 270, options?: ImageOptions | undefined): Promise&lt;ImageResult&gt;</code> | extensions/sdk/image.ts:39 | verified-ios-android-mac |
| `image.flip` | <code>image.flip(input: ImageSource, name: string, axis: "horizontal" | "vertical", options?: ImageOptions | undefined): Promise&lt;ImageResult&gt;</code> | extensions/sdk/image.ts:43 | verified-ios-android-mac |
| `image.compress` | <code>image.compress(input: ImageSource, name: string, quality?: number): Promise&lt;ImageResult&gt;</code> | extensions/sdk/image.ts:47 | verified-ios-android-mac |
| `image.convert` | <code>image.convert(input: ImageSource, name: string, format: "jpeg" | "png", quality?: number): Promise&lt;ImageResult&gt;</code> | extensions/sdk/image.ts:48 | verified-ios-android-mac |
| `image.thumbnail` | <code>image.thumbnail(input: ImageSource, name: string, maxDimension?: number): Promise&lt;ImageResult&gt;</code> | extensions/sdk/image.ts:49 | verified-ios-android-mac |
| `image.removeMetadata` | <code>image.removeMetadata(input: ImageSource, name: string, options?: ImageOptions | undefined): Promise&lt;ImageResult&gt;</code> | extensions/sdk/image.ts:50 | verified-ios-android-mac |
| `image.scanDocument` | <code>image.scanDocument(options?: { name?: string | undefined; maxPages?: number | undefined; }): Promise&lt;ScannedDocumentInfo | null&gt;</code> | extensions/sdk/image.ts:28 | verified-on-some-hosts |
| `audio.record` | <code>audio.record(maxSeconds?: number): Promise&lt;PickedFileInfo | null&gt;</code> | extensions/sdk/media.ts:28 | verified-on-some-hosts |
| `audio.stopRecording` | <code>audio.stopRecording(): Promise&lt;boolean&gt;</code> | extensions/sdk/media.ts:32 | verified-on-some-hosts |
| `audio.play` | <code>audio.play(source?: string | undefined): Promise&lt;void&gt;</code> | extensions/sdk/media.ts:33 | verified-on-some-hosts |
| `audio.pause` | <code>audio.pause(): Promise&lt;void&gt;</code> | extensions/sdk/media.ts:34 | verified-on-some-hosts |
| `audio.stop` | <code>audio.stop(): Promise&lt;void&gt;</code> | extensions/sdk/media.ts:35 | verified-on-some-hosts |
| `audio.duration` | <code>audio.duration(): Promise&lt;number | null&gt;</code> | extensions/sdk/media.ts:36 | verified-on-some-hosts |
| `audio.position` | <code>audio.position(): Promise&lt;number | null&gt;</code> | extensions/sdk/media.ts:37 | verified-on-some-hosts |
| `video.play` | <code>video.play(source?: string | undefined): Promise&lt;void&gt;</code> | extensions/sdk/media.ts:19 | verified-on-some-hosts |
| `video.pause` | <code>video.pause(): Promise&lt;void&gt;</code> | extensions/sdk/media.ts:20 | verified-on-some-hosts |
| `video.thumbnail` | <code>video.thumbnail(source: string, name: string, seconds?: number, maxDimension?: number): Promise&lt;GeneratedThumbnail&gt;</code> | extensions/sdk/media.ts:22 | verified-on-some-hosts |
| `video.info` | <code>video.info(source: string): Promise&lt;VideoInfo&gt;</code> | extensions/sdk/media.ts:21 | verified-on-some-hosts |
| `code.generateQR` | <code>code.generateQR(text: string, name: string, size?: number): Promise&lt;{ name: string; size: number; mimeType: "image/png"; width: number; height: number; }&gt;</code> | extensions/sdk/camera.ts:18 | verified-ios-android-mac |
| `code.scanQR` | <code>code.scanQR(): Promise&lt;ScannedCode | null&gt;</code> | extensions/sdk/camera.ts:17 | verified-on-some-hosts |
| `code.scanBarcode` | <code>code.scanBarcode(): Promise&lt;ScannedCode | null&gt;</code> | extensions/sdk/camera.ts:18 | verified-on-some-hosts |
| `location.current` | <code>location.current(): Promise&lt;LocationFix&gt;</code> | extensions/sdk/host.ts:65 | verified-on-some-hosts |
| `location.permission` | <code>location.permission(): Promise&lt;PermissionStatus&gt;</code> | extensions/sdk/host.ts:63 | verified-ios-android-mac |
| `location.openSettings` | <code>location.openSettings(): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:64 | verified-on-some-hosts |
| `map.view` | <code>map.view(options?: MapViewOptions, overlays?: Node[]): Node</code> | extensions/sdk/maps.ts:22 | verified-ios-android-mac |
| `map.marker` | <code>map.marker(location: MapCoordinate, options: MapMarkerOptions): Node</code> | extensions/sdk/maps.ts:31 | verified-ios-android-mac |
| `map.polyline` | <code>map.polyline(path: MapCoordinate[], options?: MapShapeOptions): Node</code> | extensions/sdk/maps.ts:38 | verified-ios-android-mac |
| `map.circle` | <code>map.circle(center: MapCoordinate, radiusMeters: number, options?: MapShapeOptions): Node</code> | extensions/sdk/maps.ts:44 | verified-ios-android-mac |
| `map.fitBounds` | <code>map.fitBounds(bounds: MapCoordinate[], padding?: number): Node</code> | extensions/sdk/maps.ts:51 | verified-ios-android-mac |
| `map.openDirections` | <code>map.openDirections(destination: MapCoordinate, options?: DirectionsOptions): Promise&lt;void&gt;</code> | extensions/sdk/maps.ts:55 | verified-ios-android-mac |
| `geo.distance` | <code>geo.distance(from: Coordinate, to: Coordinate): number</code> | extensions/sdk/core.ts:187 | verified-ios-android-mac |
| `geo.bearing` | <code>geo.bearing(from: Coordinate, to: Coordinate): number</code> | extensions/sdk/core.ts:193 | verified-ios-android-mac |
| `geo.contains` | <code>geo.contains(point: Coordinate, circle: { center: Coordinate; radius: number; }): boolean</code> | extensions/sdk/core.ts:200 | verified-ios-android-mac |
| `notification.permission` | <code>notification.permission(): Promise&lt;PermissionStatus&gt;</code> | extensions/sdk/host.ts:109 | verified-on-some-hosts |
| `notification.schedule` | <code>notification.schedule(value: LocalNotificationInput): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:110 | verified-on-some-hosts |
| `notification.cancel` | <code>notification.cancel(id: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:115 | verified-on-some-hosts |
| `notification.cancelAll` | <code>notification.cancelAll(): Promise&lt;number&gt;</code> | extensions/sdk/host.ts:116 | verified-on-some-hosts |
| `notification.listScheduled` | <code>notification.listScheduled(): Promise&lt;ScheduledLocalNotification[]&gt;</code> | extensions/sdk/host.ts:117 | verified-on-some-hosts |
| `notification.onOpen` | <code>notification.onOpen(callback: (notification: { id: string; data: Record&lt;string, string&gt;; action: string; }) =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:118 | verified-on-some-hosts |
| `background.register` | <code>background.register(task: { id: string; intervalMinutes: number; networkRequired?: boolean | undefined; chargingRequired?: boolean | undefined; }): Promise&lt;{ id: string; accepted: boolean; }&gt;</code> | extensions/sdk/host.ts:48 | verified-on-some-hosts |
| `background.unregister` | <code>background.unregister(id: string): Promise&lt;boolean&gt;</code> | extensions/sdk/host.ts:58 | verified-on-some-hosts |
| `background.status` | <code>background.status(): Promise&lt;BackgroundStatus&gt;</code> | extensions/sdk/host.ts:47 | verified-ios-android-mac |
| `network.fetch` | <code>network.fetch(url: string, options?: NetworkOptions): Promise&lt;NetworkResponse&gt;</code> | extensions/sdk/host.ts:147 | verified-on-some-hosts |
| `network.get` | <code>network.get(url: string, headers?: Record&lt;string, string&gt; | undefined): Promise&lt;NetworkResponse&gt;</code> | extensions/sdk/host.ts:179 | verified-on-some-hosts |
| `network.post` | <code>network.post(url: string, body: string, headers?: Record&lt;string, string&gt; | undefined): Promise&lt;NetworkResponse&gt;</code> | extensions/sdk/host.ts:180 | verified-on-some-hosts |
| `network.put` | <code>network.put(url: string, body: string, headers?: Record&lt;string, string&gt; | undefined): Promise&lt;NetworkResponse&gt;</code> | extensions/sdk/host.ts:181 | verified-on-some-hosts |
| `network.patch` | <code>network.patch(url: string, body: string, headers?: Record&lt;string, string&gt; | undefined): Promise&lt;NetworkResponse&gt;</code> | extensions/sdk/host.ts:182 | verified-on-some-hosts |
| `network.delete` | <code>network.delete(url: string, headers?: Record&lt;string, string&gt; | undefined): Promise&lt;NetworkResponse&gt;</code> | extensions/sdk/host.ts:183 | verified-on-some-hosts |
| `network.upload` | <code>network.upload(url: string, name: string, options?: { method?: "POST" | "PUT" | undefined; headers?: Record&lt;string, string&gt; | undefined; onProgress?: NetworkProgressHandler | undefined; }): Promise&lt;NetworkResponse&gt;</code> | extensions/sdk/host.ts:135 | verified-on-some-hosts |
| `network.download` | <code>network.download(url: string, name: string, options?: Record&lt;string, string&gt; | { headers?: Record&lt;string, string&gt; | undefined; onProgress?: NetworkProgressHandler | undefined; }): Promise&lt;NetworkFileResponse&gt;</code> | extensions/sdk/host.ts:136 | verified-on-some-hosts |
| `network.websocket.connect` | <code>network.websocket.connect(url: string): Promise&lt;ExtensionWebSocket&gt;</code> | extensions/sdk/host.ts:163 | verified-on-some-hosts |
| `socket.send` | <code>socket.send(data: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:99 | verified-on-some-hosts |
| `socket.onMessage` | <code>socket.onMessage(callback: (data: string) =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:96 | verified-on-some-hosts |
| `socket.close` | <code>socket.close(code?: number | undefined, reason?: string | undefined): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:100 | verified-on-some-hosts |
| `browser.open` | <code>browser.open(url: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:68 | verified-on-some-hosts |
| `browser.openExternal` | <code>browser.openExternal(url: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:72 | verified-on-some-hosts |
| `webView.open` | <code>webView.open(url: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:778 | verified-on-some-hosts |
| `clipboard.write` | <code>clipboard.write(value: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:205 | verified-on-some-hosts |
| `clipboard.read` | <code>clipboard.read(): Promise&lt;string | null&gt;</code> | extensions/sdk/host.ts:204 | verified-on-some-hosts |
| `share.text` | <code>share.text(value: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:232 | verified-on-some-hosts |
| `share.url` | <code>share.url(value: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:233 | verified-on-some-hosts |
| `share.image` | <code>share.image(key: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:230 | verified-on-some-hosts |
| `share.file` | <code>share.file(key: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:234 | verified-on-some-hosts |
| `share.items` | <code>share.items(items: ShareItem[]): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:231 | verified-on-some-hosts |
| `drag.source` | <code>drag.source(label: string, value: string): Node</code> | extensions/sdk/drag.ts:10 | verified-on-some-hosts |
| `drop.target` | <code>drop.target(key: string, label: string): Node</code> | extensions/sdk/drag.ts:23 | verified-on-some-hosts |
| `haptics.light` | <code>haptics.light(): Promise&lt;void&gt;</code> | extensions/sdk/haptics.ts:9 | verified-on-some-hosts |
| `haptics.medium` | <code>haptics.medium(): Promise&lt;void&gt;</code> | extensions/sdk/haptics.ts:10 | verified-on-some-hosts |
| `haptics.heavy` | <code>haptics.heavy(): Promise&lt;void&gt;</code> | extensions/sdk/haptics.ts:11 | verified-on-some-hosts |
| `haptics.success` | <code>haptics.success(): Promise&lt;void&gt;</code> | extensions/sdk/haptics.ts:12 | verified-on-some-hosts |
| `haptics.warning` | <code>haptics.warning(): Promise&lt;void&gt;</code> | extensions/sdk/haptics.ts:13 | verified-on-some-hosts |
| `haptics.error` | <code>haptics.error(): Promise&lt;void&gt;</code> | extensions/sdk/haptics.ts:14 | verified-on-some-hosts |
| `device.platform` | <code>device.platform(): Promise&lt;"ios" | "macos" | "android"&gt;</code> | extensions/sdk/device.ts:14 | verified-ios-android-mac |
| `device.osVersion` | <code>device.osVersion(): Promise&lt;string&gt;</code> | extensions/sdk/device.ts:15 | verified-ios-android-mac |
| `device.deviceClass` | <code>device.deviceClass(): Promise&lt;"phone" | "tablet" | "desktop"&gt;</code> | extensions/sdk/device.ts:16 | verified-ios-android-mac |
| `device.screen` | <code>device.screen(): Promise&lt;DeviceScreen&gt;</code> | extensions/sdk/device.ts:17 | verified-ios-android-mac |
| `device.locale` | <code>device.locale(): Promise&lt;string&gt;</code> | extensions/sdk/device.ts:18 | verified-ios-android-mac |
| `device.timezone` | <code>device.timezone(): Promise&lt;string&gt;</code> | extensions/sdk/device.ts:19 | verified-ios-android-mac |
| `device.appearance` | <code>device.appearance(): Promise&lt;"light" | "dark"&gt;</code> | extensions/sdk/device.ts:20 | verified-ios-android-mac |
| `device.capabilities` | <code>device.capabilities(): Promise&lt;string[]&gt;</code> | extensions/sdk/device.ts:21 | verified-ios-android-mac |
| `keyboard.dismiss` | <code>keyboard.dismiss(): Promise&lt;void&gt;</code> | extensions/sdk/keyboard.ts:9 | verified-on-some-hosts |
| `keyboard.focus` | <code>keyboard.focus(key: string): Promise&lt;void&gt;</code> | extensions/sdk/keyboard.ts:3 | verified-on-some-hosts |
| `accessibility.label` | <code>accessibility.label(node: Node, value: string): Node</code> | extensions/sdk/accessibility.ts:22 | verified-on-some-hosts |
| `accessibility.hint` | <code>accessibility.hint(node: Node, value: string): Node</code> | extensions/sdk/accessibility.ts:23 | verified-on-some-hosts |
| `accessibility.role` | <code>accessibility.role(node: Node, value: AccessibilityRole): Node</code> | extensions/sdk/accessibility.ts:24 | verified-on-some-hosts |
| `accessibility.isReduceMotionEnabled` | <code>accessibility.isReduceMotionEnabled(): Promise&lt;boolean&gt;</code> | extensions/sdk/accessibility.ts:26 | verified-on-some-hosts |
| `accessibility.isHighContrastEnabled` | <code>accessibility.isHighContrastEnabled(): Promise&lt;boolean&gt;</code> | extensions/sdk/accessibility.ts:27 | verified-on-some-hosts |
| `animation.animate` | <code>animation.animate&lt;T&gt;(options: AnimationOptions, update: () =&gt; T | Promise&lt;T&gt;): Promise&lt;T&gt;</code> | extensions/sdk/index.ts:52 | verified-ios-android-mac |
| `animation.spring` | <code>animation.spring&lt;T&gt;(update: () =&gt; T | Promise&lt;T&gt;, options?: Omit&lt;AnimationOptions, "curve"&gt;): Promise&lt;T&gt;</code> | extensions/sdk/index.ts:62 | verified-ios-android-mac |
| `animation.transition` | <code>animation.transition&lt;T&gt;(update: () =&gt; T | Promise&lt;T&gt;, options?: AnimationOptions): Promise&lt;T&gt;</code> | extensions/sdk/index.ts:65 | verified-ios-android-mac |
| `chart.line` | <code>chart.line(label: string, categories: string[], series: ChartSeries[]): Node</code> | extensions/sdk/charts.ts:52 | verified-ios-android-mac |
| `chart.bar` | <code>chart.bar(label: string, categories: string[], series: ChartSeries[]): Node</code> | extensions/sdk/charts.ts:54 | verified-ios-android-mac |
| `chart.area` | <code>chart.area(label: string, categories: string[], series: ChartSeries[]): Node</code> | extensions/sdk/charts.ts:53 | verified-ios-android-mac |
| `chart.pie` | <code>chart.pie(label: string, categories: string[], values: number[]): Node</code> | extensions/sdk/charts.ts:55 | verified-ios-android-mac |
| `chart.donut` | <code>chart.donut(label: string, categories: string[], values: number[]): Node</code> | extensions/sdk/charts.ts:56 | verified-ios-android-mac |
| `chart.scatter` | <code>chart.scatter(label: string, series: ScatterSeries[]): Node</code> | extensions/sdk/charts.ts:57 | verified-ios-android-mac |
| `canvas.create` | <code>canvas.create(width?: number, height?: number, label?: string): ExtensionCanvas</code> | extensions/sdk/canvas.ts:62 | verified-on-some-hosts |
| `canvas.line` | <code>canvas.create().line(x1: number, y1: number, x2: number, y2: number, appearance?: DrawingStyle): ExtensionCanvas</code> | extensions/sdk/canvas.ts:27 | verified-on-some-hosts |
| `canvas.rect` | <code>canvas.create().rect(x: number, y: number, width: number, height: number, appearance?: DrawingStyle): ExtensionCanvas</code> | extensions/sdk/canvas.ts:30 | verified-on-some-hosts |
| `canvas.circle` | <code>canvas.create().circle(x: number, y: number, radius: number, appearance?: DrawingStyle): ExtensionCanvas</code> | extensions/sdk/canvas.ts:34 | verified-on-some-hosts |
| `canvas.text` | <code>canvas.create().text(text: string, x: number, y: number, options?: { color?: DrawingColor | undefined; size?: number | undefined; align?: "leading" | "center" | "trailing" | undefined; }): ExtensionCanvas</code> | extensions/sdk/canvas.ts:38 | verified-on-some-hosts |
| `canvas.image` | <code>canvas.create().image(url: string, x: number, y: number, width: number, height: number): ExtensionCanvas</code> | extensions/sdk/canvas.ts:43 | verified-on-some-hosts |
| `canvas.path` | <code>canvas.create().path(segments: PathSegment[], appearance?: DrawingStyle): ExtensionCanvas</code> | extensions/sdk/canvas.ts:48 | verified-on-some-hosts |
| `pdf.open` | <code>pdf.open(source: string): Promise&lt;void&gt;</code> | extensions/sdk/media.ts:41 | verified-on-some-hosts |
| `pdf.pageCount` | <code>pdf.pageCount(source: string): Promise&lt;number&gt;</code> | extensions/sdk/media.ts:42 | verified-on-some-hosts |
| `pdf.thumbnail` | <code>pdf.thumbnail(source: string, name: string, page?: number, maxDimension?: number): Promise&lt;GeneratedThumbnail&gt;</code> | extensions/sdk/media.ts:48 | verified-on-some-hosts |
| `pdf.export` | <code>pdf.export(name: string, suggestedName?: string): Promise&lt;boolean&gt;</code> | extensions/sdk/media.ts:52 | verified-on-some-hosts |
| `pdf.share` | <code>pdf.share(source: string): Promise&lt;void&gt;</code> | extensions/sdk/media.ts:43 | verified-on-some-hosts |
| `pdf.create` | <code>pdf.create(name: string, pages: PDFPageInput[]): Promise&lt;PDFInfo&gt;</code> | extensions/sdk/media.ts:44 | verified-ios-android-mac |
| `scanner.document` | <code>scanner.document(options?: { name?: string | undefined; maxPages?: number | undefined; }): Promise&lt;ScannedDocumentInfo | null&gt;</code> | extensions/sdk/host.ts:306 | verified-on-some-hosts |
| `sensors.accelerometer` | <code>sensors.accelerometer(): Promise&lt;MotionVector&gt;</code> | extensions/sdk/sensors.ts:14 | verified-on-some-hosts |
| `sensors.gyroscope` | <code>sensors.gyroscope(): Promise&lt;MotionVector&gt;</code> | extensions/sdk/sensors.ts:15 | verified-on-some-hosts |
| `sensors.magnetometer` | <code>sensors.magnetometer(): Promise&lt;MotionVector&gt;</code> | extensions/sdk/sensors.ts:16 | verified-on-some-hosts |
| `sensors.orientation` | <code>sensors.orientation(): Promise&lt;MotionOrientation&gt;</code> | extensions/sdk/sensors.ts:17 | verified-on-some-hosts |
| `connectivity.status` | <code>connectivity.status(): Promise&lt;NetworkStatus&gt;</code> | extensions/sdk/host.ts:776 | verified-ios-android-mac |
| `connectivity.onChange` | <code>connectivity.onChange(callback: (status: NetworkStatus) =&gt; unknown): Promise&lt;() =&gt; void&gt;</code> | extensions/sdk/host.ts:776 | verified-on-some-hosts |
| `crypto.randomBytes` | <code>crypto.randomBytes(length: number): Uint8Array&lt;ArrayBufferLike&gt;</code> | extensions/sdk/crypto.ts:31 | verified-on-some-hosts |
| `crypto.hash` | <code>crypto.hash(text: string, algorithm?: "SHA-256" | "SHA-512"): Promise&lt;string&gt;</code> | extensions/sdk/crypto.ts:35 | verified-on-some-hosts |
| `crypto.hmac` | <code>crypto.hmac(text: string, key: string, algorithm?: "SHA-256" | "SHA-512"): Promise&lt;string&gt;</code> | extensions/sdk/crypto.ts:36 | verified-on-some-hosts |
| `crypto.encrypt` | <code>crypto.encrypt(text: string, key: string, aad?: string): Promise&lt;EncryptedText&gt;</code> | extensions/sdk/crypto.ts:33 | verified-on-some-hosts |
| `crypto.decrypt` | <code>crypto.decrypt(envelope: EncryptedText, key: string, aad?: string): Promise&lt;string&gt;</code> | extensions/sdk/crypto.ts:34 | verified-on-some-hosts |
| `secureStorage.set` | <code>secureStorage.set(name: string, value: unknown): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:347 | verified-on-some-hosts |
| `secureStorage.get` | <code>secureStorage.get&lt;T = unknown&gt;(name: string): Promise&lt;T | null&gt;</code> | extensions/sdk/host.ts:346 | verified-on-some-hosts |
| `secureStorage.delete` | <code>secureStorage.delete(name: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:348 | verified-on-some-hosts |
| `auth.oauth2` | <code>auth.oauth2(providerInput: OAuthProvider): Promise&lt;OAuthSession&gt;</code> | extensions/sdk/auth.ts:99 | verified-on-some-hosts |
| `auth.openIDConnect` | <code>auth.openIDConnect(config: { issuer: string; clientId: string; scopes: string[]; }): Promise&lt;OIDCSession&gt;</code> | extensions/sdk/auth.ts:102 | verified-on-some-hosts |
| `auth.logout` | <code>auth.logout(): Promise&lt;void&gt;</code> | extensions/sdk/auth.ts:119 | verified-ios-android-mac |
| `auth.tokenStatus` | <code>auth.tokenStatus(): Promise&lt;{ connected: boolean; expiresAt: number | null; scopes: string[]; }&gt;</code> | extensions/sdk/auth.ts:114 | verified-ios-android-mac |
| `link.create` | <code>link.create(id: string): Promise&lt;string&gt;</code> | extensions/sdk/integration.ts:23 | verified-on-some-hosts |
| `link.open` | <code>link.open(url: string): Promise&lt;void&gt;</code> | extensions/sdk/integration.ts:24 | verified-on-some-hosts |
| `app.onDeepLink` | <code>app.onDeepLink(callback: (payload: AppOpenURLPayload) =&gt; unknown): () =&gt; void</code> | extensions/sdk/host.ts:387 | verified-ios-android-mac |
| `extensions.isInstalled` | <code>extensions.isInstalled(id: string): Promise&lt;boolean&gt;</code> | extensions/sdk/integration.ts:18 | verified-on-some-hosts |
| `extensions.open` | <code>extensions.open(id: string): Promise&lt;void&gt;</code> | extensions/sdk/integration.ts:19 | verified-on-some-hosts |
| `extensions.request` | <code>extensions.request(id: string): Promise&lt;void&gt;</code> | extensions/sdk/integration.ts:20 | verified-on-some-hosts |
| `intent.register` | <code>intent.register&lt;T = unknown, R = unknown&gt;(name: string, handler: IntentHandler&lt;T, R&gt;): () =&gt; void</code> | extensions/sdk/intents.ts:21 | verified-on-some-hosts |
| `intent.perform` | <code>intent.perform&lt;T = unknown, R = unknown&gt;(name: string, input?: T | undefined): Promise&lt;R[]&gt;</code> | extensions/sdk/intents.ts:35 | verified-on-some-hosts |
| `search.registerItems` | <code>search.registerItems(items: SearchItem[]): Promise&lt;{ indexed: number; }&gt;</code> | extensions/sdk/platform.ts:47 | verified-on-some-hosts |
| `search.removeItems` | <code>search.removeItems(ids: string[]): Promise&lt;{ removed: number; }&gt;</code> | extensions/sdk/platform.ts:55 | verified-on-some-hosts |
| `host.registerShortcut` | <code>host.registerShortcut(value: HostAction): Promise&lt;{ registered: boolean; }&gt;</code> | extensions/sdk/platform.ts:68 | verified-ios-android-mac |
| `host.registerAction` | <code>host.registerAction(value: HostAction): Promise&lt;{ registered: boolean; }&gt;</code> | extensions/sdk/platform.ts:72 | verified-on-some-hosts |
| `widget.define` | <code>widget.define(value: WidgetDefinition): Promise&lt;{ registered: boolean; }&gt;</code> | extensions/sdk/platform.ts:79 | verified-ios-android-mac |
| `widget.reload` | <code>widget.reload(id: string): Promise&lt;{ queued: boolean; }&gt;</code> | extensions/sdk/platform.ts:85 | verified-ios-android-mac |
| `shortcuts.register` | <code>shortcuts.register(value: HostAction): Promise&lt;{ registered: boolean; }&gt;</code> | extensions/sdk/platform.ts:89 | verified-ios-android-mac |
| `server.function` | <code>server.function&lt;T = unknown&gt;(name: string, input?: unknown): Promise&lt;T&gt;</code> | extensions/sdk/server.ts:73 | verified-on-some-hosts |
| `server.kv.get` | <code>server.kv.get&lt;T = unknown&gt;(key: string): Promise&lt;{ value: T | null; revision: number; }&gt;</code> | extensions/sdk/server.ts:85 | verified-on-some-hosts |
| `server.kv.set` | <code>server.kv.set(key: string, value: unknown, expectedRevision: number): Promise&lt;number&gt;</code> | extensions/sdk/server.ts:86 | verified-on-some-hosts |
| `server.kv.delete` | <code>server.kv.delete(key: string, expectedRevision: number): Promise&lt;boolean&gt;</code> | extensions/sdk/server.ts:87 | verified-on-some-hosts |
| `server.db.insert` | <code>server.db.insert&lt;T extends Record&lt;string, unknown&gt;&gt;(table: string, id: string, value: T): Promise&lt;ServerRecord&lt;T&gt;&gt;</code> | extensions/sdk/server.ts:100 | verified-on-some-hosts |
| `server.db.query` | <code>server.db.query&lt;T extends Record&lt;string, unknown&gt; = Record&lt;string, unknown&gt;&gt;(table: string, options?: ServerQuery): Promise&lt;{ rows: ServerRecord&lt;T&gt;[]; nextCursor: string | null; }&gt;</code> | extensions/sdk/server.ts:101 | verified-on-some-hosts |
| `server.db.update` | <code>server.db.update&lt;T extends Record&lt;string, unknown&gt;&gt;(table: string, id: string, value: T, expectedRevision: number): Promise&lt;ServerRecord&lt;T&gt;&gt;</code> | extensions/sdk/server.ts:102 | verified-on-some-hosts |
| `server.db.delete` | <code>server.db.delete(table: string, id: string, expectedRevision: number): Promise&lt;{ deleted: boolean; }&gt;</code> | extensions/sdk/server.ts:103 | verified-on-some-hosts |
| `server.db.transaction` | <code>server.db.transaction(operations: ServerOperation[]): Promise&lt;ServerResult[]&gt;</code> | extensions/sdk/server.ts:104 | verified-on-some-hosts |
| `cloud.user.get` | <code>cloud.user.get&lt;T = unknown&gt;(name: string): Promise&lt;{ value: T | null; revision: number; }&gt;</code> | extensions/sdk/host.ts:326 | verified-on-some-hosts |
| `cloud.user.set` | <code>cloud.user.set(name: string, expectedRevision: number, value: unknown): Promise&lt;number&gt;</code> | extensions/sdk/host.ts:330 | verified-on-some-hosts |
| `cloud.shared.create` | <code>cloud.shared.create&lt;T&gt;(group: string, name: string, value: T): Promise&lt;SharedValue&lt;T&gt;&gt;</code> | extensions/sdk/collaboration.ts:30 | verified-on-some-hosts |
| `cloud.shared.read` | <code>cloud.shared.read&lt;T = unknown&gt;(group: string, name: string): Promise&lt;SharedValue&lt;T&gt; | null&gt;</code> | extensions/sdk/collaboration.ts:31 | verified-on-some-hosts |
| `cloud.shared.update` | <code>cloud.shared.update&lt;T&gt;(group: string, name: string, value: T, expectedRevision: number): Promise&lt;SharedValue&lt;T&gt;&gt;</code> | extensions/sdk/collaboration.ts:32 | verified-on-some-hosts |
| `cloud.shared.delete` | <code>cloud.shared.delete(group: string, name: string, expectedRevision: number): Promise&lt;{ deleted: true; }&gt;</code> | extensions/sdk/collaboration.ts:33 | verified-on-some-hosts |
| `groups.create` | <code>groups.create(name: string): Promise&lt;ExtensionGroup&gt;</code> | extensions/sdk/collaboration.ts:13 | verified-on-some-hosts |
| `groups.join` | <code>groups.join(invitation: string): Promise&lt;ExtensionGroup | null&gt;</code> | extensions/sdk/collaboration.ts:17 | verified-on-some-hosts |
| `groups.leave` | <code>groups.leave(id: string): Promise&lt;{ left: true; } | null&gt;</code> | extensions/sdk/collaboration.ts:21 | verified-on-some-hosts |
| `groups.members` | <code>groups.members(id: string): Promise&lt;GroupMember[]&gt;</code> | extensions/sdk/collaboration.ts:22 | verified-on-some-hosts |
| `groups.invite` | <code>groups.invite(id: string, options?: { expiresInHours?: number | undefined; maxUses?: number | undefined; }): Promise&lt;GroupInvitation&gt;</code> | extensions/sdk/collaboration.ts:23 | verified-on-some-hosts |
| `realtime.subscribe` | <code>realtime.subscribe&lt;T&gt;(group: string, topic: string, onMessage: (message: RealtimeMessage&lt;T&gt;) =&gt; unknown, options?: RealtimeSubscribeOptions): () =&gt; void</code> | extensions/sdk/collaboration.ts:54 | verified-on-some-hosts |
| `realtime.publish` | <code>realtime.publish&lt;T&gt;(group: string, topic: string, value: T): Promise&lt;RealtimeMessage&lt;T&gt;&gt;</code> | extensions/sdk/collaboration.ts:49 | verified-on-some-hosts |
| `server.cron.register` | <code>server.cron.register(name: string, input?: unknown, intervalSeconds?: number): Promise&lt;CronRegistration&gt;</code> | extensions/sdk/server.ts:77 | verified-on-some-hosts |
| `server.cron.status` | <code>server.cron.status(name: string): Promise&lt;CronStatus&gt;</code> | extensions/sdk/server.ts:82 | verified-on-some-hosts |
| `push.sendToCurrentUser` | <code>push.sendToCurrentUser(value: { title: string; body: string; data?: Record&lt;string, string&gt; | undefined; }): Promise&lt;{ accepted: boolean; }&gt;</code> | extensions/sdk/platform.ts:96 | verified-on-some-hosts |
| `console.log` | <code>console.log(...values: unknown[]): void</code> | extensions/sdk/debug.ts:14 | verified-on-some-hosts |
| `console.warn` | <code>console.warn(...values: unknown[]): void</code> | extensions/sdk/debug.ts:15 | verified-on-some-hosts |
| `console.error` | <code>console.error(...values: unknown[]): void</code> | extensions/sdk/debug.ts:16 | verified-on-some-hosts |
| `debug.inspect` | <code>debug.inspect(value: unknown): string</code> | extensions/sdk/debug.ts:4 | verified-on-some-hosts |
| `analytics.event` | <code>analytics.event(name: string): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:340 | verified-on-some-hosts |
| `capabilities.has` | <code>capabilities.has(name: string): boolean</code> | extensions/sdk/host.ts:351 | verified-ios-android-mac |
| `permissions.status` | <code>permissions.status(permission: Permission): Promise&lt;PermissionStatus&gt;</code> | extensions/sdk/host.ts:42 | verified-ios-android-mac |
| `permissions.request` | <code>permissions.request(permission: Permission): Promise&lt;PermissionStatus&gt;</code> | extensions/sdk/host.ts:43 | verified-on-some-hosts |
| `permissions.openSettings` | <code>permissions.openSettings(): Promise&lt;void&gt;</code> | extensions/sdk/host.ts:44 | verified-on-some-hosts |

<!-- GENERATED PUBLIC API INDEX END -->
