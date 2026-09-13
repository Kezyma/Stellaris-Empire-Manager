# Stellaris Empire Manager

Build Stellaris empires in a browser, with the game's own rules enforced and the game's own words
for what every choice does.

**[Open the designer →](https://kezyma.github.io/Stellaris-Empire-Manager/)**

Or [download the Windows app](#the-desktop-app), which opens your designs file itself and saves
straight back to it.

## Your file stays yours

There is no account to make here and no server of ours behind it. By default nothing is uploaded at
all: your designs file is read inside the browser tab and never leaves it, and Export hands it back
to you as a download.

The one exception is yours to switch on. If you keep your designs file in OneDrive, **Cloud** can
open it where it already sits, and then the site reads and writes that one file in your own
storage - see [Working from OneDrive](#working-from-onedrive). Nothing is sent anywhere until you
sign in and pick a file, and disconnecting ends it.

Your file lives here:

```
Documents\Paradox Interactive\Stellaris\user_empire_designs_v3.4.txt
```

If your Documents folder is redirected into OneDrive, look under your OneDrive folder instead.

## Getting started

Press **Import** in the header and choose that file. Everything in it appears as a card, with its
own flag, species and government.

The first time, the dialog opens at your Documents folder and you will have to find
`Paradox Interactive\Stellaris` yourself - a web page is not allowed to choose a folder for you.
After that your browser remembers, and Import and Export both open straight there.

You do not have to bring a file. **Create** starts an empty empire, and any of the game's own
empires can be opened and copied as a starting point.

## Content packs

The row of icons across the top decides what the lists offer. The site cannot tell which packs you
own, so it begins with all of them on. Turn off the ones you do not have and every list narrows to
match your game.

## The empire list

Your own empires come first, then the ones the game ships. Selecting a card shows it in the preview
beside the list: its flag, its ruler in their room, the world it starts on, everything it has taken
and what all of that adds up to.

From there, **Edit** opens it in the designer, and **Delete** removes it from your file.

## The designer

The choices run down the left, a section at a time - the empire's name, its species, its government,
origin, homeworld, ruler, flag, room, ships and advisor. The preview on the right updates as you go.

Every list only offers what the game would offer. Where something is unavailable it stays visible
and says why: the wrong ethics, a missing content pack, no room left. Anything that would stop the
game accepting your empire is reported as a problem in the preview, so a design that reads as
finished here is one the game will take.

## Getting it back into the game

Press **Export**. A save dialog opens where you last opened one - your Stellaris folder, after the
first time - with the right filename already in the box. Save over your designs file and you are
done.

**Close Stellaris first.** The game writes that file when it exits, so anything saved while it is
running will be overwritten the moment you quit.

Some browsers have no save dialog to offer, and there Export falls back to an ordinary download: the
file lands in Downloads and you move it yourself. The site says so when it happens.

Firefox and Safari have never had one. **Brave has one but ships it switched off**, which is easy to
mistake for a fault in the site - if Export downloads in Brave, that is why. To turn it on, open
`brave://flags/#file-system-access-api`, set it to Enabled and restart the browser. Chrome and Edge
need nothing.

Keep a copy of anything you would be sorry to lose. The site writes only the file you pick in that
dialog and nothing else, but it cannot keep the old version aside for you the way a desktop program
would.

## Working from OneDrive

Windows redirects Documents into OneDrive on a great many machines, which means the designs file is
often already there. Where it is, **Cloud** in the header opens it in place and Export becomes
**Save**: no dialog, no download, no moving a file out of Downloads afterwards.

Press **Cloud**, sign in to OneDrive, and browse to your file - it will be under
`Documents\Paradox Interactive\Stellaris` inside whichever folder OneDrive keeps for your PC. Tick
**Write my changes to it as I go** if you want saving to happen by itself. The site remembers the
file, so the next visit opens it without asking.

Two things worth knowing before you do:

- **OneDrive cannot grant access to one file.** Its permission screen asks for your files in
  general, because Microsoft offers nothing narrower to a personal account. This site only ever
  touches the file you pick; the grant is simply wider than the use. **Disconnect** ends it.
- **The desktop sees your changes when OneDrive brings them down**, which is seconds to minutes,
  not instantly. Editing the same file in two places at once is how you get the conflict copies
  OneDrive names after your PC.

If the file changes somewhere else while you are editing, nothing is written over it - the site says
so and leaves your work in hand.

## Sharing an empire

The link button in the designer copies a web address that carries the whole empire inside it. Anyone
who opens it sees your design without a file changing hands. It is only added to their own empires
if they press Save.

## What the browser remembers

Your file is kept in the browser between visits, so closing the tab does not cost you an evening's
work. It is not a backup: it belongs to that one browser on that one machine, and clearing site data
clears it. Export anything you would be sorry to lose.

## The desktop app

**[Download it for Windows →](https://github.com/Kezyma/Stellaris-Empire-Manager/releases/latest/download/StellarisEmpireManager-win-x64.zip)**

The same designer in a window, with the one difference that matters: it has your designs file. It
finds your Stellaris installation, opens the file on launch, and **Save** writes it back in place,
keeping a dated copy beside it. Nothing to import first, and nothing to export afterwards.

Windows 10 or 11, 64-bit. Unzip it anywhere and run `StellarisEmpireManager.exe` - there is nothing
else to install, since the .NET runtime is inside the download. The first start reads your game
files, which takes a minute, and remembers the result until the game is patched.

It can also write the file as you go, so the game and the designer can be open together: turn on the
tick beside Save. Something else writing the file - the game, usually, on the way out - is noticed,
and where taking it would cost you work you are asked what to do with it rather than told.

The download is always the newest build. There is no version to choose and no updater: the link
above is replaced every time the code changes.

There is no macOS or Linux build. The window is WPF, which is Windows only, and the site already
runs everywhere a browser does - a second desktop shell would be a second place to keep the same
promises about somebody's file, which is not a cost worth paying twice.
