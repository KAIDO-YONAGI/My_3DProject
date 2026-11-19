using Core.Scripts.Character.Stats;
using Core.Scripts.SO;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public class DeathTrigger : MonoBehaviour
{
    [SerializeField] private PlayerDamagedEventSO playerDamagedEvent;


    private void OnTriggerExit(Collider obj)
    {
        if(obj.CompareTag("Player"))
            playerDamagedEvent.OnPlayerDamaged(StatsManager.Instance.Stats.MaxHealth);
    }
}
