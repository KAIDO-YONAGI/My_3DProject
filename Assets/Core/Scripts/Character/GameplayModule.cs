using Core.Scripts.Character.Interactions;
using Core.Scripts.Character.Presentation;
using UnityEngine;

namespace Core.Scripts.Character
{
    /// <summary>
    /// 玩法模块的装配入口：把模块内的攻击组件绑定到玩家根节点和本地相机。
    /// 网络玩家由组装链路在装配表现后调用 Bind；单机角色在 Start 中按视觉层自行解析。
    /// </summary>
    public class GameplayModule : MonoBehaviour
    {
        [SerializeField] private Attack attack;

        private bool bound;

        private void Awake()
        {
            Debug.Assert(attack != null,
                $"{nameof(attack)} 未指定：模块内的攻击组件不会生效。", this);
        }

        private void Start()
        {
            if (bound) return;

            // 单机角色没有组装链路，直接读取同一角色视觉层的输入相机。
            CharacterVisual visual = GetComponentInParent<CharacterVisual>(true);
            Bind(transform.root, visual != null && visual.InputCamera != null
                ? visual.InputCamera.transform
                : null);
        }

        /// <summary>由组装链路在视觉实例与玩家根就绪后调用。</summary>
        /// <param name="playerRoot">玩家根节点，用于身份判断与自身碰撞体排除。</param>
        /// <param name="aimSource">攻击射线起点与朝向，通常是本地视觉的输入相机。</param>
        public void Bind(Transform playerRoot, Transform aimSource)
        {
            attack.Bind(playerRoot, aimSource);
            bound = true;
        }
    }
}