using UnityEngine;

/// <summary>
/// Cheap "is alive" polish for the blocky agent placeholder:
///   1. Jaw flap driven by AudioSource amplitude (RMS of GetOutputData).
///   2. Head LookAt the camera, smoothed and clamped to a forward arc.
///   3. Idle breathing on the torso (gentle sine wave) when not speaking.
///
/// Drop this onto NPCAgent. It auto-finds children named "Jaw" / "Head" / "Torso"
/// and the AudioSource on the same GameObject. gazeTarget falls back to Camera.main.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AgentAnimation : MonoBehaviour
{
    [Header("References (auto-wired if left empty)")]
    public Transform jaw;
    public Transform head;
    public Transform torso;
    public Transform gazeTarget;

    [Header("Jaw Flap")]
    public bool enableJawFlap = true;
    [Tooltip("Number of audio samples sampled per frame for amplitude.")]
    public int amplitudeSamples = 256;
    [Tooltip("How much the jaw can stretch vertically vs its baseline.")]
    public float jawMaxScaleMultiplier = 3.5f;
    [Tooltip("How fast the jaw chases the current amplitude.")]
    public float jawSmoothing = 18f;
    [Tooltip("Amplifies the RMS reading before clamping to [0,1].")]
    public float jawSensitivity = 6f;
    [Tooltip("RMS below this is treated as silence.")]
    [Range(0f, 0.05f)] public float jawNoiseFloor = 0.005f;

    [Header("Head Gaze")]
    public bool enableGaze = true;
    public float gazeSmoothing = 4f;
    [Tooltip("Max angle the head can rotate from its default forward (degrees).")]
    public float maxGazeAngle = 55f;

    [Header("Idle Breathing")]
    public bool enableBreathing = true;
    [Tooltip("Breaths per second.")] public float breathingRate = 0.35f;
    [Tooltip("Plus or minus this fraction of torso Y scale.")]
    public float breathingAmplitude = 0.02f;

    AudioSource audioSource;
    float[] sampleBuffer;
    float currentJawAmp;
    Vector3 jawBaseScale;
    Vector3 jawBaseLocalPos;
    Vector3 torsoBaseScale;
    Quaternion headBaseRot; // world-space baseline

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (jaw == null) jaw = transform.Find("Jaw");
        if (head == null) head = transform.Find("Head");
        if (torso == null) torso = transform.Find("Torso");
        if (gazeTarget == null && Camera.main != null) gazeTarget = Camera.main.transform;

        sampleBuffer = new float[Mathf.Max(64, amplitudeSamples)];
        if (jaw != null)
        {
            jawBaseScale = jaw.localScale;
            jawBaseLocalPos = jaw.localPosition;
        }
        if (torso != null) torsoBaseScale = torso.localScale;
        if (head != null) headBaseRot = head.rotation;
    }

    void LateUpdate()
    {
        UpdateJaw();
        UpdateHead();
        UpdateBreathing();
    }

    void UpdateJaw()
    {
        if (!enableJawFlap || jaw == null) return;

        float targetAmp = 0f;
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.GetOutputData(sampleBuffer, 0);
            float sumSq = 0f;
            for (int i = 0; i < sampleBuffer.Length; i++)
                sumSq += sampleBuffer[i] * sampleBuffer[i];
            float rms = Mathf.Sqrt(sumSq / sampleBuffer.Length);
            targetAmp = Mathf.Clamp01((rms - jawNoiseFloor) * jawSensitivity);
        }

        currentJawAmp = Mathf.Lerp(currentJawAmp, targetAmp, Time.deltaTime * jawSmoothing);
        float yMul = 1f + currentJawAmp * (jawMaxScaleMultiplier - 1f);
        float newY = jawBaseScale.y * yMul;
        float drop = (newY - jawBaseScale.y) * 0.5f; // keep top edge ~fixed
        jaw.localScale = new Vector3(jawBaseScale.x, newY, jawBaseScale.z);
        jaw.localPosition = jawBaseLocalPos + new Vector3(0f, -drop, 0f);
    }

    void UpdateHead()
    {
        if (!enableGaze || head == null || gazeTarget == null) return;

        // Direction from head to camera (world space).
        Vector3 toTarget = gazeTarget.position - head.position;
        if (toTarget.sqrMagnitude < 0.0001f) return;
        toTarget.Normalize();

        // Default-forward of the agent body (so we clamp gaze around its facing).
        Vector3 bodyForward = transform.forward;

        // Clamp angle between bodyForward and toTarget.
        float angle = Vector3.Angle(bodyForward, toTarget);
        Vector3 clampedDir = toTarget;
        if (angle > maxGazeAngle)
        {
            clampedDir = Vector3.RotateTowards(bodyForward, toTarget, maxGazeAngle * Mathf.Deg2Rad, 0f).normalized;
        }

        Quaternion desired = Quaternion.LookRotation(clampedDir, Vector3.up);
        head.rotation = Quaternion.Slerp(head.rotation, desired, Time.deltaTime * gazeSmoothing);
    }

    void UpdateBreathing()
    {
        if (!enableBreathing || torso == null) return;
        // Skip breathing while speaking - the jaw flap is enough motion.
        bool speaking = audioSource != null && audioSource.isPlaying;
        if (speaking)
        {
            // Slowly lerp torso back to base while speaking.
            torso.localScale = Vector3.Lerp(torso.localScale, torsoBaseScale, Time.deltaTime * 4f);
            return;
        }
        float t = Mathf.Sin(Time.time * breathingRate * Mathf.PI * 2f);
        torso.localScale = new Vector3(
            torsoBaseScale.x,
            torsoBaseScale.y * (1f + t * breathingAmplitude),
            torsoBaseScale.z);
    }
}
