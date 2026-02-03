using System.IO;
using UnityEngine;

public class VoiceRecognizerLite : MonoBehaviour
{
    public AudioInputRealtime audio;

    public PlayerMovement playerMovement;
    
    private VoiceTemplateLite jump = new();
    private VoiceTemplateLite turn = new();

    public float frameThreshold = 18f;
    public float cooldown = 0.25f;

    const int MFCC_DIM = 6;

    int jumpIndex;
    int turnIndex;

    bool locked;
    float unlockTime;

    void Awake()
    {
        Load();
    }

    void Start()
    {
        audio.OnWindow += OnWindow;
    }

    void OnDisable()
    {
        audio.OnWindow -= OnWindow;
    }

    void Load()
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            "voice_templates_lite.json"
        );

        if (!File.Exists(path))
        {
            Debug.LogError("No voice templates found");
            return;
        }

        var data = JsonUtility.FromJson<VoiceTemplateDataLite>(
            File.ReadAllText(path)
        );

        jump.windows.Clear();
        turn.windows.Clear();

        foreach (var f in data.jump)
            jump.windows.Add(f);

        foreach (var f in data.turn)
            turn.windows.Add(f);

        Debug.Log(
            $"Templates loaded | jump={jump.windows.Count}, turn={turn.windows.Count}"
        );
    }

    void OnWindow(float[] mfcc)
    {
        if (locked && Time.time < unlockTime)
            return;

        Check(ref jumpIndex, jump, mfcc, OnJump);
        Check(ref turnIndex, turn, mfcc, OnTurn);
    }

    void Check(
        ref int index,
        VoiceTemplateLite tpl,
        float[] input,
        System.Action fire
    )
    {
        if (!tpl.IsComplete)
            return;

        float dist = FrameDist(input, tpl.windows[index].ToArray());


        if (dist < frameThreshold)
        {
            index++;

            if (index >= 3)
            {
                Fire(fire);
            }
        }
        else
        {
            index = 0;
        }
    }

    void Fire(System.Action action)
    {
        locked = true;
        unlockTime = Time.time + cooldown;

        jumpIndex = 0;
        turnIndex = 0;

        action();
    }

    float FrameDist(float[] a, float[] b)
    {
        float sum = 0f;

        for (int i = 0; i < MFCC_DIM; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }

        return Mathf.Sqrt(sum);
    }

    void OnJump()
    {
        Debug.Log("JUMP 🔥");
        playerMovement?.RequestJump();
    }

    void OnTurn()
    {
        Debug.Log("TURN 🔥");
        playerMovement?.RequestTurn();
    }
}
