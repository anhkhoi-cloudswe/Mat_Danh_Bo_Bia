using UnityEngine;
using System.Collections;

/// <summary>
/// Âm thanh nền ban ngày / ban đêm của gameplay:
///
/// - Ban ngày  : ve sầu (cicada-calls) phát ngẫu nhiên, nghỉ 5–15 giây giữa các lần.
/// - Ban đêm   : dế kêu (crickets) LOOP liên tục như tiếng nền + cú hú (Night_Owl) phát
///               ngẫu nhiên mỗi 30–90 giây — đúng nhịp thực tế.
///
/// Cả hai AudioSource được tạo runtime; script chỉ cần tồn tại trên bất kỳ
/// GameObject nào trong scene (thường là _GameAudio hoặc tương tự).
/// </summary>
public class GameplayAmbientSound : MonoBehaviour
{
    public enum AmbientMode { Auto, ForceDay, ForceNight }
    public AmbientMode mode = AmbientMode.Auto;

    // ───────────────── AudioSources ─────────────────
    private AudioSource loopSource;   // crickets (loop) hoặc ve sầu (non-loop)
    private AudioSource sparseSource; // Night_Owl (không loop, phát theo interval)

    // ───────────────── Clips ─────────────────
    private AudioClip dayClip;       // Resources/SoundEffects/cicada-calls
    private AudioClip nightLoopClip; // Resources/SoundEffects/crickets
    private AudioClip nightOwlClip;  // Resources/SoundEffects/Night_Owl

    // ───────────────── State ─────────────────
    private bool lastIsDay;
    private bool isInitialized;

    private Coroutine dayCoroutine;
    private Coroutine owlCoroutine;

    // Volume
    private const float DayVolume          = 0.40f;  // ve sầu
    private const float CricketsVolume     = 0.35f;  // dế kêu nền
    private const float OwlVolume          = 0.55f;  // cú hú thỉnh thoảng
    private const float FadeDuration       = 1.5f;   // giây fade khi chuyển phase

    // ─────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────

    private void Start()
    {
        // Tạo AudioSource chính (loop / ve sầu)
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.playOnAwake = false;
        loopSource.loop        = false;
        loopSource.volume      = 0f;

        // Tạo AudioSource phụ dành riêng cho Night Owl
        sparseSource = gameObject.AddComponent<AudioSource>();
        sparseSource.playOnAwake = false;
        sparseSource.loop        = false;
        sparseSource.volume      = 0f;

        // Load clips
        dayClip       = Resources.Load<AudioClip>("SoundEffects/cicada-calls");
        nightLoopClip = Resources.Load<AudioClip>("SoundEffects/crickets");
        nightOwlClip  = Resources.Load<AudioClip>("SoundEffects/Night_Owl");

        if (dayClip       == null) Debug.LogWarning("[GameplayAmbientSound] Missing: SoundEffects/cicada-calls");
        if (nightLoopClip == null) Debug.LogWarning("[GameplayAmbientSound] Missing: SoundEffects/crickets");
        if (nightOwlClip  == null) Debug.LogWarning("[GameplayAmbientSound] Missing: SoundEffects/Night_Owl");

        DetectPhase();
        ApplyPhase(animate: false);
    }

    private void Update()
    {
        bool currentIsDay = DetectPhaseRaw();
        if (!isInitialized)
        {
            isInitialized = true;
            lastIsDay = currentIsDay;
            ApplyPhase(animate: false);
        }
        else if (currentIsDay != lastIsDay)
        {
            lastIsDay = currentIsDay;
            ApplyPhase(animate: true);
        }
    }

    // ─────────────────────────────────────────
    // PHASE DETECTION
    // ─────────────────────────────────────────

    private void DetectPhase()
    {
        lastIsDay     = DetectPhaseRaw();
        isInitialized = true;
    }

    private bool DetectPhaseRaw()
    {
        if (mode == AmbientMode.ForceDay) return true;
        if (mode == AmbientMode.ForceNight) return false;

        var dnc = FindObjectOfType<DayNightCycle>();
        if (dnc != null)
            return (dnc.timeOfDay >= 5f && dnc.timeOfDay < 19f);

        if (GameTimeManager.Instance != null)
            return (GameTimeManager.Instance.CurrentPhase == GameTimeManager.GamePhase.DAY);

        return true; // fallback: ngày
    }

    private void ApplyPhase(bool animate)
    {
        // Dừng mọi coroutine cũ
        if (dayCoroutine != null) { StopCoroutine(dayCoroutine); dayCoroutine = null; }
        if (owlCoroutine != null) { StopCoroutine(owlCoroutine); owlCoroutine = null; }

        if (lastIsDay)
        {
            // Ban ngày
            sparseSource.Stop();
            sparseSource.volume = 0f;
            dayCoroutine = StartCoroutine(DayRoutine(animate));
        }
        else
        {
            // Ban đêm
            loopSource.Stop();
            dayCoroutine = StartCoroutine(NightCricketRoutine(animate));
            owlCoroutine = StartCoroutine(NightOwlRoutine());
        }
    }

    // ─────────────────────────────────────────
    // DAY ROUTINE  (ve sầu ngắt quãng)
    // ─────────────────────────────────────────

    private IEnumerator DayRoutine(bool fadeIn)
    {
        sparseSource.Stop();
        sparseSource.volume = 0f;

        if (dayClip == null) yield break;

        if (fadeIn)
        {
            loopSource.clip   = dayClip;
            loopSource.loop   = false;
            loopSource.volume = 0f;
            loopSource.Play();
            yield return StartCoroutine(FadeVolume(loopSource, DayVolume, FadeDuration));
        }

        while (true)
        {
            // Phát 1 lần ve sầu
            if (!loopSource.isPlaying)
            {
                loopSource.clip   = dayClip;
                loopSource.loop   = false;
                loopSource.volume = DayVolume;
                loopSource.Play();
            }

            // Chờ clip kết thúc
            while (loopSource.isPlaying) yield return null;

            // Nghỉ ngẫu nhiên 5–15 giây
            yield return new WaitForSeconds(Random.Range(5f, 15f));
        }
    }

    // ─────────────────────────────────────────
    // NIGHT ROUTINE #1: Dế cricket loop nền
    // ─────────────────────────────────────────

    private IEnumerator NightCricketRoutine(bool fadeIn)
    {
        if (nightLoopClip == null) yield break;

        loopSource.clip   = nightLoopClip;
        loopSource.loop   = true;
        loopSource.volume = fadeIn ? 0f : CricketsVolume;
        loopSource.Play();

        if (fadeIn)
        {
            yield return StartCoroutine(FadeVolume(loopSource, CricketsVolume, FadeDuration));
        }

        // Tạo hiệu ứng dế kêu lúc to lúc nhỏ tự nhiên
        float baseVol = CricketsVolume;
        while (true)
        {
            float targetVol = baseVol * Random.Range(0.7f, 1.15f);
            float duration = Random.Range(3f, 7f);
            yield return StartCoroutine(FadeVolume(loopSource, targetVol, duration));
        }
    }

    // ─────────────────────────────────────────
    // NIGHT ROUTINE #2: Cú hú ngẫu nhiên
    // ─────────────────────────────────────────

    private IEnumerator NightOwlRoutine()
    {
        if (nightOwlClip == null) yield break;

        // Tránh hú ngay lập tức
        yield return new WaitForSeconds(Random.Range(5f, 10f));

        while (true)
        {
            if (!lastIsDay) // chắc chắn vẫn đang ban đêm
            {
                sparseSource.clip   = nightOwlClip;
                sparseSource.loop   = false;
                sparseSource.volume = OwlVolume;
                sparseSource.Play();

                // Chờ clip phát xong
                yield return new WaitForSeconds(nightOwlClip.length + 0.5f);
            }

            // Theo yêu cầu mới: hú lặp lại sau mỗi 10-15 giây
            yield return new WaitForSeconds(Random.Range(10f, 15f));
        }
    }

    // ─────────────────────────────────────────
    // STOP HELPERS
    // ─────────────────────────────────────────

    private void StopDay(bool animate)
    {
        if (animate && loopSource.isPlaying)
            StartCoroutine(FadeOutAndStop(loopSource, FadeDuration));
        else
        {
            loopSource.Stop();
            loopSource.volume = 0f;
        }
    }

    private void StopSparse(bool animate)
    {
        if (animate && sparseSource.isPlaying)
            StartCoroutine(FadeOutAndStop(sparseSource, FadeDuration));
        else
        {
            sparseSource.Stop();
            sparseSource.volume = 0f;
        }
    }

    // ─────────────────────────────────────────
    // FADE UTILITIES
    // ─────────────────────────────────────────

    private IEnumerator FadeVolume(AudioSource src, float targetVolume, float duration)
    {
        float start   = src.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed      += Time.deltaTime;
            src.volume    = Mathf.Lerp(start, targetVolume, elapsed / duration);
            yield return null;
        }
        src.volume = targetVolume;
    }

    private IEnumerator FadeOutAndStop(AudioSource src, float duration)
    {
        yield return StartCoroutine(FadeVolume(src, 0f, duration));
        src.Stop();
    }
}
