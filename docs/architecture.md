# How this is put together

Twelve projects, two hosts, and one rule that keeps them apart. This page is the map: what each
project is for, which way the arrows point, and why three decisions that look odd are the right ones.

## The projects

| Project | Lines | What it is |
|---|---:|---|
| `Sem.Clausewitz` | 1,070 | A parser and writer for Paradox's own file format. Knows nothing about Stellaris. |
| `Sem.GameData` | 3,149 | The shape of an extracted installation: `GameDatabase` and the forty-seven kinds of thing it holds. |
| `Sem.Designs` | 2,466 | An empire design as the game stores it - views over a parsed block, so a field this app does not know about survives a round trip. |
| `Sem.Rules` | 3,999 | What the game allows. What may be chosen, what it costs, why something is unavailable, and what a finished design gets wrong. |
| `Sem.Io` | 815 | Every write in the app goes through here, against a policy that names the folders it may touch. |
| `Sem.Assets` | 600 | Decoding the game's textures and writing PNGs. |
| `Sem.MeshBake` | 1,491 | Turning the game's meshes into flat pictures. |
| `Sem.Extraction` | 9,541 | Reading an installation and producing the database, the text and the artwork. |
| `Sem.Ui` | 26,888 | The designer itself: every component, and the services behind them. |
| `Sem.Cli` | 936 | The extractor as a command, which is how the committed data is made. |
| `Sem.Web` | 79 | The browser host. A `Program.cs` and nothing else. |
| `Sem.Desktop` | 890 | The WPF host: finds the game, extracts if it must, and shows the same designer in an embedded browser. |

## The one rule

**`Sem.Ui` must stay browser-safe.** It references `Sem.Clausewitz`, `Sem.Designs`, `Sem.GameData`
and `Sem.Rules`, and it must never reference `Sem.Extraction`, `Sem.Io`, `Sem.Assets` or
`Sem.MeshBake`. Those read files, decode textures and rasterise meshes; none of that exists in a
WebAssembly tab, and a reference to any of them would be found at publish time rather than at compile
time.

Everything else about the graph follows from that. It is acyclic, and the layers run:

```
        Sem.Clausewitz          Sem.Assets
              |                   |     |
        Sem.Designs        Sem.MeshBake |
              |   \              |      |
              |    Sem.GameData  |      |
              |        |         |      |
            Sem.Rules  |         |      |
                |      |         |      |
    Sem.Ui <----+------+      Sem.Extraction ----> Sem.Io
      |   \                        |     |
  Sem.Web  Sem.Desktop ------------+-----+
                |
             Sem.Cli (extraction, from the command line)
```

`Sem.Desktop` is the only project that reaches both sides: it is the host that extracts *and*
displays, which is the whole of what makes it different from the web.

## The two hosts

Both run the same designer. What differs is where the data comes from and where the file goes, and
those are the only two services either host must register for itself - `AddSemDesigner` registers
the rest for both.

|  | `Sem.Web` | `Sem.Desktop` |
|---|---|---|
| Game data | Fetched over HTTP from the published site | Read off disk, from a cache it extracted itself |
| Images | Fetched from the site | Served through a WebView2 virtual host |
| The designs file | A save dialog; the player chooses. Or one file at OneDrive, once connected to | The player's real file, replaced in place after a question |
| Between visits | Kept in browser storage, and the cloud file reopened where one was chosen | Not kept - the player's file is the copy that counts |
| Content packs | All of them assumed | Only the ones actually installed |

Two consequences worth knowing before changing either.

**The desktop cannot fetch its own data over HTTP.** It maps a hostname with
`SetVirtualHostNameToFolderMapping`, and that is the embedded browser intercepting requests its own
renderer makes. A managed `HttpClient` in the host process never goes near it. Data is read from
disk; only images, which the *page* fetches, go through the mapping.

**The web cannot write anywhere but a dialog, or a file it has been connected to.** Everything else
is the same designer, so a change that assumes a file system will compile and then fail in a tab.

Where a save goes is therefore no longer fixed for the visit. `Sem.Web` registers a
`FileExchangeRouter` as its one `IFileExchange` and everything downstream keys off that interface -
whether Save is called Save or Export, whether replacing the file asks first, whether the sync
switch does anything. Connecting swaps what the router forwards to and raises an event; nothing
else is rebuilt. See [cloud-setup.md](cloud-setup.md) for the registration a provider needs, and
why a public client can commit its client id.

## Why the extracted data is committed

`src/Sem.Web/wwwroot/gamedata` is 218 MB of extracted database, text and artwork, and it is in the
repository. That is deliberate, and it is not a cache.

Extraction needs a Stellaris installation. No build runner has one, so the site cannot be built from
source the way a normal project can - the data has to arrive some other way, and committing it is the
only way that survives a clean checkout. The desktop is the exception that proves it: that host *has*
an installation, so it extracts for itself and never reads the committed copy.

The consequence is that re-extracting is a commit, not a build step. `build-site.cmd` is the routine
that does it properly: extract, build, test, and rehearse the real Pages publish including the
`.nojekyll` check.

## Where the safety rules live

Every write goes through `SafeFile` against a `WritePolicy` that names the folders it may touch, and
the default policy can reach neither the game installation nor the player's saves. The desktop grants
exactly one extra folder, the one its designs file is in, and nothing else. See
[file-safety.md](file-safety.md).

## The other pages here

- [cloud-setup.md](cloud-setup.md) - registering the app with a provider, and why the identifier it
  commits is not a credential.
- [empire-flags.md](empire-flags.md) - the two unrelated things the game calls a flag.
- [flag-colours.md](flag-colours.md) - what each of the six colour slots actually paints.
- [hidden-content.md](hidden-content.md) - what the game defines but never offers.
- [in-game-test.md](in-game-test.md) - checking a design against the game itself.
