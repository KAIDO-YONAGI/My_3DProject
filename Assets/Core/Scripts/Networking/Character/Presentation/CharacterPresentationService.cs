using System.Collections.Generic;
using Core.Scripts.Camera.Runtime;
using Core.Scripts.Character.Presentation;
using Core.Scripts.Networking.Character.Sync;
using UnityEngine;

namespace Core.Scripts.Networking.Character.Presentation
{
    internal sealed class CharacterPresentationService
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
                Debug.LogWarning(
                    $"无法加载联机角色：编号 {requestedCharacterId} 缺少完整角色 Prefab 配置。",
                    manager);
                return;
            }

            // 生命周期回调与 SyncVar hook 共用缓存；身份变化且资源相同时仅刷新相机配置。
            if (presentations.TryGetValue(player, out CharacterPresentationState currentInstance)
                && currentInstance != null
                && currentInstance.Instance != null
                && currentInstance.CharacterId == requestedCharacterId
                && currentInstance.SourcePrefab == prefab)
            {
                if (currentInstance.CameraIsLocal != isLocalPlayer)
                {
                    ConfigureLocalCamera(player, currentInstance);
                }

                return;
            }

            CharacterVisual configuredVisual = prefab.GetComponent<CharacterVisual>();
            string issue = "Prefab 根节点缺少 CharacterVisual";
            if (configuredVisual == null || !configuredVisual.TryValidate(isLocalPlayer, out issue))
            {
                Debug.LogError($"角色 {requestedCharacterId} 的资源 {prefab.name} 配置无效：{issue}。", prefab);
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
            if (visual == null || !visual.TryValidate(isLocalPlayer, out issue))
            {
                characterInstance.SetActive(false);
                Object.Destroy(characterInstance);
                Debug.LogError($"角色 {requestedCharacterId} 的资源 {prefab.name} 实例配置无效：{issue}。", manager);
                return;
            }

            manager.RemoveCharacter(player);
            ConfigureNetworkCharacterComponents(visual);

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
            player.PlayerController?.RebindAnimator(visual.Animator);
        }

        /// <summary>解除网络根的动画和输入引用，销毁视觉实例并移除玩家的表现状态。</summary>
        public void RemoveCharacter(CharacterIdSync player)
        {
            if (player == null)
            {
                return;
            }

            // 先解除引用，再销毁这些引用所属的视觉实例。
            player.PlayerController?.RebindInputSpace(null);
            player.PlayerController?.RebindAnimator(null);

            if (presentations.TryGetValue(player, out CharacterPresentationState presentation)
                && presentation != null
                && presentation.Instance != null)
            {
                presentation.Instance.SetActive(false);
                Object.Destroy(presentation.Instance);
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
            visual.StandaloneController.enabled = false;
            visual.MovementCollider.enabled = false;
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

            // 本地玩家以当前实例显式配置的 Camera 作为输入参考，远程玩家清空该引用。
            UnityEngine.Camera localCamera = enableLocalCamera
                ? presentation.Visual.InputCamera
                : null;
            player.PlayerController?.RebindInputSpace(localCamera != null ? localCamera.transform : null);
            presentation.CameraIsLocal = enableLocalCamera;
        }
    }
}
