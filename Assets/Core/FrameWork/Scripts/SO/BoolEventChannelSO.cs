using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BoolEventChannel", menuName = "Events/Bool Event Channel")]
public class BoolEventChannelSO : ScriptableObject
{
    public event Action<bool> OnEventRaised;

    public void Raise(bool value)
    {
        OnEventRaised?.Invoke(value);
    }
}
