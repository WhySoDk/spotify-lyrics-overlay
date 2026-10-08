<img src="https://github.com/WhySoDk/largeGif/blob/main/Timeline.gif" />

 # Spotify Lyrics Overlay
An overlay that shows the synced lyrics of the song currently playing on Spotify, on top of everything else on your screen.

<table>
  <tr>
    <td>
      <img width="755" height="857" alt="image" src="https://github.com/user-attachments/assets/5979fb11-7f9f-42c9-8621-0620f6c55991"
"/>
    </td>
    <td>
      <h3>The application supports:</h3>
      <ul>
        <li>Font, size and style (Bold, Italic and Drop shadow)</li>
        <li>Secondary font for lines the main font can't show </li>
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
<img width="1263" height="251" alt="en" src="https://github.com/user-attachments/assets/f31cc070-2b6e-473d-8718-bed95edd2161" />
<img width="1263" height="251" alt="th" src="https://github.com/user-attachments/assets/0be52a99-fa71-41f7-a13c-a780e46ff947" />
<img width="1263" height="250" alt="jp" src="https://github.com/user-attachments/assets/26c3f14e-d0a3-4770-bc01-65e72abb5e0d" />
<img width="1263" height="251" alt="kr" src="https://github.com/user-attachments/assets/8970ac19-0a37-4702-8ffd-d26366ae85cb" />



# Use case
Now you can sing along with your song while coding. A study found that this improves code quality by 150 percent.
<img width="2559" height="1439" alt="Screenshot 2026-10-09 020425" src="https://github.com/user-attachments/assets/48e513bc-decd-4ba1-acdf-736d644bb26a" />


#### Or in Balatro, increase rare joker chance on the first shop by naneinf%
<img width="2559" height="1439" alt="Screenshot 2026-10-09 015953" src="https://github.com/user-attachments/assets/92932372-5677-4946-887c-a01390d34a28" />

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

# Fixing wrong or missing lyrics
The **Lyrics** card has three buttons, they work on the song that is playing right now:

- **Refetch current song** forgets the cached lyrics and album color and looks both up again.
- **Set lyrics from lrclib link...** uses a LRCLIB record you picked, e.g. `https://lrclib.net/tracks/36084871`. Search for the song on [LRCLIB](https://lrclib.net/) and paste the link of the right one.
- **Paste synced lyrics...** uses LRC lyrics you paste, e.g. from [Lyricsify](https://www.lyricsify.com/). 
Lyrics you set with the last two buttons are never replaced by a lookup. Use **Refetch current song** to go back to the automatic ones.

# Status messages
Shown for 5 seconds, then hidden. (If debug is checked)

| Case | Message |
|---|---|
| No lyrics found | No lyrics found |
| Lyrics not time synced | (Lyrics not sync) |
| Synced lyrics can't be read | (Can't parse lyrics format) |
| Instrumental song | (Instrumental) |
| Lyrics request failed | (Couldn't load lyrics) |
| Can't reach Spotify | (Can't connect to Spotify) |

# Lyrics caching
| Result | Kept |
|---|---|
| Synced lyrics or instrumental | Forever |
| Lyrics manually set  | Forever |
| Unsynced lyrics, or not found | 7 days|

# Security concern
The Client ID is saved as plain text in `config.json` if `Remember Client ID` is checked. If you're concerned about security, keep it unchecked.

## Thanks
Special thanks to [LRCLIB](https://lrclib.net/) for providing a completely free service for finding and contributing synchronized lyrics, without LRCLIB, this project could not be built.❤️🙏
