/*********************************************************************************************
 * COMPONENT OF: (helper, not a component) used by MusicPlayer, TrafficCar, PoliceCar,
 *               PlayerWeapon
 * REQUIRED DEPENDENCIES: sound files in Assets/Resources/Audio/<folder>
 * DESCRIPTION: Finds sounds by folder name, for example SoundLibrary.Random("Cars/Horn").
 *              To add your own music or sounds, just drop .mp3/.ogg/.wav files into the
 *              matching folder in Assets/Resources/Audio - no other setup needed.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.Collections.Generic;
using UnityEngine;

public static class SoundLibrary
{
    private static readonly Dictionary<string, AudioClip[]> loaded = new Dictionary<string, AudioClip[]>();

    // Every clip in Assets/Resources/Audio/<folder>
    public static AudioClip[] All(string folder)
    {
        if (!loaded.TryGetValue(folder, out AudioClip[] clips))
        {
            clips = Resources.LoadAll<AudioClip>("Audio/" + folder);
            loaded[folder] = clips;
        }
        return clips;
    }

    // One random clip from the folder, or null if the folder is empty
    public static AudioClip Random(string folder)
    {
        AudioClip[] clips = All(folder);
        return clips.Length == 0 ? null : clips[UnityEngine.Random.Range(0, clips.Length)];
    }
}
