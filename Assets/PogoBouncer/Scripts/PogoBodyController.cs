using UnityEngine;

public class PogoBodyController : MonoBehaviour
{
    private PogoBouncerAgent _pogoBouncerAgent;


    public void Awake()
    {
        _pogoBouncerAgent = GetComponentInParent<PogoBouncerAgent>();
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (_pogoBouncerAgent != null)
        {
            _pogoBouncerAgent.NotifyBodyHit(collision);
        }
    }
}
