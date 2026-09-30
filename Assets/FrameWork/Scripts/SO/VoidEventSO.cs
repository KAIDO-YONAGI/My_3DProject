using System;
using UnityEngine;

// 命名规范：VoidEventSO 类名不携带语义，同语义复用时资产名必须含语义前缀，
// 如 SlashActionFinishedEventSO、SceneLoadedVoidEventSO；禁止再创建无语义的资产名。
[CreateAssetMenu(fileName = "VoidEventSO", menuName = "Events/VoidEventSO", order = 0)]

public class VoidEventSO : ScriptableObject
{
    public event Action VoidEvent;

    public void OnEventRaised()
    {
        VoidEvent?.Invoke();
    }
}
