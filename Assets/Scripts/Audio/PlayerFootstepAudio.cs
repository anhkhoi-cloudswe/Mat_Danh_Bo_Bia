using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerFootstepAudio : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private AudioSource audioSource;
    
    private AudioClip walkingClip;
    private AudioClip runningClip;
    
    private void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.volume = 0.6f;

        // Load footstep sounds
        walkingClip = Resources.Load<AudioClip>("SoundEffects/Walking");
        runningClip = Resources.Load<AudioClip>("SoundEffects/Running");

        if (walkingClip == null) Debug.LogWarning("[PlayerFootstepAudio] Missing Walking audio clip in Resources/SoundEffects/Walking");
        if (runningClip == null) Debug.LogWarning("[PlayerFootstepAudio] Missing Running audio clip in Resources/SoundEffects/Running");
    }

    private void Update()
    {
        if (playerMovement == null || audioSource == null) return;

        // Check if moving on ground
        bool isMovingOnGround = playerMovement.CurrentSpeed > 0.1f;
        
        // Try to check if CharacterController is grounded
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null && !cc.isGrounded)
        {
            isMovingOnGround = false;
        }

        if (isMovingOnGround)
        {
            // Determine if running
            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            AudioClip targetClip = isRunning ? runningClip : walkingClip;

            if (targetClip != null)
            {
                if (audioSource.clip != targetClip)
                {
                    audioSource.clip = targetClip;
                    audioSource.Play();
                }
                else if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }
        }
        else
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }
}
