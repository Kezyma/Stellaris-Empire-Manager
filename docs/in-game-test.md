# Checking a design in the game

Automated tests prove the file is written correctly. Only Stellaris can prove it accepts what was
written. Run this after any change to how designs are read or written, and before a release.

## Before you start

Take a copy of your designs file. The desktop app keeps its own archive under
`%LocalAppData%\StellarisEmpireManager\archive`, but a copy you made yourself is one you can find
in a hurry.

```
C:\Users\<you>\Documents\Paradox Interactive\Stellaris\user_empire_designs_v3.4.txt
```

If Documents is redirected to OneDrive, it is under your OneDrive folder instead. The app finds it
either way, through the path the game's launcher declares.

Stellaris must not be running. It writes this file on exit and would overwrite the test.

## The test

1. **Open an existing empire.** Every empire you already had should be listed, with its own flag
   and species, and none should be reported as invalid. An empire the game accepts that this app
   rejects is a bug in the rules, not in your empire.

2. **Save without changing anything.** The file should be byte-identical afterwards. Compare it:

   ```bash
   git hash-object user_empire_designs_v3.4.txt
   ```

   Any difference here means the writer is not preserving what it read, which would eventually
   damage something.

3. **Change one field and save.** Alter an empire's authority, then compare against the copy you
   took. Exactly one line should differ. More than that means formatting is being disturbed.

4. **Build a new empire.** Give it a species, a portrait, ethics, an authority, two civics, an
   origin and a homeworld, and check the summary reports no problems. Save.

5. **Start the game.** In the empire creation screen, your new empire should appear in the list
   with the right name, flag and portrait, and every choice should be as you left it.

   The flag is worth a second look. A two-tone background should show both colours, not two shades
   of one, and the emblem should be its own artwork rather than a flat silhouette. If you set a map
   colour, the empire's territory should take it once the game starts; the fourth stored colour does
   nothing in the game and should still be `"null"` in the file.

6. **Check the log.** `Documents\Paradox Interactive\Stellaris\logs\error.log` should contain
   nothing about empire designs. Warnings there mean the game read something it did not like even
   if it did not complain on screen.

7. **Start a game with it.** The empire should be playable, on the homeworld and in the system you
   chose, with the traits and civics you gave it.

8. **Quit and compare once more.** The game rewrites the file on exit. Open it in this app again:
   it should still load, still validate, and still round-trip. This is what catches the app and
   the game disagreeing about the format.

## If something fails

Note which step, keep the file that caused it, and add it to the round-trip fixtures. A real file
that broke something is worth more than any test written from imagination.
