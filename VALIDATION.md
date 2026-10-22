# TVRock Zoo 1.0.31 validation

- Local UI compatibility tests passed (no live TVRock reservation writes).
- Synthetic 3000-program guide drawing median 19.1ms, 500-reservation read median 1.65ms. These exclude network/TVRock response time and are not an end-to-end speed claim.
- New media tab shares the same source with Studio. Synthetic TS -> MP4/AV1 conversion passed on CPU and RTX5060 NVENC. Verified codec, dimensions, frame rate, duration, original-file preservation, collision avoidance and cancellation cleanup.
- Embedded mpv decoded the video within the 320px preview host; IPC control and leaving-tab cleanup passed.
- Media tool archives are pinned and hash checked; the installer bundles FFmpeg, FFprobe, mpv and notices/source links.
- AMD/Intel real hardware encoding, remote recording-folder performance and exhaustive long recordings remain unverified.
