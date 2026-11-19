using System.Collections.Generic;
using Core.Scripts.Camera;
using Core.Scripts.Character;
using Core.Scripts.Character.Presentation;
using Core.Scripts.Networking.Sync;
using UnityEngine;

namespace Core.Scripts.Networking.CharacterPresentation
{
    /// <summary>根据服务器确认的角色编号装配或移除网络玩家的本地与远程视觉表现。</summary>
    class CharacterPresentationService
    {
        private readonly NetworkCharacterManager manager;
        private readonly Dictionary<CharacterIdSync, CharacterPresentationState> presentations;

        public CharacterPresentationService(
            NetworkCharacterManager manager,
            Dictionary<CharacterIdSync, CharacterPresentationState> presentations)
        {
            this.manager = manager;
            this.presentations = presentations;
        }

        /// <summary>
        /// 使用服务器确认的编号，把共用角色 Prefab 装配到网络根下。
        /// 本地拥有者启用相机与输入，其他玩家仅保留视觉与动画。
        /// </summary>
        public void ApplyCharacter(CharacterIdSync player, int requestedCharacterId)
        {
            bool isLocalPlayer = player != null && player.IsLocalPlayerCharacter;
            GameObject prefab;
            bool hasPrefab = manager.TryGetCharacterPrefab(requestedCharacterId, out prefab);
            if (player == null || !manager.isActiveAndEnabled || !hasPrefab)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"无法加载联机角色：编号 {requestedCharacterId} 缺少完整角色 Prefab 配置。",
                    manager);
#endif
                return;
            }

            // 生命周期回调与 SyncVar hook 共用缓存；身份变化且资源相同时仅刷新相机配置。
            if (presentations.TryGetValue(player, out CharacterPresentationState currentInstance)
                && currentInstance != null
                && currentInstance.Instance != null
                && currentInstance.CharacterId == requestedCharacterId
                && currentInstance.SourcePrefab == prefab)
            {
                // 同一实例身份变化：只刷新相机开关与输入参考，不重建视觉和动画驱动。
                if (currentInstance.CameraIsLocal != isLocalPlayer)
                {
                    ConfigureLocalCamera(player, currentInstance);
                    BindCharacter(player, currentInstance.Visual);
                    UpdateGameplayModule(player, currentInstance, isLocalPlayer);
                }

                return;
            }

            CharacterVisual configuredVisual = prefab.GetComponent<CharacterVisual>();
            string issue = "Prefab 根节点缺少 CharacterVisual";
            if (configuredVisual == null || !configuredVisual.TryValidate(isLocalPlayer, out issue))
            {
#if UNITY_EDITOR
                Debug.LogError($"角色 {requestedCharacterId} 的资源 {prefab.name} 配置无效：{issue}。", prefab);
#endif
                return;
            }

            GameObject characterInstance = InstantiateInactiveCharacter(prefab, player.transform);
            // 实例名标出本地或远程身份，便于核对运行时使用的资源。
            characterInstance.name = $"{prefab.name}_{(isLocalPlayer ? "LocalCharacter" : "RemoteCharacter")}";
            // 视觉实例与网络根对齐，世界位置和物理运动由根上的玩家控制器负责。
            characterInstance.transform.localPosition = Vector3.zero;
            characterInstance.transform.localRotation = Quaternion.identity;
            characterInstance.transform.localScale = Vector3.one;

            CharacterVisual visual = characterInstance.GetComponent<CharacterVisual>();
            if (visual == null)
            {
                Object.Destroy(characterInstance);
#if UNITY_EDITOR
                Debug.LogError($"角色 {requestedCharacterId} 的实例缺少 CharacterVisual。", characterInstance);
#endif
                return;
            }

            // 替换旧实例：先解除旧视觉的动画与输入绑定，再销毁它。
            RemoveCharacter(player);
            ConfigureNetworkCharacterComponents(visual);
            DisableEmbeddedGameplayModule(characterInstance);

            // 每类相机组件由视觉资产保存，后续身份配置直接使用当前实例的引用。
            var presentation = new CharacterPresentationState
            {
                Instance = characterInstance,
                CharacterId = requestedCharacterId,
                SourcePrefab = prefab,
                Cameras = visual.Cameras,
                Listeners = visual.Listeners,
                CameraControllers = visual.CameraControllers,
                Visual = visual
            };

            ConfigureLocalCamera(player, presentation);
            presentations[player] = presentation;
            characterInstance.SetActive(true);
            BindCharacter(player, visual);
            UpdateGameplayModule(player, presentation, isLocalPlayer);
        }

        /// <summary>解除网络根的动画与输入绑定，销毁视觉实例并移除玩家的表现状态。</summary>
        public void RemoveCharacter(CharacterIdSync player)
        {
            // 先解除引用，再销毁这些引用所属的视觉实例。
            player.PlayerController.UnbindModel();

            if (presentations.TryGetValue(player, out CharacterPresentationState presentation)
                && presentation != null)
            {
                RemoveGameplayModule(presentation);
                if (presentation.Instance != null)
                {
                    presentation.Instance.SetActive(false);
                    Object.Destroy(presentation.Instance);
                }
            }

            presentations.Remove(player);
        }

        private static GameObject InstantiateInactiveCharacter(GameObject prefab, Transform parent)
        {
            // 在停用层级内实例化，身份配置完成前不执行相机和移动的启用生命周期。
            var staging = new GameObject("CharacterAssembly");
            staging.SetActive(false);
            try
            {
                GameObject instance = Object.Instantiate(prefab, staging.transform);
                instance.SetActive(false);
                instance.transform.SetParent(parent, false);
                return instance;
            }
            finally
            {
                Object.Destroy(staging);
            }
        }

        private static void ConfigureNetworkCharacterComponents(CharacterVisual visual)
        {
            // 视觉实例不承担运动：控制器与碰撞体在激活前停用，运动由网络根负责。
            visual.StandaloneController.enabled = false;
            visual.MovementCollider.enabled = false;
        }

        // 视觉 Prefab 自带的玩法模块只服务单机角色；网络玩家的玩法模块挂在网络根上，这里停用视觉内的副本。
        private static void DisableEmbeddedGameplayModule(GameObject characterInstance)
        {
            GameplayModule[] embeddedModules = characterInstance.GetComponentsInChildren<GameplayModule>(true);
            foreach (GameplayModule module in embeddedModules)
            {
                module.gameObject.SetActive(false);
            }
        }

        // 本地玩家装配玩法模块并绑定攻击参考；远程玩家不装配该模块。
        private void UpdateGameplayModule(
            CharacterIdSync player,
            CharacterPresentationState presentation,
            bool isLocalPlayer)
        {
            if (!isLocalPlayer)
            {
                RemoveGameplayModule(presentation);
                return;
            }

            if (presentation.GameplayModule == null)
            {
                GameObject prefab = manager.GameplayModulePrefab;
                GameObject instance = Object.Instantiate(prefab, player.transform);
                instance.name = prefab.name;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                presentation.GameplayModule = instance.GetComponent<GameplayModule>();
            }

            Transform aimSource = presentation.Visual.InputCamera != null
                ? presentation.Visual.InputCamera.transform
                : null;
            presentation.GameplayModule.Bind(player.transform, aimSource);
        }

        // 远程玩家或角色实例替换时回收玩法模块。
        private static void RemoveGameplayModule(CharacterPresentationState presentation)
        {
            if (presentation.GameplayModule != null)
            {
                Object.Destroy(presentation.GameplayModule.gameObject);
                presentation.GameplayModule = null;
            }
        }

        // 把视觉实例交给网络根控制器：本地玩家带上输入相机，远程玩家只绑定动画。
        private static void BindCharacter(CharacterIdSync player, CharacterVisual visual)
        {
            Transform inputSpace = null;
            if (player.IsLocalPlayerCharacter)
            {
                Debug.Assert(visual.InputCamera != null, "本地玩家视觉缺少输入相机。", visual);
                inputSpace = visual.InputCamera.transform;
            }

            player.PlayerController.BindModel(visual.Animator, inputSpace);
        }

        private static void ConfigureLocalCamera(
            CharacterIdSync player,
            CharacterPresentationState presentation)
        {
            bool enableLocalCamera = player.IsLocalPlayerCharacter;

            foreach (UnityEngine.Camera camera in presentation.Cameras)
            {
                camera.enabled = enableLocalCamera;
            }

            foreach (AudioListener listener in presentation.Listeners)
            {
                listener.enabled = enableLocalCamera;
            }

            // 跟随目标统一指向网络根，启用状态由本地玩家身份控制。
            foreach (ThirdPersonCamera cameraController in presentation.CameraControllers)
            {
                cameraController.SetTarget(player.transform);
                cameraController.enabled = enableLocalCamera;
            }

            presentation.CameraIsLocal = enableLocalCamera;
        }
    }
}