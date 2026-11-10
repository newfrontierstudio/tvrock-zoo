# TVRock Zoo 1.2.8 検証

STUDIO 0.7.4の量子認知・取消後の再評価・上限集計・分秒入力をTVRock方式に移植。

- Releaseビルド：成功。自己完結型 .NET 8.0.32 / Windows x64。
- 量子計算：独立した複素密度行列計算との1,000比較、5,000状態更新を含む6,008チェック成功。配布用実行ファイルでも再確認。
- 推薦プロトコル：取消／無効の枠返却、同放送の復活防止、次回候補、明示的復元、放送終了後の取消履歴保持、結果不明の保留枠、期限切れ、UI操作と実行処理の競合、実際の選定順へのスコア適用を確認。
- 画面：量子認知ON/OFF・70%初期値・反映率保存・新規30件、分秒入力の符号と境界、録画調整ダイアログの表示を確認。曜日別・個別調整の優先を確認。
- 通信：読み取り再試行、予約変更を1回だけ送る動作、直列通信の模擬試験を通過。実予約は作成・変更していません。
- ローカル負荷測定：3,000番組/127条件の順位計算中央値654ms、500予約読み込み1.66ms、描画スクロール26.6ms。模擬データであり実環境の性能保証ではありません。
- 広範囲の旧画面試験は、既存の局色選択プレビュー試験で停止しました。今回の推薦設定・分秒入力は専用の画面試験で通過しています。
- 実際のTVRockへの接続・予約登録と実ユーザーでの推薦精度は未検証です。

アルゴリズム：quantum-cognition-v1。TVRock互換処理：tvrock-v1。
量子ハードウェアではなく量子確率の数学を用いた実験的な推薦モデルです。
STUDIO本体・受信設定・量子設定は更新前後のハッシュ一致を確認。Infernoも既存ファイルを継承します。

---

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
