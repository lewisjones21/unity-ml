using UnityEngine;
using MLAgents;

[RequireComponent(typeof(Rigidbody))]
public class CaveCrawlerAgent : Agent
{
    [Header("Cave Crawler Options")]

    [SerializeField]
    private Transform _target = null;
    private Transform Target => HasTarget ? _target : _rb.transform;
    private bool HasTarget => _target != null;

    [SerializeField]
    private TextMesh _rewardText = null;

    [SerializeField]
    private bool _controlled = false;

    [SerializeField]
    private Vector3 _spawnRegionSize = new Vector3(25f, 25f, 25f);

    [SerializeField]
    private float _targetResetDistance = 0f;

    [SerializeField]
    private float _maxViewDistance = 10f;

    [SerializeField]
    private float _raySplayAngle = 45f;

    [SerializeField]
    private float _forwardAccel = 6f;

    [SerializeField]
    private float _sideAccel = 3f;

    [SerializeField]
    private float _angularAccel = 3f;

    [SerializeField]
    private float _speedRewardFactor = 0.1f;

    [SerializeField]
    private float _targetReward = 2f;

    [SerializeField]
    private float _targetFacingRewardFactor = 0.25f;

    [SerializeField]
    private float _targetDistancePenaltyFactor = 0.5f;

    [SerializeField]
    private float _impactPenalty = 3f;

    [SerializeField]
    private float _inputDeltaPenalty = 0.1f;

    private float[] _lastAction = new float[ACTION_LENGTH];

    private Vector3 _lastMousePosition = Vector3.zero;

    private Rigidbody _rb = null;

    private float _runStartTime;

    private const int ACTION_LENGTH = 6;

    private const int SIDE_RAY_COUNT = 6;

    private const float COLLISION_PENALTY_DELAY = 1f;


    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Update()
    {
        UpdateRewardText();

        if (Input.GetKeyDown(KeyCode.C))
        {
            _controlled = !_controlled;
        }
    }

    public void FixedUpdate()
    {
        // Reward moving quickly
        AddReward(_rb.velocity.magnitude * Time.fixedDeltaTime * _speedRewardFactor);
        // Reward facing the target
        AddReward((1f + Vector3.Dot(transform.forward,
                                    (Target.position - transform.position).normalized)) * 0.5f
                  * Time.fixedDeltaTime * _targetFacingRewardFactor);
        // Penalize distance to target
        AddReward(Vector3.Distance(Target.position, transform.position) * Time.fixedDeltaTime
                  * -_targetDistancePenaltyFactor);

        if (HasTarget && Vector3.Distance(_rb.position, Target.position) < 0.5f)
        {
            AddReward(_targetReward);
            Done();
        }
    }

    public void OnCollisionEnter(Collision collision)
    {
        // Allow for some time to uncollide from walls (caused by random spawn position)
        if (Time.time - _runStartTime > COLLISION_PENALTY_DELAY)
        {
            // Penalize impacts
            AddReward(-_impactPenalty);
        }
    }

    public override void InitializeAgent()
    {
        _rb = GetComponent<Rigidbody>();
        UpdateRewardText();
        _runStartTime = Time.time;
    }

    public override void CollectObservations()
    {
        // Observe body configuration
        AddVectorObs(transform.position);
        AddVectorObs(transform.rotation);
        // Observe target position
        AddVectorObs(transform.InverseTransformPoint(Target.position));
        Debug.DrawLine(transform.position, Target.position, Color.green, Time.fixedDeltaTime);
        // Observe nearby objects
        Ray[] viewRays = new Ray[SIDE_RAY_COUNT + 1];
        viewRays[0] = new Ray(transform.position, transform.forward);
        float angle = 0f, increment = 2f * Mathf.PI / SIDE_RAY_COUNT;
        float forwardness = Mathf.Cos(_raySplayAngle);
        float sideness = Mathf.Sin(_raySplayAngle);
        for (int r = 1; r < SIDE_RAY_COUNT + 1; r++)
        {
            angle += increment;
            viewRays[r] = new Ray(transform.position,
                                  transform.rotation * new Vector3(Mathf.Sin(angle) * sideness,
                                                                   Mathf.Cos(angle) * sideness,
                                                                   forwardness));
        }
        RaycastHit hit;
        foreach (Ray ray in viewRays)
        {
            if (Physics.Raycast(ray, out hit, _maxViewDistance))
            {
                AddVectorObs(hit.distance);
                if (Time.timeScale <= 1f)
                {
                    Debug.DrawLine(ray.origin, hit.point, Color.red, Time.fixedDeltaTime);
                }
            }
            else
            {
                AddVectorObs(_maxViewDistance);
                if (Time.timeScale <= 1f)
                {
                    Debug.DrawLine(ray.origin, ray.origin + ray.direction * _maxViewDistance,
                                   Color.red, Time.fixedDeltaTime);
                }
            }
        }
    }

    public override void AgentAction(float[] vectorAction)
    {
        if (vectorAction.Length != ACTION_LENGTH)
        {
            Debug.LogError("Action vector has unexpected length (" + vectorAction.Length
                           + ", should be " + ACTION_LENGTH + ")");
            return;
        }
        // Restrict range of actions and penalize changes
        float penalty = 0f;
        for (var i = 0; i < vectorAction.Length; i++)
        {
            vectorAction[i] = Mathf.Clamp(vectorAction[i], -1f, 1f);
            penalty += Mathf.Abs(vectorAction[i] - _lastAction[i]);
        }
        // Penalize inconsistent actions
        AddReward(penalty * -_inputDeltaPenalty * Time.fixedDeltaTime);
        _lastAction = vectorAction;

        // Apply the given action
        _rb.AddForce(_rb.rotation * new Vector3(vectorAction[0] * _sideAccel,
                                                vectorAction[1] * _sideAccel,
                                                vectorAction[2] * _forwardAccel),
                     ForceMode.Acceleration);
        _rb.AddRelativeTorque(
            new Vector3(vectorAction[3], vectorAction[4], vectorAction[5]) * _angularAccel,
            ForceMode.Acceleration);
    }

    public override void AgentOnDone()
    {
    }

    public override void AgentReset()
    {
        transform.position = Vector3.Scale(new Vector3(
            Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f), _spawnRegionSize);
        transform.rotation = Random.rotationUniform;
        if (HasTarget)
        {
            if (_targetResetDistance <= 0f)
            {
                _target.position = Vector3.Scale(new Vector3(
                    Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f),
                                                 _spawnRegionSize);
            }
            else
            {
                do
                {
                    _target.position = transform.position + Random.onUnitSphere;
                }
                while (_target.position.x < -_spawnRegionSize.x * 0.5f
                       || _target.position.x > _spawnRegionSize.x * 0.5f
                       || _target.position.y < -_spawnRegionSize.y * 0.5f
                       || _target.position.y > _spawnRegionSize.y * 0.5f
                       || _target.position.z < -_spawnRegionSize.z * 0.5f
                       || _target.position.z > _spawnRegionSize.z * 0.5f);
            }
        }
    }

    public override float[] Heuristic()
    {
        var action = new float[ACTION_LENGTH];
        if (_controlled)
        {
            // Implement QWEASD 3D controls
            action[2] += Input.GetKey(KeyCode.W) ? 1f : 0f;
            action[2] -= Input.GetKey(KeyCode.S) ? 1f : 0f;
            action[0] += Input.GetKey(KeyCode.D) ? 1f : 0f;
            action[0] -= Input.GetKey(KeyCode.A) ? 1f : 0f;
            action[1] += Input.GetKey(KeyCode.Q) ? 1f : 0f;
            action[1] -= Input.GetKey(KeyCode.E) ? 1f : 0f;
            // Implement mouse rotation controls
            if (Input.GetMouseButton(1))
            {
                action[3] = (Input.mousePosition - _lastMousePosition).y;
                action[4] = (Input.mousePosition - _lastMousePosition).x;
            }
            action[5] = Input.mouseScrollDelta.y;
            _lastMousePosition = Input.mousePosition;
        }
        else
        {
            if (HasTarget)
            {
                // Accelerate towards the target
                Vector3 targetDir = _target.position - transform.position;
                action[0] = Mathf.Max(0f, Mathf.Min(1f, Vector3.Dot(targetDir, transform.right)));
                action[1] = Mathf.Max(0f, Mathf.Min(1f, Vector3.Dot(targetDir, transform.up)));
                action[2] = Mathf.Max(0f, Mathf.Min(1f, Vector3.Dot(targetDir, transform.forward)));
                // Rotate towards the target
                Quaternion targetRotationDelta = Quaternion.LookRotation(
                    targetDir, -Physics.gravity) * Quaternion.Inverse(_rb.rotation);
                action[3] += targetRotationDelta.x;
                action[4] += targetRotationDelta.y;
                action[5] += targetRotationDelta.z;
            }
            else
            {
                action[2] = 1f;
            }
        }
        return action;
    }

    private void UpdateRewardText()
    {
        if (_rewardText != null)
        {
            _rewardText.text = GetCumulativeReward().ToString("F2");
        }
    }
}
