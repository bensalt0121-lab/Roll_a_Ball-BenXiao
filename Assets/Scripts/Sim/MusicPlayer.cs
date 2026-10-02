/*********************************************************************************************
 * COMPONENT OF: Game Systems (made by the setup tool)
 * REQUIRED DEPENDENCIES: two AudioSources on the same object, SoundLibrary, WantedLevel,
 *                        music in Assets/Resources/Audio/Music/City, BlackMarket and Wanted,
 *                        city noise in Assets/Resources/Audio/City/Ambience
 * DESCRIPTION: Plays background music that fits what is happening: calm city music,
 *              sneaky music in the sewer black market, and chase music when the police
 *              are after you. Also plays quiet street noise in the city.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    [Header("Speakers")]
    public AudioSource music;
    public AudioSource ambience;

    [Header("Volume")]
    [Range(0f, 1f)] public float musicVolume = 0.35f;
    [Range(0f, 1f)] public float ambienceVolume = 0.25f;
    // Seconds to fade between songs
    public float fadeTime = 1.5f;

    [Header("When To Switch")]
    // Below this height the player is underground (the sewer black market)
    public float undergroundHeight = -10f;
    // Chase music plays at this many wanted stars or more
    public int starsForChaseMusic = 2;

    private Transform player;
    private string currentMood = "";
    private float fade = 1f;
    private string nextMood;

    void Start()
    {
        PlayerInteractor found = FindAnyObjectByType<PlayerInteractor>();
        player = found != null ? found.transform : null;
        StartAmbience();
    }

    void Update()
    {
        PickMood();
        FadeBetweenSongs();
        KeepPlaying();
    }

    // Decides which kind of music fits right now
    void PickMood()
    {
        string mood = "Music/City";

        if (player != null && player.position.y < undergroundHeight)
            mood = "Music/BlackMarket";
        else if (WantedLevel.Instance != null && WantedLevel.Instance.Stars >= starsForChaseMusic)
            mood = "Music/Wanted";

        if (mood != currentMood && mood != nextMood)
            nextMood = mood;

        if (ambience != null)
            ambience.volume = mood == "Music/BlackMarket" ? 0f : ambienceVolume;
    }

    // Fades the old song out, switches, then fades the new one in
    void FadeBetweenSongs()
    {
        if (music == null)
            return;

        float step = Time.deltaTime / Mathf.Max(0.01f, fadeTime);

        if (nextMood != null)
        {
            fade -= step;
            if (fade <= 0f)
            {
                fade = 0f;
                currentMood = nextMood;
                nextMood = null;
                PlaySongFor(currentMood);
            }
        }
        else
        {
            fade = Mathf.Min(1f, fade + step);
        }

        music.volume = musicVolume * fade;
    }

    // Starts another song from the same folder when one ends
    void KeepPlaying()
    {
        if (music != null && !music.isPlaying && nextMood == null && currentMood != "")
            PlaySongFor(currentMood);
    }

    void PlaySongFor(string mood)
    {
        AudioClip song = SoundLibrary.Random(mood);
        if (song == null && mood != "Music/City")
            song = SoundLibrary.Random("Music/City");

        music.clip = song;
        music.loop = false;
        if (song != null)
            music.Play();
    }

    void StartAmbience()
    {
        if (ambience == null)
            return;

        ambience.clip = SoundLibrary.Random("City/Ambience");
        ambience.loop = true;
        ambience.volume = ambienceVolume;
        if (ambience.clip != null)
            ambience.Play();
    }
}
