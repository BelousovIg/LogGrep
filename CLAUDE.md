# LogGrep

A WPF desktop app that slices a World of Warcraft combat log into per-pull files.
See `README.md` for what it does and how the parser reads the log.

## Finishing work

**Commit and push when a piece of work is done. Do not wait to be asked.**

- **Commit as the repository owner alone.** The identity is already set locally on this clone
  (`Igor Belousov <60176208+BelousovIg@users.noreply.github.com>` — local config, not global), so
  a plain `git commit` is enough. Never add `Co-Authored-By` or any other attribution trailer.
- **Push to `origin main` over SSH.** Git Extensions sets `GIT_SSH` only inside its own process, so
  a terminal has to point Git at plink itself, otherwise Git falls back to its own OpenSSH, which
  cannot see the key in Pageant and fails with `Permission denied (publickey)`:

  ```powershell
  $env:GIT_SSH = 'C:\Program Files\PuTTY\plink.exe'   # bash: export GIT_SSH="/c/Program Files/PuTTY/plink.exe"
  git push origin main
  ```

  The key itself lives in Pageant, which Git Extensions starts.
- **"Done" means checked, not just written.** Build first, and say plainly what was verified and
  what was not — the UI is WPF, so anything that needs a mouse cannot be confirmed from here.

## Building and checking

```powershell
dotnet build src/LogGrep/LogGrep.csproj
```

Parser changes deserve more than a build. The app takes a log path on the command line, and there
are real logs in `~/Downloads` (`WoWCombatLog-*.txt`) to check against — the full one is ~1.4 GB and
scans in about 9 seconds, and the per-encounter exports next to it are ~60 MB each. For anything
that needs to read the results rather than look at them, a throwaway console project in the
scratchpad directory that references `src/LogGrep/LogGrep.csproj` can call the scanner and the view
models directly; delete it once the answer is in.
