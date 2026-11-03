# CIT Hub 拡張機能の作成

CIT Hub拡張機能はHTML Web Appとして作成します。HTML・CSS・JavaScriptを使って画面を作り、CIT Hubの本人データや端末機能は `window.CITHub.call(operation, args)` から利用します。

## 開発の流れ

1. HTMLを作成する
2. SDKのビルドツールでmanifest JSONを生成する
3. 開発者コンソールから提出する
4. 自分の端末で動作確認する
5. 審査後に公開する
