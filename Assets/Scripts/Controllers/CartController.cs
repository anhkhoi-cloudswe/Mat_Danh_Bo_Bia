using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CartController : MonoBehaviour
{
    [Header("Movement")]
    [Range(10f, 20000f)]
    [SerializeField] private float moveForce = 150f;
    [Range(1f, 30f)]
    [SerializeField] private float maxSpeed = 8f;
    [Range(0f, 1f)]
    [SerializeField] private float frictionFactor = 0.85f;

    [Header("Steering")]
    [Range(10f, 360f)]
    [SerializeField] private float steerSpeed = 360f;
    [SerializeField] private bool steerOnlyWhenMoving;

    [Header("Input Mapping")]
    [SerializeField] private string moveAxis = "Vertical";
    [SerializeField] private string steerAxis = "Horizontal";

    [Header("Physics Limits")]
    [SerializeField] private bool preventTipping = true;

    [Header("Mount / Dismount")]
    [SerializeField] private bool isRiding;
    [SerializeField] private KeyCode mountKey = KeyCode.F;
    [SerializeField] private float mountRange = 3f;
    [SerializeField] private Vector3 dismountOffset = new Vector3(-1.2f, 0f, 0f);

    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private SimpleProceduralWalker proceduralWalker;
    [SerializeField] private Transform mountPoint;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog;
    [SerializeField] private bool showGizmos = true;

    private Rigidbody rb;
    private float moveInput;
    private float steerInput;
    private float currentSpeed;

    public bool IsRiding
    {
        get => isRiding;
        set
        {
            isRiding = value;
            if (!isRiding)
            {
                StopCart();
            }
        }
    }

    public float CurrentSpeed => currentSpeed;
    public bool IsMoving => currentSpeed > 0.1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (preventTipping)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    private void Start()
    {
        FindPlayerReferences();
        InitializeState();
    }

    private void Update()
    {
        if (Input.GetKeyDown(mountKey))
        {
            ToggleMount();
        }

        if (!isRiding)
        {
            return;
        }

        moveInput = Input.GetAxisRaw(moveAxis);
        steerInput = Input.GetAxisRaw(steerAxis);
    }

    private void FixedUpdate()
    {
        if (!isRiding)
        {
            return;
        }

        ApplyMovement();
        ApplySteering();
        ApplyFriction();
        ClampSpeed();

        currentSpeed = rb.linearVelocity.magnitude;
    }

    public void ToggleMount()
    {
        if (isRiding)
        {
            Dismount();
            return;
        }

        FindPlayerReferences();
        if (playerTransform == null)
        {
            return;
        }

        if (Vector3.Distance(playerTransform.position, transform.position) <= mountRange)
        {
            Mount();
        }
    }

    public void Mount()
    {
        FindPlayerReferences();
        if (playerTransform == null)
        {
            Debug.LogError("[CartController] Player Transform not found.");
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        Transform parent = mountPoint != null ? mountPoint : transform;
        playerTransform.SetParent(parent);
        playerTransform.localPosition = Vector3.zero;
        playerTransform.localRotation = Quaternion.identity;

        if (proceduralWalker != null)
        {
            proceduralWalker.CurrentState = SimpleProceduralWalker.WalkerState.Riding;
        }

        isRiding = true;
        Log("Mounted cart.");
    }

    public void Dismount()
    {
        FindPlayerReferences();
        if (playerTransform == null)
        {
            return;
        }

        playerTransform.SetParent(null);
        playerTransform.position = transform.TransformPoint(dismountOffset);
        playerTransform.rotation = transform.rotation;

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (proceduralWalker != null)
        {
            proceduralWalker.CurrentState = SimpleProceduralWalker.WalkerState.OnFoot;
        }

        isRiding = false;
        StopCart();
        Log("Dismounted cart.");
    }

    public void StopCart()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.MovePosition(position);
        rb.MoveRotation(rotation);
    }

    public void ApplyExternalForce(Vector3 forceVector)
    {
        rb.AddForce(forceVector, ForceMode.Impulse);
    }

    private void InitializeState()
    {
        if (isRiding)
        {
            Mount();
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (proceduralWalker != null)
        {
            proceduralWalker.CurrentState = SimpleProceduralWalker.WalkerState.OnFoot;
        }

        if (playerTransform != null)
        {
            playerTransform.SetParent(null);
        }

        StopCart();
    }

    private void FindPlayerReferences()
    {
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }
        }

        if (playerTransform == null)
        {
            return;
        }

        if (playerMovement == null)
        {
            playerMovement = playerTransform.GetComponent<PlayerMovement>();
        }

        if (proceduralWalker == null)
        {
            proceduralWalker = playerTransform.GetComponentInChildren<SimpleProceduralWalker>();
        }
    }

    private void ApplyMovement()
    {
        if (Mathf.Approximately(moveInput, 0f) || rb.linearVelocity.magnitude >= maxSpeed)
        {
            return;
        }

        rb.AddForce(transform.forward * moveInput * moveForce, ForceMode.Force);
    }

    private void ApplySteering()
    {
        if (Mathf.Approximately(steerInput, 0f))
        {
            return;
        }

        if (steerOnlyWhenMoving && !IsMoving)
        {
            return;
        }

        float rotationAmount = steerInput * steerSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, rotationAmount, 0f));
    }

    private void ApplyFriction()
    {
        if (!Mathf.Approximately(moveInput, 0f))
        {
            return;
        }

        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, frictionFactor * Time.fixedDeltaTime);
    }

    private void ClampSpeed()
    {
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }

    private void Log(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[CartController] {message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        if (rb != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, rb.linearVelocity);
        }
    }
}
