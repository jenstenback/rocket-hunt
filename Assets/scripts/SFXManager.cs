using UnityEngine;

/* ==========================================================================================
 * SCRIPT: SFXManager.cs
 * DOEL: Procedurele Audio Synthesizer voor Sci-Fi geluidseffecten.
 * UITLEG VOOR EXAMEN/PRESENTATIE:
 * Om te voorkomen dat de game honderden megabytes aan externe .MP3 of .WAV bestanden
 * moet laden, berekent dit script alle geluiden wiskundig via PCM float-arrays.
 * Door sinusgolven (Mathf.Sin) te moduleren met frequentie-lerps genereren we in RAM
 * direct piepjes, lasers, akkoorden en explosies. Dit maakt de game extreem lichtgewicht!
 * ========================================================================================== */

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    private AudioSource audioSource;
    private AudioClip laserClip;
    private AudioClip hitClip;
    private AudioClip headshotClip;
    private AudioClip pickupClip;
    private AudioClip explosionClip;
    private AudioClip winClip;
    private AudioClip loseClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void AutoInit()
    {
        GameObject go = new GameObject("SFXManager_Global");
        go.AddComponent<SFXManager>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D geluid overal hoorbaar
        audioSource.volume = 1f;
        audioSource.ignoreListenerPause = true; // Speel ook geluid af als het spel gepauzeerd is

        // Zorg altijd voor een AudioListener in de game
        if (FindAnyObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }

        // Genereer alle Sci-Fi arcade geluiden volledig procedureel in het geheugen!
        laserClip = GenerateLaser();
        hitClip = GenerateHit();
        headshotClip = GenerateHeadshot();
        pickupClip = GeneratePickup();
        explosionClip = GenerateExplosion();
        winClip = GenerateWin();
        loseClip = GenerateLose();
    }

    void Update()
    {
        if (FindAnyObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
        }
    }

    public void PlayLaser() { if (laserClip) audioSource.PlayOneShot(laserClip, 0.6f); }
    public void PlayHit(bool isHeadshot) { if (isHeadshot && headshotClip) audioSource.PlayOneShot(headshotClip, 0.9f); else if (hitClip) audioSource.PlayOneShot(hitClip, 0.8f); }
    public void PlayPickup() { if (pickupClip) audioSource.PlayOneShot(pickupClip, 0.9f); }
    public void PlayExplosion() { if (explosionClip) audioSource.PlayOneShot(explosionClip, 1f); }
    public void PlayAlienDeath() { if (explosionClip) audioSource.PlayOneShot(explosionClip, 0.8f); }
    public void PlayWin() { if (winClip) audioSource.PlayOneShot(winClip, 1f); }
    public void PlayLose() { if (loseClip) audioSource.PlayOneShot(loseClip, 1f); }

    private int GetRate()
    {
        return AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 44100;
    }

    private AudioClip GenerateLaser()
    {
        int rate = GetRate();
        int count = (int)(rate * 0.14f);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = Mathf.Lerp(880f, 150f, t);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * (1f - t);
        }
        AudioClip clip = AudioClip.Create("Laser", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateHit()
    {
        int rate = GetRate();
        int count = (int)(rate * 0.05f);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            data[i] = Random.Range(-1f, 1f) * (1f - t);
        }
        AudioClip clip = AudioClip.Create("Hit", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateHeadshot()
    {
        int rate = GetRate();
        int count = (int)(rate * 0.25f);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float f1 = 1760f;
            float f2 = 3520f;
            data[i] = (Mathf.Sin(2f * Mathf.PI * f1 * i / rate) * 0.6f + Mathf.Sin(2f * Mathf.PI * f2 * i / rate) * 0.4f) * (1f - t);
        }
        AudioClip clip = AudioClip.Create("Headshot", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GeneratePickup()
    {
        int rate = GetRate();
        int count = (int)(rate * 0.2f);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = t < 0.5f ? 523.25f : 659.25f; // C5 naar E5
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * (1f - t);
        }
        AudioClip clip = AudioClip.Create("Pickup", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateExplosion()
    {
        int rate = GetRate();
        int count = (int)(rate * 0.4f);
        float[] data = new float[count];
        float currentVal = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            if (i % (rate / 2200 + 1) == 0) currentVal = Random.Range(-1f, 1f);
            data[i] = currentVal * (1f - t);
        }
        AudioClip clip = AudioClip.Create("Explosion", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateWin()
    {
        int rate = GetRate();
        int count = (int)(rate * 0.8f);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = t < 0.25f ? 440f : (t < 0.5f ? 554.37f : 659.25f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * (1f - t);
        }
        AudioClip clip = AudioClip.Create("Win", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private AudioClip GenerateLose()
    {
        int rate = GetRate();
        int count = (int)(rate * 0.8f);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float freq = Mathf.Lerp(440f, 220f, t);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * (1f - t);
        }
        AudioClip clip = AudioClip.Create("Lose", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
