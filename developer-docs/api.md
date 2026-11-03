# CIT Hub Web App API

> CIT Hub拡張機能の画面はローカルHTML Web Appで作成します。

## 1. manifest

必須の形は次のとおりです。

```json
{
  "schemaVersion": 2,
  "name": "Study timer",
  "version": "1.0.0",
  "description": "A local study timer.",
  "permissions": ["timetable.read"],
  "ui": { "mode": "web" },
  "content": [],
  "webApp": {
    "entry": "index.html",
    "entrypoint": "index.html",
    "files": { "index.html": "<!doctype html>..." },
    "contentHash": "sha256-..."
  }
}
```

`content`は空配列にし、`ui.mode`を`web`にします。`webApp.files.index.html`がエントリーポイントです。

## 2. 作成とビルド

HTMLにCSSとJavaScriptをインラインで記載し、次のコマンドでJSONを生成します。

```sh
npm install
npm run build:web -- examples/web-app-study-timer.html \
  --metadata examples/web-app-study-timer.json \
  --out dist/study-timer.json
```

ビルダーは`schemaVersion: 2`、`ui.mode: "web"`、`content: []`、SHA-256済みの`webApp.files`を生成します。個別ファイルは64個以内、1ファイル512KiB以内、合計6MiB以内です。`index.html`がエントリーポイントです。

## 3. HTMLの安全境界

ローカルWebViewで実行される未信頼コードとして検査されます。次は禁止です。

- 外部スクリプト、外部CSS、外部画像、iframe、任意URL遷移
- `fetch`、`XMLHttpRequest`、WebSocket、EventSource、Service Worker
- `eval`、`Function`、動的`import`、任意のネイティブコード
- Portal/Manabaのパスワード、OTP秘密鍵、Cookie、セッショントークンの取得
- 他ユーザーのデータや管理者データの取得

画面は標準DOM、CSS、SVG、Canvas、Web Animationsで作成できます。Claude生成では、セマンティックHTML、ライト/ダーク対応、レスポンシブ配置、アクセシビリティ、`prefers-reduced-motion`を必須とします。

## 4. CIT Hubブリッジ

HTMLからCIT Hubの機能を使う唯一の入口は`window.CITHub`です。

```js
const host = await window.CITHub.ready();
const today = await window.CITHub.call('cit.timetable.today', {});
const stop = window.CITHub.on('resume', () => refresh());
```

`ready()`はホスト情報を返します。`on()`は解除関数を返します。`call()`は権限、ユーザー操作、端末Capability、ホストAPIバージョンを検査してから実行します。エラーは`{code, message}`として返るため、必ず`try/catch`で画面に表示してください。

### 本人データ

| operation | 必要な権限 | 用途 |
|---|---|---|
| `cit.timetable.today` / `tomorrow` / `week` | `timetable.read` | 今日・明日・週の時間割 |
| `cit.timetable.current` / `next` | `timetable.read` | 授業中・次の授業 |
| `cit.courses.list` / `get` / `search` | `timetable.read` | 履修授業 |
| `cit.assignments.list` / `pending` / `overdue` / `dueSoon` | `assignments.read` | 課題状況 |
| `cit.todo.list` | `todos.read` | ToDo |
| `cit.courseNotes.list` / `get` | `courseNotes.read` | 授業メモ |
| `cit.calendar.events` / `holidays` | `calendar.read` | 学年暦 |
| `cit.bus.next` / `timetable` | `bus.read` | バスダイヤ |
| `cit.cafeteria.menu` / `hours` | `cafeteria.read` | 食堂情報 |

### 端末機能

| operation | 必要な権限 | 条件 |
|---|---|---|
| `permissions.status` / `permissions.request` | 対象権限 | OS権限の確認・要求。ユーザー操作が必要 |
| `camera.capturePhoto` | `camera.use`, `files.selection.read` | カメラ起動はユーザー操作から実行 |
| `photos.save` | `photos.user`, `files.selection.read` | 確認後に写真へ保存 |
| `audio.record` / `audio.stopRecording` | `microphone.use`, `files.selection.read` | 録音はユーザー操作から実行 |
| `sensors.motion` | `sensors.motion` | 端末が対応するセンサーだけ |
| `device.info` | なし | OS・端末Capabilityの限定情報 |
| `browser.openExternal` | `browser.open` | HTTPS URLのみ。ユーザー操作が必要 |
| `notification.schedule` | `notifications.schedule` | 通知権限とユーザー操作が必要 |

### ローカル保存

| operation | 必要な権限 | 用途 |
|---|---|---|
| `storage.get` / `set` / `has` / `remove` / `keys` | なし | 拡張機能・ユーザーごとの端末内保存 |
| `secureStorage.get` / `set` / `delete` | なし | 端末の保護領域への保存 |
| `files.read` / `write` / `delete` / `list` | `files.storage` | 拡張機能専用領域 |
| `files.selection.pick` / `read` | `files.user`, `files.selection.read` | ユーザーが選択したファイル |
| `files.export` / `saveAs` | `files.storage` | ユーザー操作による書き出し |

### 拡張機能サーバー

| operation | 必要な権限 | 用途 |
|---|---|---|
| `server.function` | `server.functions` | 審査済み関数の限定実行 |
| `server.cron.register` / `unregister` / `status` | `server.functions` | サーバー上のベストエフォート定期処理 |
| `server.files.*` | `cloud.storage` | ユーザー・拡張機能単位の暗号化ファイル |
| `server.db.*` / `server.kv.*` | `cloud.storage` | ユーザー単位のサーバー保存 |

サーバー関数は端末上で実行されず、ユーザーのパスワード、OTP、Cookie、セッショントークンは渡されません。管理者データや他ユーザーのデータは取得できません。

## 5. ライフサイクル

`window.CITHub.on()`で`resume`、`suspend`、`appear`、`disappear`、`openURL`、`notification`、`networkChange`を購読できます。画面を離れるときは解除関数を呼び、非同期処理の結果が破棄済み画面へ反映されないようにしてください。

## 6. 審査と更新

審査ではmanifest、HTML、権限、外部通信、データ範囲、エラー処理を検査します。審査前の下書きは作成者の端末だけで動作確認できます。公開版は承認後に配布されます。更新版もWeb App形式で提出します。

## 7. エラー

代表的なエラーコードは`permission_denied`、`user_action_required`、`unsupported`、`network_error`、`not_found`、`rate_limited`、`storage_error`、`validation_error`、`host_update_required`です。エラーコードを画面へ表示し、再試行・権限設定・ユーザー操作など次の手順を案内してください。

詳細なoperation、引数、戻り値、対応OSは、このAPIリファレンスと配布されるSDKの`API.md`を参照してください。
