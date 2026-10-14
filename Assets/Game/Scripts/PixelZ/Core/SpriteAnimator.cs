using UnityEngine;
using UnityEngine.UI;

// Reproduce una lista de sprites a un ritmo fijo. Sirve tanto para SpriteRenderer como para Image (menú).
public class SpriteAnimator : MonoBehaviour
{
    public SpriteRenderer target;
    public Image uiTarget;
    public bool unscaledTime;

    private Sprite[] frames;
    private float fps;
    private bool loop;
    private float time;

    public int Frame { get; private set; }
    public bool IsDone { get; private set; }
    public Sprite[] Current => frames;

    public void Play(Sprite[] newFrames, float newFps, bool newLoop, bool restart = false)
    {
        if (newFrames == null || newFrames.Length == 0) return;
        fps = newFps;
        if (!restart && newFrames == frames) return;

        frames = newFrames;
        loop = newLoop;
        time = 0f;
        Frame = 0;
        IsDone = false;
        Apply();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || IsDone) return;

        time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        int f = (int)(time * fps);
        if (loop) f %= frames.Length;
        else if (f >= frames.Length)
        {
            f = frames.Length - 1;
            IsDone = true;
        }

        if (f != Frame)
        {
            Frame = f;
            Apply();
        }
    }

    private void Apply()
    {
        Sprite s = frames[Frame];
        if (target != null) target.sprite = s;
        if (uiTarget != null) uiTarget.sprite = s;
    }
}
