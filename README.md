# TVRock Zoo

TVRockの予約・番組表を、見やすく使いやすくするWindowsアプリです。録画と番組表の取得はTVRockが担当します。

**[最新版をダウンロード](https://github.com/newfrontierstudio/tvrock-zoo/releases/latest)** · [更新履歴](RELEASE-NOTES.md)

## 主な機能

- 番組表からダブルクリックで予約。空きチューナーを自動選択し、手動選択にも対応。
- 予約の取り消し、録画開始・終了時間の調整。
- 検索条件や予約履歴を参考にした、おすすめ予約。ChatGPTへの相談も任意で利用可能。
- 録画済み一覧のプレビュー再生とMP4（AV1）変換。元の動画は残します。
- 同梱のInfernoで、不要な部分をカットして書き出し。
- 局の色・文字サイズ・詳細ポップアップなどをカスタマイズ。

## はじめ方

Windows 10/11（64ビット）と、設定済みのTVRock・録画環境が必要です。

1. インストーラーを実行します。.NETは同梱されています。
2. 初回設定でTVRockのフォルダーとWeb URLを指定します。
3. 番組表を読み込み、使い始めます。

別PCへの接続は、詳細設定でURLと読み取り可能な共有フォルダーを指定します。接続できない場合も、この設定を確認してください。

更新はZooを終了し、上書きインストールします。保存済みの設定を引き継ぎます。

## 動画機能

初回は[動画追加パック](https://github.com/newfrontierstudio/tvrock-zoo/releases/download/v1.1.1/Zoo-MediaTools-x64-20261021.zip)を展開し、「録画済み」→「再生・変換の部品を追加」で中の`Tools`フォルダーを選びます。部品はStudioと共用できます。

AV1は画質を保ちながら容量を抑えられる動画形式です。変換には対応GPUまたはCPUを使用します。録画中のファイルは編集・変換対象外です。音声はAAC、放送字幕・データ放送は変換対象外です。

## 1.2.1の更新

Studioの改善を反映し、予約操作・詳細設定・スクロール操作を見直しました。録画時間の調整画面にBonDriver名を表示し、TsRenameとの連携設定も分かりやすくしました。Inferno 0.8.2を同梱しています。

## 設定・お問い合わせ

設定は`%LOCALAPPDATA%\TVRockZoo`に保存します。アンインストールしても、録画ファイル・予約・設定とTVRock・TVTestは残ります。

詳しい使い方はアプリ内のHELPへ。不具合は「不具合を報告」から、内容を確認して非公開で送信できます。[報告フォーム](https://zoo-support-report.stonyray64.chatgpt.site/)も利用できます。

## 開発・ライセンス

[MITライセンス](LICENSE)でソース公開。[依存ライブラリ](THIRD-PARTY-NOTICES.md) · [検証結果](BENCHMARKS.md)

.NET 8 SDKで`scripts/build.ps1`を実行してビルドします。インストーラー作成にはInno Setup 6と`-Installer`指定が必要です。

[☕ 開発を応援する（note）](https://note.com/discreet/n/n3e2ee058b3bd)
