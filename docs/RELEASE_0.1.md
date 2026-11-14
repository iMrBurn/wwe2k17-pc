# What 0.1 still needs

Short list, most important first. `VERSION` stays `0.0.1-dev` until the first two blocks pass.

## Blocking: never tested

These are the ones that would make a release pointless if they are broken. All need someone at the keyboard.

- [ ] **Saves round-trip.** The game has written a `SaveData.Dat` (4.5 MB, `userdata\B13EBABEBABEBABE\545408BA\00000001\`),
      so writing works. Nobody has checked that it is *read back*. Change a setting or create something, quit
      from the EXIT menu, relaunch, and confirm it is still there.
- [ ] **Creation suite.** What WWE players use most, and it exercises a lot of code at once.
- [ ] **MyCAREER** and the rest of **Universe** (the Story Collection is done and working): do they start, do
      they survive a few minutes.
- [ ] **A real match from the menus on each of the four exes.** One has been played on `wwe2k17_native.exe`;
      the other three have only run the attract loop.

Doing all of the above also fills `userdata\cache\pipelines.bin` and `declarations.bin` with everything those
screens need, which is exactly what the shipped seed should contain. So the testing *is* the preparation.

## Then: packaging

- [ ] `tools\package.ps1 -Zip` once the caches above are full. It copies the four exes, the launcher, the two
      seed caches, and refuses to finish if any game file ends up in the folder.
- [ ] **The launcher must pick the exe.** `WWE2K17 Launcher.exe` exists but the four renderers are chosen with
      `$env:WWE2K17_EXE` today. This belongs to the launcher session, not to this one.
- [ ] Bump `VERSION` to `0.1.0` and say in the README what this release is.

## Known and accepted for 0.1

- Movies are not quite smooth with the native renderers (about a third of a movie's frames held a render frame
  too long or short). Measured, five causes ruled out, best remaining lead written up in `PORTING_PLAN.md`:
  the native build replaces the game's own `D3DDevice_Swap`, the Xenos build does not. Visible but harmless.
- AMD GPUs are untested here and will stay that way until someone with one tries it. Say so plainly.
- Loading times cannot be measured until WWE 2K15 PC's `rexglue-sdk-iostats.patch` is applied to `patches\sdk`
  (`GetFileIoStats` is stubbed, so every `[load]` line reads 0.0 MB).

## Done

- Universe: the Story Collection reads as fully unlocked, tested in game.
- Everything the renderer knows about is prepared at startup: 0 pipelines compiled mid-game, against 9 before
  the vertex declarations were remembered.
- Texture builds are 7x cheaper (0.63 ms -> 0.09 ms each; worst second 110 ms -> 20 ms of render thread).
- The README says what works, what does not, and what every option does.
