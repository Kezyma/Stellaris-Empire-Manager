# Registering the app with a cloud provider

What you have to do by hand before the cloud feature can work, for each provider, and why the thing
you end up committing into a public repository is safe to commit.

## Is it safe to put an app identifier in a public site?

Yes, and it is worth understanding why, because the instinct that it is not is a good one.

**The client ID is not a credential.** OAuth divides applications into *confidential* clients, which
run on a server and can keep a secret, and *public* clients, which run on someone else's machine and
cannot. A site published to GitHub Pages is a public client by definition: anything shipped to the
browser is readable by everyone who loads it, and minifying or encoding it changes nothing. So the
specification does not ask a public client to hold a secret. It asks it to prove itself two other
ways instead.

**The redirect URI is the first.** Every provider keeps a list of addresses it is willing to send an
authorisation response to, and you write that list. If somebody copies our client ID into
`https://not-us.example`, the provider looks up the ID, sees that address is not on its list, and
refuses before anything is issued. Since RFC 9700 (January 2025) this is required to be exact string
matching with no wildcards, which is why the steps below are fussy about registering the precise
addresses and nothing broader.

**PKCE is the second.** Before sending anyone to sign in, the app invents a random secret, keeps it,
and sends only a hash of it. The provider remembers the hash and will not exchange the resulting
code for a token unless it is handed the original. So a code intercepted in transit is inert: the
interceptor has the hash, which is the half that does not work.

Between them, a stolen client ID buys an attacker the ability to send a user to a real Microsoft
sign-in page that refuses to come back to them. That is the whole attack.

### What is actually worth worrying about

- **Script running on our own origin.** This is the real risk and the only serious one. Anything that
  can execute on our page can read whatever the page can, token included - so the defence is the
  Content-Security-Policy in `src/Sem.Web/wwwroot/index.html` and the escaping tests in
  `tests/Sem.Ui.Tests/LocalizerTests.cs`, both of which exist already, and both of which were written
  before any token did.
- **A loose redirect list.** Wildcards, or a registered address with an open redirect on it, hand an
  attacker the one thing the allowlist was stopping. Register exact addresses, and remove the
  localhost ones from the production registration if you ever split the two.
- **The implicit flow.** An older OAuth mode that returns the token in the URL itself, where it lands
  in history and in referrers. Do not enable it; authorisation code with PKCE is what all three
  providers below now recommend.
- **The client *secret*.** Every provider will also show you one. It has no use in a browser app and
  must never be committed or shipped. If you paste one anywhere near this repository, rotate it.

### What goes where

| Value | Public? | Where it lives |
|---|---|---|
| Client ID / App key | Yes, by design | Committed in the repo, served in the page |
| Google API key (for the picker) | Yes, if restricted to our origins | Committed, restricted in the console |
| Client secret / App secret | **No** | Nowhere. Not in the repo, not in the site, not in a build variable |
| Access and refresh tokens | **No** | The browser's own storage, at runtime, never committed |

---

## OneDrive (Microsoft)

The first provider, because Windows commonly redirects Documents into OneDrive, which is where
Stellaris keeps the designs file.

1. Sign in at **https://portal.azure.com** with the Microsoft account you want to own the
   registration, and open **Microsoft Entra ID** → **App registrations** → **New registration**.
2. **Name:** `Stellaris Empire Manager`. This is what the consent screen shows the player, so it is
   worth getting right.
3. **Supported account types:** *Personal Microsoft accounts only*, unless you also want people
   signing in with work or school accounts, in which case choose the option that names both.
4. **Redirect URI:** choose the platform **Single-page application (SPA)** from the dropdown. This
   matters more than anything else on the page. The *Web* platform expects a client secret at the
   token endpoint and will not send the CORS headers a browser needs, so choosing it produces a
   sign-in that appears to work and then fails at the last step.
5. Enter `https://kezyma.github.io/Stellaris-Empire-Manager/` and register.
6. Open **Authentication** and add a second SPA redirect URI, `http://localhost:5155/`, so the dev
   server can sign in too. Leave *Access tokens* and *ID tokens* (implicit flow) **unticked**.
7. Open **API permissions** → **Add a permission** → **Microsoft Graph** → **Delegated permissions**,
   and add **`Files.ReadWrite`** and **`offline_access`**. The second is what lets the session be
   renewed quietly instead of asking the player to sign in again every hour.
8. Copy the **Application (client) ID** from the Overview page. That is the value the app needs.

**Authority:** `https://login.microsoftonline.com/consumers` for personal accounts only, or
`/common` if you enabled both kinds at step 3.

**Be aware:** Microsoft offers no per-file consent for personal accounts - `Files.Read.Selected` and
`Files.ReadWrite.Selected` are work-and-school only and are not for calling Graph. So the consent
screen will say *"Have full access to your files"* even though the app only ever touches the one
file the player points it at. There is no way to ask for less, and it should be said plainly in the
UI rather than hoped past.

---

## Google Drive

Better behaved than OneDrive on consent, and worse on reach: the designs file is only in Drive if
somebody mirrors Documents with Drive for Desktop.

1. At **https://console.cloud.google.com**, create a project - `Stellaris Empire Manager`.
2. **APIs & Services** → **Library**, and enable both **Google Drive API** and **Google Picker API**.
3. **OAuth consent screen** → **External**. Fill in the app name, a support email and a developer
   contact email. The app name is what the player sees.
4. Add the scope **`https://www.googleapis.com/auth/drive.file`** and nothing else. This is the one
   that matters: `drive.file` is classed non-sensitive and needs no verification from Google, and it
   grants access only to files the user picks through the Picker or that the app itself created.
   `drive` and `drive.readonly` are *restricted*, and asking for either drags the project into a
   verification process with a security assessment.
5. **Publish** the app. Left in *Testing* it is capped at 100 users and refresh tokens expire after
   seven days, which reads as a bug to anyone using it.
6. **Credentials** → **Create credentials** → **OAuth client ID** → **Web application**.
7. **Authorised JavaScript origins:** `https://kezyma.github.io` and `http://localhost:5155`.
   **Authorised redirect URIs:** `https://kezyma.github.io/Stellaris-Empire-Manager/` and
   `http://localhost:5155/`. Origins have no path; redirect URIs do.
8. **Create credentials** → **API key** as well - the Picker needs one. Then **restrict** it: under
   *Application restrictions* choose *Websites* and list the two origins, and under *API
   restrictions* limit it to the Google Picker API.
9. Copy the **Client ID** and the **API key**.

---

## Dropbox

Worth registering only if you want it, because it is the weakest fit of the three.

1. At **https://www.dropbox.com/developers/apps**, choose **Create app**.
2. **Choose an API:** *Scoped access*.
3. **Choose the type of access:** *Full Dropbox*. An app folder cannot see a designs file that
   already exists elsewhere in the account, so the app-folder option cannot do what this feature is.
4. Name the app and create it.
5. On **Settings**, under **OAuth 2 → Redirect URIs**, add `https://kezyma.github.io/Stellaris-Empire-Manager/`
   and `http://localhost:5155/`.
6. On **Permissions**, tick `files.metadata.read`, `files.content.read` and `files.content.write`,
   then **Submit**. Permissions have to be submitted before they take effect, and an app that was
   authorised before you changed them keeps the old set until the player signs in again.
7. Copy the **App key** from Settings. Ignore the App secret entirely.

**Be aware:** Dropbox's own guidance for a pure-JavaScript app is short-lived access tokens with
PKCE and **no refresh token**, so a session lasts about four hours and then needs signing in again.
That is a real difference from the other two, not a detail.

---

## The three compared

| | OneDrive | Google Drive | Dropbox |
|---|---|---|---|
| Reaches an existing designs file | Yes | Yes, via the Picker | Yes |
| Consent needed for it | All files | That one file | All files |
| Provider verification | None | None with `drive.file` | None |
| Session survives quietly | Yes, with `offline_access` | Yes, once published | No - about four hours |
| Likely to already hold the file | **Yes** - Windows redirects Documents | Only with Drive for Desktop | Only if deliberately moved |

---

## After registering

The client IDs go into the repository as ordinary configuration, committed like any other constant.
Nothing above needs to be hidden, and nothing above should be pasted into a chat, an issue or a
commit message **except** the client IDs and the restricted API key - if you find yourself copying
a value labelled *secret*, stop.
