using UnityEngine;

public class PogoFootController : MonoBehaviour
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
            _pogoBouncerAgent.SetContactInfo(collision.contacts[0].point,
                                             collision.contacts[0].normal);
        }
    }

    public void OnCollisionStay(Collision collision)
    {
        if (_pogoBouncerAgent != null)
        {
            _pogoBouncerAgent.SetContactInfo(collision.contacts[0].point,
                                             collision.contacts[0].normal);
        }
    }

    public void OnCollisionExit(Collision collision)
    {
        if (_pogoBouncerAgent != null)
        {
            _pogoBouncerAgent.ResetContactInfo();
        }
    }
}
