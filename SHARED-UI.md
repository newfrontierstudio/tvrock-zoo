# Studio / Zoo の改善を共有する方針

ZooはTVRockで録画・EPG取得を行う互換フロントエンド、Studioは独立した録画管理です。予約データの保存先と録画エンジンはそれぞれの構成を保ちます。

Studio側のプロジェクトからZooの以下のソースを直接参照し、片方だけへの修正を防ぎます。

- ScheduleGrid / GuideReservationMarker / ScheduleDragVisuals: 番組表のクリック、文字上の詳細、日付移動、局の並べ替え。
- FlatHoverCard / ProgramHoverLayout: 詳細ポップアップの表示と入力の通過。
- KeywordCandidates: 検索候補の再利用、局名照合の削減。
- GuideReservationTransaction: TVRockへ登録する場合の番組照合、送信と保存結果の確認。Studio独立録画ではStudio側の予約処理を使います。
- GuideBookingIndex / GuideSearchWord / BackGesture / GestureOptions / LatestInformation / RerunPreference / RecordingIndicator: 共通表示・操作。

変更時はZooのtests-uiとStudioのidle-search/themeテストを実行します。ダブルクリックはイベントを直接呼ぶテストだけでなくWindowsのマウスメッセージで確認します。TVRock登録は合成データで送信・保存結果・応答消失・再送しない動作を確認します。本番TVRockへの実登録は別の受け入れ確認であり、このテストで検証済みとは扱いません。

Zooへの改善反映時も、Studioの受信環境・TVTestの32/64bit設定を変更しません。
