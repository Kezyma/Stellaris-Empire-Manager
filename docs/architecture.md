# How this is put together

Twelve projects, two hosts, and one rule that keeps them apart. This page is the map: what each
project is for, which way the arrows point, and why three decisions that look odd are the right ones.

## The projects

| Project | Lines | What it is |
|---|---:|---|
| `Sem.Clausewitz` | 1,082 | A parser and writer for Paradox's own file format. Knows nothing about Stellaris. |
| `Sem.GameData` | 3,168 | The shape of an extracted installation: `GameDatabase` and the forty-seven kinds of thing it holds. |
| `Sem.Designs` | 2,510 | An empire design as the game stores it - views over a parsed block, so a field this app does not know about survives a round trip. |
| `Sem.Rules` | 4,094 | What the game allows. What may be chosen, what it costs, why something is unavailable, and what a finished design gets wrong. |
| `Sem.Io` | 1,141 | Every write in the app goes through here, against a policy that names the folders it may touch. |
| `Sem.Assets` | 637 | Decoding the game's textures and writing PNGs. |
| `Sem.MeshBake` | 1,512 | Turning the game's meshes into flat pictures. |
| `Sem.Extraction` | 9,753 | Reading an installation and producing the database, the text and the artwork. |
| `Sem.Ui` | 30,518 | The designer and the wiki: every component, and the services behind them. |
| `Sem.Cli` | 919 | The extractor as a command, which is how the committed data is made. |
| `Sem.Web` | 88 | The browser host. A `Program.cs` and nothing else. |
| `Sem.Desktop` | 1,421 | The WPF host: finds the game, extracts if it must, and shows the same designer in an embedded browser. |

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

## Keeping a file and an app in step

Both hosts make the same promise about somebody's designs file, by different means:

> **Nothing is written over a change this app has not seen.**

What enforces it is a **baseline** - what the file held the last time this app read or wrote it -
and the rule that only a read or a write may move it. Merely *noticing* that the file moved does
not: a change that was seen and then never dealt with must not license the next write to go
straight over it.

|  | `Sem.Desktop` | The cloud exchange |
|---|---|---|
| The baseline | The bytes themselves | The provider's version stamp |
| Noticing a change | A folder watcher | Polling, paced by whether anybody is looking |
| Refusing a write | Reads the file back and compares before replacing it | Hands the stamp over as `If-Match` and lets the provider refuse |

The mechanisms differ; the answer does not. Either way the write comes back
`SaveOutcome.Conflicted`, which is **not a failure to save** - it is a refusal to overwrite
somebody, and the thing to do about it is look at what arrived.

### The one question, asked in one voice

Four answers, everywhere the two sides can disagree - importing a file, connecting to one, resuming
a connection, the watcher noticing one change, and a save that was refused:

- **Keep Current** - your empires stand; what arrived becomes the thing they will be written over.
- **← Merge** / **Merge →** - both, differing only in which copy of an empire you both have is kept.
- **Take File** / **Take Cloud** - theirs, and your unsaved edits go with it.

`Arrival` and `ArrivalQuestion` are the vocabulary and `ArrivalChoices` is the row of buttons, so
the question reads the same wherever it is met. **Every answer moves the baseline**, including the
ones that keep your own - that is what stops the same change being asked about twice, and what makes
each answer go on meaning what it said.

### Where a refusal goes

`DesignSync.ReconcileAsync` is what a refused write ends in. It reads the file *directly* rather
than through `LoadFromDiskAsync`, which returns at a guard when writing-as-you-go is switched off -
so the question is asked whether or not the switch is on. Reading is also what lifts the refusal,
since a read is exactly what the baseline records: by the time an answer is pressed the write is
allowed again, and the write that follows the answer is the save that was asked for.

`SessionHost.Reconcile` is the hook that reaches it, and `SessionHost.Deferred` is how a caller
tells "saved" from "waiting on an answer" - both look like the absence of an error message
otherwise, and one caller closes the editor on it.

## Running it while you work

`.claude/launch.json` starts the site on `http://localhost:5155` under `dotnet watch`, so it rebuilds
and restarts itself whenever anything in the project graph changes - including `Sem.Ui`, which is
where nearly all of the work happens. Leave it running and reload the page.

It runs with `--no-hot-reload`, and that is the whole point of the entry rather than an oversight.
Hot reload patches the assemblies in the running process and does not rewrite the ones the server
hands out, so a Blazor WebAssembly page that is reloaded fetches the last full build and silently
loses the change - the page goes *backwards* while the terminal says the edit was applied in 790ms.
Restarting on every change costs about twenty-five seconds and is always the truth.

It builds Debug, which keeps it out of the way of `dotnet build -c Release` and `dotnet test -c Release`:
different `bin` and `obj` subtrees, so the two can run at once and neither disturbs the other.

`--launch-profile http` is what puts it on 5155 and sets `ASPNETCORE_ENVIRONMENT=Development`. The
port is not arbitrary - it is registered as a redirect URI with both cloud providers, so moving it
breaks signing in locally. See [cloud-setup.md](cloud-setup.md).

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

## The wiki, and where its data comes from

`/wiki/civics`, `/wiki/origins`, `/wiki/ethics`, `/wiki/authorities`, `/wiki/species`,
`/wiki/species-traits` and `/wiki/leader-traits` show every one of those the game defines, including
the ones no player can ever take.

The shelves are one component and one row type. They are very different records and a reader asks
the same things of each - what is it, what does it do, who may have it, what does it cost in packs -
so `WikiRow` carries that much and `WikiRow.Facts` carries whatever a kind has that the others do
not: an ethic's cost and opposite, an authority's elections and heir, a leader trait's tier. A field
per kind would leave every row three-quarters empty and give each page a reason to know about the
others.

A column is drawn only where the rows fill it. The ethics are why: the game gates them on nothing at
all, so Playable would say Yes seventeen times and Pack would be blank, and both would take width
from the columns that do say something.

Six of the seven need nothing extracted. `gamedb.json` already carries all 358 civics and all 364
species traits, unfiltered - `EmpireOptions` narrows them at the point of use, not at extraction - so
what those pages need beyond the records is worked out at runtime in `Sem.Ui/Services`: `CivicReach`
says whether a player could ever be offered one, `ContentPacks` says which packs gate it, and the
description is the `_desc` convention every option chip already reads by.

The alternative was a property or two on `CivicDefinition`, which reads better and costs a schema
bump - and a schema bump sends every desktop player back through thirty-five thousand files to learn
something the file they already have could have told them. **Anything the database can answer should
be asked of it rather than added to it.**

### When the database cannot answer: wiki packs

The leader traits are the first thing the wiki wants that an empire designer has no use for. There
are 728 of them, nothing in an empire can hold one, and they were deliberately discarded at
extraction for years on exactly the reasoning above.

So they are not in the database. They are in `gamedata/wiki/leader-traits.json`, written by
`GameDataWriter` beside `gamedb.json`, and fetched through `IGameDataSource.LoadWikiPackAsync` the
first time somebody opens the page. `gamedb.json` does not grow, its schema does not move, and a
visitor who never opens that page never fetches it.

This is `wardrobe.json` generalised. That file has worked this way since the ruler got a figure, for
the same reason stated on the same interface: kept apart and fetched only when something asks. What
is new is that there will be a great many - planets, shipsets, AI personalities, traditions,
ascension perks, anomalies, archaeology sites, astral rifts, buildings, research, edicts, policies,
systems, events, situations, enclaves, empires - so the loader is keyed by domain rather than a
field and a gate per file.

Two things about a pack that are not obvious:

- **It carries its own text.** `loc/en.json` is pruned to what the database reaches, and a pack is by
  definition about things the database does not carry - so its names are pruned away and always
  would be. Not one leader trait has a name in that file. `LocalisationPruner.Slice` gives a pack the
  slice it needs, expanded through the same `$key$` following, and the shelf reads it through a
  `Localizer` of its own so a name written as `$leader_trait_archaeologist$ II` resolves.
- **It states the shape it is in**, refused on a mismatch the way a stale `gamedb.json` already is. A
  schema number per domain, so a change to one does not re-version the rest.

On the desktop the same files are written into the cache directory, and `GameDataCache.IsUsable`
checks each one exists - a cache built before a domain existed passes every other test and would
never be rebuilt, leaving that page permanently empty on the machine with nothing to say why.

The filter machinery is shared rather than copied. `Facet<TRow>` and `Sifter<TRow>` are what the
empire list's own headings and narrowing are built from, and `FilterCard`, `SearchBox` and
`AskedToggle` are generic in the row for the same reason.

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
