# EggInc.tools

Front door for a set of Egg, Inc. fan tools. Lists every tool, shows the version each one is running, and
renders the shared header those tools embed so four separate origins read as one site.

| Tool | Where | State |
|---|---|---|
| EggLedger | https://eggledger.egginc.tools | live |
| EggIncognito | https://eggincognito.egginc.tools | live |
| EggAbacus | https://eggabacus.egginc.tools | not released |
| EggIdentity | https://id.egginc.tools | the shared sign-in, not listed on the hub |

Each tool is its own origin and its own deployment. The hub reads the URL and state of every tool from the
EggIdentity settings database, so a tool moves host without a hub release.

Sign-in is shared once the session cookie is scoped to `.egginc.tools`. Until the identity host moves off
`eggidentity.davidarthurcole.me`, that cookie does not reach the hub. See `docs/handoff.md`.

`EggIncTools.Shell` is published to NuGet. Tools reference it and drop `<ToolsHeader Active="slug"/>`
into their layout.

Independent, fan-made, and not affiliated with, endorsed by, or sponsored by Auxbrain Inc. Egg, Inc. and
related marks are the property of their respective owners.
