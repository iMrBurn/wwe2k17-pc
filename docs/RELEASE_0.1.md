# What 0.1 still needs

Short list, most important first. `VERSION` stays `0.0.1-dev` until the first two blocks pass.

## Blocking: never tested

These are the ones that would make a release pointless if they are broken. All need someone at the keyboard.

- [x] **Saves round-trip.** Done 2026-11-15 on `wwe2k17_native.exe`: the user changed something, quit from the
      EXIT menu, and the game rewrote `SaveData.Dat` (`userdata\B13EBABEBABEBABE\545408BA\00000001\`, 4,709,917 ->
      4,709,925 bytes, different content) and closed cleanly (`EXIT confirmed` ... `Execution complete`). After
      relaunching, the user confirmed the change was still there. Only one setting and one exe so far: a save
      with real content (a created wrestler, a Universe in progress) and the other three exes are not covered.
- [~] **Creation suite.** What WWE players use most, and it exercises a lot of code at once. First pass
      2026-11-15 on `wwe2k17_native.exe`: the user created an attire for AJ Styles, saw it in the editor, in a
      match and on the entrance, with **no texture bug** (he had them in WWE '12 and 2K15). The game wrote
      `00CustomSuperStar.cas` (3.3 MB) and `SaveData.Dat` (4,710,719 bytes) during the session; CAWs work.
      **Closed 2026-11-17: not a bug.** The Xenos emulation build (`wwe2k17.exe`) shows the same silhouette on
      the same screen, so it is the game's own placeholder for a created superstar (the select screen's renders are
      pre-made images, and a CAW has none). Original note: **on the superstar select screen the character's render is a black silhouette** ("SUPERSTAR /
      OVERALL 79" over a dark figure). No warning or rejected texture in the log. Not yet known whether it hits
      every wrestler or only the edited one, nor whether `wwe2k17.exe` (Xenos) shows it. Next: F9 on that
      screen (draw log of the frame), and the same screen on the Xenos build. The user says the silhouette is CAW-only
      (normal superstars render fine). That the created attire is still there after a relaunch was **not
      checked**: the user chose to accept it as fine on 2026-11-15, and the game did write `00CustomSuperStar.cas`.
- [x] **MyCAREER** and the rest of **Universe** (played by the user 2026-11-17) (the Story Collection is done and working): do they start, do
      they survive a few minutes.
- [x] **A real match from the menus on each of the four exes.** Played by the user on 2026-11-17 (after the
      performance changes of that day, which touch all four exes).

Doing all of the above also fills `userdata\cache\pipelines.bin` and `declarations.bin` with everything those
screens need, which is exactly what the shipped seed should contain. So the testing *is* the preparation.

## Then: packaging

- [ ] `tools\package.ps1 -Zip` once the caches above are full. It copies the four exes, the launcher, the two
      seed caches, and refuses to finish if any game file ends up in the folder.
- [x] **The launcher picks the exe.** Done in 46f7a8f (2026-11-13): "NATIF · VULKAN / DIRECT3D 12 / DIRECT3D 11"
      and "ÉMULATION XENOS". Its Vulkan description still says full-screen movies are a little jerky, which the
      plane-delay fix has since improved: to reword in the launcher's own work.
- [x] Bump `VERSION` to `0.1.0` and say in the README what this release is.

## Known and accepted for 0.1

- Movies in the native renderers: luma and chroma planes did not reach the screen together (one frame in five); fixed by showing a movie's planes one frame late (`svr_movie_plane_delay`), user-verified "cleaner" on the arena preview on 2026-11-15. Not yet looked at by eye: the transitions and the intro. The game's own frame picks stay slightly uneven in every build.
- AMD GPUs are untested here and will stay that way until someone with one tries it. Say so plainly.
- Loading times cannot be measured until WWE 2K15 PC's `rexglue-sdk-iostats.patch` is applied to `patches\sdk`
  (`GetFileIoStats` is stubbed, so every `[load]` line reads 0.0 MB).

## Done

- Universe: the Story Collection reads as fully unlocked, tested in game.
- Everything the renderer knows about is prepared at startup: 0 pipelines compiled mid-game, against 9 before
  the vertex declarations were remembered.
- Texture builds are 7x cheaper (0.63 ms -> 0.09 ms each; worst second 110 ms -> 20 ms of render thread).
- The README says what works, what does not, and what every option does.
