# TVRock Zoo 1.1.1 validation

- Local UI compatibility tests cover guide text-only hover, blank-area click/double-click retention, edge wheel day navigation and burst suppression.
- Candidate-cache regression covers reuse with unchanged guide/rules, new UI rule objects, keyword edits, new guide content, end-time expiry and cancellation. Shared candidate code is used by both Zoo and Studio.
- Local tests use synthetic data and do not write production TVRock reservations.
- Recording and EPG acquisition remain owned by TVRock. This update does not migrate TVTest architecture or touch Studio receiver overrides.
- Recorded-media preview and AV1 conversion are restored. FFmpeg/FFprobe/mpv are a separate optional pack, not application-installer payload.
- Update notification and optional broadcast-symbol display correction default OFF. The latter formats existing Unicode display marks; it is not a new raw ARIB or subtitle decoder.
- CPU savings vary by programme count, keywords and update activity. No fixed percentage reduction or all-environment stability guarantee is claimed.

### Studioフィードバック・予約入力の追加検証

Windowsのマウスメッセージからタイトルのダブルクリックで1回の予約要求が発生すること、詳細ポップアップがHTTRANSPARENTを返すことを確認。合成TVRockデータでタイトル更新、別イベント・放送時間変更の拒否、送信後の保存確認、応答消失と自動再送防止を確認。本番TVRockへの新規予約は行っていません。共通ソースの範囲はSHARED-UI.mdに記載。

## 動画追加パックとビルド

合成TSでFFprobe、ピクセルアスペクト比、サムネイル、AV1/MP4変換、元TS保持、出力の重複回避、中止時の一時ファイル削除を確認。両アプリの埋め込み再生とCtrl/Shift選択を確認。追加パックの検証・導入・同じ部品の再利用・破損パック拒否を確認。未使用タブが監視を開始しないことを確認しました。

変更なしの通常ローカルビルドの一例はStudio約1.7秒、Zoo約2.6秒。コード変更・初回ビルド・テスト・インストーラー圧縮・追加パック作成は別時間であり、一定時間を保証する数値ではありません。


2026-10-23 Inferno連携版
- Zoo 1.1.3／Studio 0.3.3 self-contained publish成功（.NET 8.0.32）。
- 両アプリの録画済みから同梱inferno.exeの別ウィンドウ起動を確認。
- 録画終了ファイルのボタン有効、録画中のボタン無効、既存色の維持を確認。
- 両アプリの録画済みCtrl／Shift複数選択テスト成功。
- Inferno 0.5.2の順逆移動、方向切替、離して停止のテスト成功。短いTSで低速逆方向初動181ms。長尺録画での応答時間・実マウス操作の保証値ではありません。
- 今回チューナー・CAS・録画予約設定の変更と実放送録画ベンチは実施していません。64bit版TVTest対応の開発凍結は維持。
