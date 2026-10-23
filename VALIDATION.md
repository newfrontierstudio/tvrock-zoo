# TVRock Zoo 1.1 validation

- Local UI compatibility tests cover guide text-only hover, blank-area click/double-click retention, edge wheel day navigation and burst suppression.
- Candidate-cache regression covers reuse with unchanged guide/rules, new UI rule objects, keyword edits, new guide content, end-time expiry and cancellation. Shared candidate code is used by both Zoo and Studio.
- Local tests use synthetic data and do not write production TVRock reservations.
- Recording and EPG acquisition remain owned by TVRock. This update does not migrate TVTest architecture or touch Studio receiver overrides.
- The recorded-media tab, media source from the active app, and FFmpeg/FFprobe/mpv distribution are removed from this version. No playback/encoding performance claims apply to 1.1.
- Update notification and optional broadcast-symbol display correction default OFF. The latter formats existing Unicode display marks; it is not a new raw ARIB or subtitle decoder.
- CPU savings vary by programme count, keywords and update activity. No fixed percentage reduction or all-environment stability guarantee is claimed.

### Studioフィードバック・予約入力の追加検証

Windowsのマウスメッセージからタイトルのダブルクリックで1回の予約要求が発生すること、詳細ポップアップがHTTRANSPARENTを返すことを確認。合成TVRockデータでタイトル更新、別イベント・放送時間変更の拒否、送信後の保存確認、応答消失と自動再送防止を確認。本番TVRockへの新規予約は行っていません。共通ソースの範囲はSHARED-UI.mdに記載。
