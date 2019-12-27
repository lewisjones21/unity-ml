using UnityEngine;
using MLAgents;

public class PogoBouncerAgent : Agent
{
    [Header("Pogo Bouncer Options")]

    [SerializeField]
    private Rigidbody _body = null;

    [SerializeField]
    private Rigidbody _foot = null;

    [SerializeField]
    private Transform _target = null;
    private Transform Target => _target != null ? _target : _body.transform;

    [SerializeField]
    private ConfigurableJoint _joint;

    public override void InitializeAgent()
    {
    }

    public override void CollectObservations()
    {
        AddVectorObs(_body.position);
        AddVectorObs(_body.transform.InverseTransformPoint(_foot.position));
        AddVectorObs(_body.transform.InverseTransformPoint(Target.position));
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
            AddReward(-0.05f * penalty);

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
        _body.transform.localPosition = Vector3.up * Random.Range(0.5f, 2.5f);
        _body.velocity = new Vector3(Random.Range(-0.5f, 0.5f),
                                     Random.Range(-0.5f, 0.5f),
                                     Random.Range(-0.5f, 0.5f));
        _body.rotation = Quaternion.Euler(Random.insideUnitSphere * 30f);
        _body.angularVelocity = Vector3.zero;
        _foot.transform.localPosition = new Vector3(Random.Range(-0.5f, 0.5f),
                                                    Random.Range(0f, 0.5f),
                                                    Random.Range(-0.5f, 0.5f));
        _foot.velocity = Vector3.zero;
        _foot.rotation = Quaternion.identity;
        _foot.angularVelocity = Vector3.zero;
        if (_target != null)
        {
            _target.position = _target.parent.position + new Vector3(Random.Range(-3f, 3f),
                                                                     Random.Range(1f, 2.5f),
                                                                     Random.Range(-3f, 3f));
        }
    }

    public override float[] Heuristic()
    {
        var action = new float[3];

        action[0] = Input.GetKey(KeyCode.Space) ? -1.0f : 1.0f;
        action[1] = Input.GetAxis("Horizontal");
        action[2] = Input.GetAxis("Vertical");
        return action;
    }

    public void FixedUpdate()
    {
        // Penalize not being upright
        AddReward(new Vector2(_body.rotation.eulerAngles.x,
                              _body.rotation.eulerAngles.z).magnitude * 0.01f);
        // If the body has nearly hit the floor, reset
        if (_body.position.y < 0.4f)
        {
            AddReward(-1f);
            Done();
        }
    }
}
