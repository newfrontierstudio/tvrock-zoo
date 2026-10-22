# Dependencies

TVRock Zoo does not include TVRock or any tuner driver.

- .NET 8 / Windows Forms — Microsoft and contributors; MIT license. Runtime license and third-party notices are included in the binary distribution.
- Html Agility Pack 1.12.4 — ZZZ Projects and contributors; MIT license. See `licenses/HtmlAgilityPack-LICENSE.txt`.
- Microsoft.Web.WebView2 SDK 1.0.3485.44 — Microsoft Corporation; license in `licenses/WebView2-LICENSE.txt`. WebView2 Runtime is installed separately.

Package versions are pinned in project files. Third-party components retain their respective licenses. TVRock Zoo itself is licensed under the MIT License; see LICENSE.

## Media tools (separate processes)

FFmpeg, FFprobe and mpv are redistributed as unmodified external executables. FFmpeg/FFprobe perform media analysis and AV1 conversion; mpv provides FFmpeg-based embedded playback. Their GPL licenses and corresponding-source/build links are included under `MediaTools/licenses` and `MediaTools/SOURCES.txt`. See `scripts/Prepare-MediaTools.ps1` for the pinned archive URLs and SHA256 verification. The media tools are not linked into the application.
