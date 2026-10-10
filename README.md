
 ![OverlayExample-ezgif com-optimize](https://github.com/user-attachments/assets/c9dbebed-6fde-4854-94d0-91dc283837a0)

 # Spotify Lyrics Overlay
An overlay that shows the synced lyrics of the song currently playing on Spotify, on top of everything else on your screen.

<table>
  <tr>
    <td>
      <img src="https://github.com/user-attachments/assets/c1c08b51-c1df-477e-9951-af29156b98ba" alt="App Screenshot" width="400"/>
    </td>
    <td>
      <h3>The application supports:</h3>
      <ul>
        <li>Font, size and style (Bold, Italic and Drop shadow)</li>
        <li>Secondary font for lines the main font can't show (e.g. Thai lyrics with an English font)</li>
        <li>Text color, or a color picked from the album cover</li>
        <li>Background box with its own color, opacity and spread</li>
        <li>Line transitions: Legacy, Slide up, Crossfade, Scroll, Pop, Karaoke fill, Push sideways and Typewriter</li>
        <li>Monitor selection and X / Y offset (0, 0 is the center of the screen)</li>
        <li>Remember Start / Stop, so the overlay starts by itself when you open the app</li>
      </ul>
    </td>
  </tr>
</table>

Every setting updates the lyrics live.

# Multi language support
If the selected font does not support the language of a line, the secondary font is used for that line. With no secondary font set, Windows picks a default font.
![Still 2025-06-04 191303_4 1 1 (1)](https://github.com/user-attachments/assets/688c37cd-7329-44ed-b0cc-8f2c728915bc)

# Use case
Now you can sing along with your song while coding. A study found that this improves code quality by 150 percent.
![Screenshot 2025-06-04 183321](https://github.com/user-attachments/assets/28bd844c-b9e4-4879-b079-e0286593ef14)

#### Or in Balatro, increase rare joker chance on the first shop by naneinf%
![image](https://github.com/user-attachments/assets/de58758b-26cf-4dd2-af4b-d51f7bfe2ba8)

# How to use
Download `spotify-lyrics-overlay.exe` from the [latest release](https://github.com/WhySoDk/spotify-lyrics-overlay/releases/latest) and run it. Nothing else needs to be installed.

- The Spotify app only needs to be created once, after that you can reuse the same Client ID.
- If you already have a Client ID, start at step 6.
- If you checked `Remember Client ID`, start at step 7.

1. Go to the Spotify Developer Dashboard and create a new app:
   https://developer.spotify.com/dashboard/create

2. Set any App Name and App Description (these can be anything you like).

3. Under "Redirect URIs", add the following:
   `http://127.0.0.1:5543/callback`

4. Check the boxes for:
   - Web API
   - Web Playback SDK

5. After creating the app, copy the **Client ID**.

6. Paste the **Client ID** into the program.

7. Click "Start", the lyrics should now appear on your screen.

The settings, the Spotify login and the lyrics cache are saved next to the exe (`config.json`, `token.json` and `lyrics_cache`).

# Finding the lyrics
Lyrics come from [LRCLIB](https://lrclib.net/). For each song the app tries, in order:

1. The exact title, all artists and the song length
2. The same with only the main artist, as LRCLIB usually lists only that one
3. A search by the title without its version part (`(feat. ...)`, `- from the series ...`), picking a synced record with the same length (±2 seconds)

When LRCLIB's best match only has plain lyrics, the app still searches for a synced record of the same song. Songs whose title says they have no vocals, like `(Instrumental)`, `- Inst.`, `(Off Vocal)`, `(Karaoke Version)` or `(MR)`, are shown as instrumental without a lookup.

### Cache
| Result | Kept |
|---|---|
| Synced lyrics or instrumental | Forever |
| Plain lyrics only, or not found | 7 days, then looked up again |
| Lyrics you set yourself | Forever |

The album cover color has its own cache and is picked while the lyrics are still loading.

Lyrics, album colors and album covers are kept in one SQLite database, `lyrics_cache/cache.db`, which can be opened with e.g. [DB Browser for SQLite](https://sqlitebrowser.org/). The old cache (one `.json` file per song) is imported on the first start and moved to `lyrics_cache/imported_json`, imported songs get their name filled in the next time they play.

# Fixing wrong or missing lyrics
The **Lyrics** card has three buttons, they work on the song that is playing right now:

- **Refetch current song** forgets the cached lyrics and album color and looks both up again.
- **Set lyrics from lrclib link...** uses a LRCLIB record you picked, e.g. `https://lrclib.net/tracks/36084871`. Search for the song on [LRCLIB](https://lrclib.net/) and paste the link of the right one.
- **Paste synced lyrics...** uses LRC lyrics you paste, e.g. from [Lyricsify](https://www.lyricsify.com/). Tags like `[ar: Artist]` are skipped, each lyrics line needs a time like `[00:20.50]`.

Lyrics you set with the last two buttons are never replaced by a lookup. Use **Refetch current song** to go back to the automatic ones.

# Status messages
Shown for 5 seconds, then hidden.

| Case | Message |
|---|---|
| No lyrics found | No lyrics found |
| Lyrics not time synced | (Lyrics not sync) |
| Synced lyrics can't be read | (Can't parse lyrics format) |
| Instrumental song | (Instrumental) |
| Lyrics request failed | (Couldn't load lyrics) |
| Can't reach Spotify | (Can't connect to Spotify) |

### Debug info
Check `Show debug info` to see a small gray box above the lyrics telling what the lookup is doing, for 5 seconds each:

- the step being tried, e.g. `1/3 Simple query`, `2/3 Main artist query`, `3/3 Search by title`
- what was found, e.g. `Found synced lyrics`
- cached songs without synced lyrics and when they are looked up again, e.g. `Cache: not found, expires in 6d 5h`
- cache hits with the song name, e.g. `Cache hit: jupiter — almost monday` (these can be hidden with `Also show cache hits`)

# Build
Run `build.cmd` in the project root (needs the .NET 9 SDK). It builds a single exe into the release folder, your `config.json`, `token.json` and lyrics cache there are kept. Close the app first, a running exe can't be replaced.

Releases are built by GitHub Actions: push a version tag (e.g. `git tag v2.1.0 && git push origin v2.1.0`) and the exe is published as a release.

# Security concern
The Client ID is saved as plain text in `config.json` if `Remember Client ID` is checked. If you're concerned about security, keep it unchecked.

## Thanks
Special thanks to [LRCLIB](https://lrclib.net/) for providing a completely free service for finding and contributing synchronized lyrics, without LRCLIB, this project could not be built.❤️🙏
