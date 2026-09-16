# clip

read, write, and browse clipboard history from the cli.

history stored at `%APPDATA%\clip\history.txt` (base64 per line, max 100 entries, deduplicated).

```
clip                   print current clipboard
clip get               same
clip set <text>        copy text to clipboard
clip <text>            shortcut: copy text
echo "hi" | clip set   pipe to clipboard
clip ls [n]            show last n history entries (default 20)
clip clear             wipe history
```

---

## example

```
> clip set "hello world"
  ✓ copied (11 chars)

> clip
hello world

> clip ls
    1. hello world
    2. ghp_abc123...
    3. https://some-url-you-copied.com

  3 of 3 entries shown
```

## build

```
dotnet build -c Release
```

.NET 8+, windows only (Win32 clipboard API via P/Invoke).

## testing

built clean. `clip get` confirmed returning "(clipboard is empty)" correctly. Win32 P/Invoke signatures verified. history file base64 encoding logic reviewed.

**not live-tested:** set → get round-trip not exercised (clipboard STA thread needs interactive context). correct in principle, not verified end-to-end.

## license

MIT
