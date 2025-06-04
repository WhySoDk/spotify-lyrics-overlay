
 ![OverlayExample-ezgif com-optimize](https://github.com/user-attachments/assets/c9dbebed-6fde-4854-94d0-91dc283837a0)

 # Spotify Lyrics Overlay
An overlay that displays the lyrics based on the song currently playing on Spotify.

<table>
  <tr>
    <td>
      <img src="https://github.com/user-attachments/assets/c1c08b51-c1df-477e-9951-af29156b98ba" alt="App Screenshot" width="400"/>
    </td>
    <td>
      <h3>The Application supports:</h3>
      <ul>
        <li>Font selection</li>
        <li>Font size control</li>
        <li>Style control (Bold, Italic, and Drop shadow)</li>
        <li>Color selection</li>
        <li>Monitor selection</li>
        <li>X and Y offset (0,0 is at the center of the screen)</li>
      </ul>
    </td>

  </tr>
</table>

Any configuration will update the lyrics live.

# Multi Language support
If the selected font does not support the shown language, the default font will be used.
![Still 2025-06-04 191303_4 1 1 (1)](https://github.com/user-attachments/assets/688c37cd-7329-44ed-b0cc-8f2c728915bc)

# Use case
Now you can sing along with your song while coding. A study found that this improves code quality by 150 percent.
![Screenshot 2025-06-04 183321](https://github.com/user-attachments/assets/28bd844c-b9e4-4879-b079-e0286593ef14)

#### Or in Balatro, increase rare joker chance on the first shop by naneinf%
![image](https://github.com/user-attachments/assets/de58758b-26cf-4dd2-af4b-d51f7bfe2ba8)

# How to use
The program executable (.exe) is in the release folder. The program should run when you click on it.
- The Spotify application is only need to create once, after that you can reuse th same Client ID.
- If you already have Client ID, you can start at step 6.
- And if you use remember Client ID, you can start at step 7.
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

7. Click "Start" — the lyrics should now appear on your screen.


# Security Concern
The Client ID will be saved as a string in a JSON file if `Remember Client Id` is checked. If you're concerned about security, please be aware of this.

## Thanks
Special thanks to [LRCLIB](https://lrclib.net/) for providing a completely free service for finding and contributing synchronized lyrics, without LRCLIB, this project could not be built.❤️🙏

