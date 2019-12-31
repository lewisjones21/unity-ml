using UnityEngine;
using MLAgents;

public class PogoBouncerAgent : Agent
{
    [Header("Pogo Bouncer Options")]

    [SerializeField]
    private Rigidbody _body = null;

    [SerializeField]
    private Rigidbody _leg = null;

    [SerializeField]
    private Transform _target = null;
    private Transform Target => _target != null ? _target : _body.transform;

    [SerializeField]
    private Transform _groundReference = null;

    [SerializeField]
    private ConfigurableJoint _joint = null;

    [SerializeField]
    private Vector3 _legResetOffset = Vector3.down;

    [SerializeField]
    private TextMesh _rewardText = null;

    [SerializeField]
    private bool _controlled = false;

    [SerializeField]
    private float _survivalRewardFactor = 1f;

    [SerializeField]
    private float _heightRewardFactor = 1f;

    [SerializeField]
    private float _tiltPenaltyFactor = 2f;

    [SerializeField]
    private float _inputPenaltyFactor = 1f;

    [SerializeField]
    private float _fallPenalty = 5f;

    [SerializeField]
    [Range(0f, 0.1f)]
    private float _angularCheatFactor = 0f;

    [SerializeField]
    private float _penalizeTiltLimit = 30f;

    [SerializeField]
    private float _resetHeightMin = 0f;

    [SerializeField]
    private float _resetHeightMax = 1f;

    [SerializeField]
    private float _resetSpeedMax = 0.5f;

    [SerializeField]
    private float _resetAngleMax = 30f;

    private float _contactStartTime;
    private Vector3 _contactPosition = Vector3.zero, _contactNormal = Vector3.zero;


    public override void InitializeAgent()
    {
        UpdateRewardText();
    }

    public override void CollectObservations()
    {
        // Observe body configuration
        AddVectorObs(GetHeightAboveGround());
        AddVectorObs(_body.rotation);
        // Observe relative leg position
        AddVectorObs(_body.transform.InverseTransformPoint(_leg.position) - _legResetOffset);
        // Observe contact information
        if (_contactStartTime >= 0f)
        {
            AddVectorObs(Vector3.Dot(Physics.gravity.normalized, _contactNormal));
        }
        else
        {
            AddVectorObs(0f);
        }
    }

    public override void AgentAction(float[] vectorAction)
    {
        if (vectorAction.Length == 3)
        {
            // Restrict range of actions
            float penalty = 0f;
            for (var i = 0; i < vectorAction.Length; i++)
            {
                vectorAction[i] = Mathf.Clamp(vectorAction[i], -1f, 1f);
                penalty += vectorAction[i] * vectorAction[i];
            }
            penalty /= vectorAction.Length;
            // Reward smaller actions
            AddReward(penalty * -_inputPenaltyFactor * Time.fixedDeltaTime);

            // Apply action vector to pogo joint parameters
            _joint.targetPosition = new Vector3(0f, 0f, vectorAction[0] * _joint.linearLimit.limit);
            _joint.targetRotation = Quaternion.Euler(
                vectorAction[1] * _joint.highAngularXLimit.limit,
                vectorAction[2] * _joint.angularYLimit.limit,
                0f);
        }
        else
        {
            Debug.LogError("Action vector has unexpected length");
        }
    }

    public override void AgentOnDone()
    {
    }

    public override void AgentReset()
    {
        Vector3 up = -Physics.gravity.normalized;
        _body.rotation = Quaternion.LookRotation(Vector3.forward, up)
            * Quaternion.Euler(Random.insideUnitSphere * _resetAngleMax);
        _body.angularVelocity = Vector3.zero;
        _body.position = (_groundReference?.position ?? _body.transform.parent.position)
            + up * Random.Range(_resetHeightMin, _resetHeightMax)
            - _body.rotation * _legResetOffset;
        _body.velocity = Random.insideUnitSphere * _resetSpeedMax;

        _leg.position = _body.position + _body.rotation * _legResetOffset;
        _leg.velocity = _body.velocity;
        _leg.rotation = _body.rotation;
        _leg.angularVelocity = _body.angularVelocity;
        if (_target != null)
        {
            _target.position = _target.parent.position + new Vector3(Random.Range(-3f, 3f),
                                                                     Random.Range(1f, 2.5f),
                                                                     Random.Range(-3f, 3f));
        }

        ResetContactInfo();
    }

    public override float[] Heuristic()
    {
        var action = new float[3];
        if (_controlled)
        {
            action[0] = Input.GetKey(KeyCode.Space) ? -1.0f : 1.0f;
            action[1] = Input.GetAxis("Horizontal");
            action[2] = Input.GetAxis("Vertical");
        }
        else
        {
            action[0] = _contactStartTime >= 0f ? 0f : 1.0f;
        }
        return action;
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
        // Reward being alive
        AddReward(Time.fixedDeltaTime * _survivalRewardFactor);
        // Reward being up high
        AddReward(Time.fixedDeltaTime * _heightRewardFactor * GetHeightAboveGround());
        // Penalize not being upright
        float tiltAngle = Vector3.Angle(-Physics.gravity, _body.transform.up);
        float penalizeTiltLimit = _penalizeTiltLimit;
        if (_contactStartTime >= 0f)
        {
            // Be more harsh about tilt whilst touching the ground
            penalizeTiltLimit = 0f;
        }
        AddReward((tiltAngle - penalizeTiltLimit) / 180f
                  * Time.fixedDeltaTime * -_tiltPenaltyFactor);

        // If the body has fallen below the floor, reset
        if (GetHeightAboveGround() < 0f)
        {
            AddReward(-_fallPenalty);
            Done();
        }

        if (_angularCheatFactor > 0f)
        {
            // Cheat the angular properties back to an upright configuration to aid learning
            float xRotation = _body.rotation.eulerAngles.x;
            xRotation = xRotation > 180f ? xRotation - 360f : xRotation;
            float zRotation = _body.rotation.eulerAngles.z;
            zRotation = zRotation > 180f ? zRotation - 360f : zRotation;
            _body.rotation = Quaternion.Euler(xRotation * (1f - _angularCheatFactor),
                                              _body.rotation.eulerAngles.y,
                                              zRotation * (1f - _angularCheatFactor));
            _body.angularVelocity *= 1f - _angularCheatFactor;
        }
    }

    public void NotifyBodyHit(Collision collision)
    {
        // If the body has nearly hit the floor
        if (collision.gameObject.tag == "ground")
        {
            AddReward(-_fallPenalty);
            Done();
        }
    }

    public void SetContactInfo(Vector3 position, Vector3 normal)
    {
        if (_contactStartTime < 0f)
        {
            _contactStartTime = Time.fixedTime;
        }
        _contactPosition = position;
        _contactNormal = normal;
        Debug.DrawRay(_contactPosition, _contactNormal, Color.red, Time.fixedDeltaTime);
    }
    public void ResetContactInfo()
    {
        _contactStartTime = -1f;
        _contactPosition = Vector3.zero;
        _contactNormal = Vector3.zero;
        if (_groundReference != null)
        {
            Debug.DrawRay(_groundReference.position, -_groundReference.up, Color.red, 0.1f);
        }
        else
        {
            Debug.DrawRay(_contactPosition, Vector3.down, Color.red, 0.1f);
        }
    }

    private float GetHeightAboveGround()
    {
        if (_groundReference != null)
        {
            return Vector3.Dot(_body.position - _groundReference.position,
                               -Physics.gravity.normalized);
        }
        return _body.position.y;
    }

    private void UpdateRewardText()
    {
        if (_rewardText != null)
        {
            _rewardText.text = GetCumulativeReward().ToString("F2");
        }
    }
}
