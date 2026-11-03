# CIT Hub 拡張機能の現行仕様

CIT Hub の公開拡張機能は HTML Web App だけを受け付けます。

- manifest は `schemaVersion: 2`、`ui.mode: "web"`、`content: []`、`webApp` を必須とします。
- HTML、CSS、JavaScript は `webApp.files["index.html"]` に含めます。外部 script/style/image、iframe、fetch、WebSocket、eval、Function、dynamic import は使用できません。
- CIT Hub の本人データや端末機能は `window.CITHub.call(operation, args)` で呼び出します。
- 旧 TypeScript/QuickJS/native UI SDK（`defineProgram`、`runtime`、native `content`）は廃止済みです。旧形式のアップロードは `native_ui_removed` または `web_app_required` で拒否されます。
- ビルド例: `node extensions/kit/build-web-app.mjs app/index.html --metadata app/metadata.json --out dist/app.json`

旧形式の既存データは実行されず、Web App形式への更新が必要です。
