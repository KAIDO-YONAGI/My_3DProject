using Core.Scripts.Character.Animation;
using Core.Scripts.Character.Input;
using Core.Scripts.Character.Movement.States;
using UnityEngine;

namespace Core.Scripts.Character.Movement
{
    /// <summary>
    /// 负责角色的实际运动计算。
    /// PlayerCharacterController 每帧传入 CharacterInput，本类驱动 CharacterController，
    /// 再把动画所需结果整理成 CharacterMotion 交给 CharacterAnimator。
    /// </summary>
    public class CharacterMotor
    {
        // owner 用于坐标转换和可选转向，CharacterController 负责碰撞约束后的最终位移。
        private readonly Transform owner;
        private readonly CharacterController characterController;
        private readonly CharacterMovementSettings settings;

        // 水平与竖直速度跨帧保存，才能实现物理上的加减速、重力和跳跃。
        private Vector3 planarVelocity;
        private float verticalVelocity;

        // 非跳跃离地的持续时长，用于判断下坡与台阶造成的悬空是否仍在容忍窗口内。
        private float airborneDuration;
        private readonly CharacterMovementStateMachine stateMachine = new CharacterMovementStateMachine();

        /// <summary>
        /// 绑定运动对象、碰撞控制器和参数配置，并用贴地拉力初始化竖直速度。
        /// </summary>
        public CharacterMotor(Transform owner, CharacterController characterController, CharacterMovementSettings settings)
        {
            this.owner = owner;
            this.characterController = characterController;
            this.settings = settings;
            verticalVelocity = settings.GroundedPull;
        }

        /// <summary>
        /// 按“输入方向 -> 目标速度 -> 重力/跳跃 -> 碰撞位移 -> 动画快照”的顺序完成一帧更新。
        /// </summary>
        public CharacterMotion Tick(CharacterInput input, Transform inputSpace, float deltaTime)
        {
            bool hasMoveInput = input.HasMoveInput;

            // 输入方向优先参考显式 inputSpace，其次参考非角色子节点的主相机。
            Vector3 desiredMoveDirection = ResolveMoveDirection(input.MoveInput, inputSpace);
            if (settings.OrientToMovement && hasMoveInput && desiredMoveDirection.sqrMagnitude > 0.0001f)
            {
                RotateTowards(desiredMoveDirection, deltaTime);
            }

            // 起跳必须读取移动前的贴地状态，否则向上的位移会先把 isGrounded 变为 false。
            bool groundedBeforeMove = characterController.isGrounded;
            if (groundedBeforeMove && verticalVelocity < 0f)
            {
                verticalVelocity = settings.GroundedPull;
            }

            bool jumpStarted = stateMachine.CanJump(groundedBeforeMove) && input.JumpPressed;
            if (jumpStarted)
            {
                verticalVelocity = settings.GetJumpSpeed();
            }

            // 水平速度保留加减速手感；没有输入时目标速度立即归零，但实际速度平滑衰减。
            float targetSpeed = hasMoveInput ? settings.GetTargetSpeed(input.SprintHeld) : 0f;
            Vector3 targetPlanarVelocity = hasMoveInput ? desiredMoveDirection * targetSpeed : Vector3.zero;
            float velocityStep = hasMoveInput ? settings.Acceleration : settings.Deceleration;
            if (!groundedBeforeMove)
            {
                // 空中只降低改变水平速度的能力，不影响重力计算。
                velocityStep *= settings.AirControlMultiplier;
            }

            planarVelocity = Vector3.MoveTowards(planarVelocity, targetPlanarVelocity, velocityStep * deltaTime);
            verticalVelocity += settings.Gravity * deltaTime;

            Vector3 frameMotion = (planarVelocity + Vector3.up * verticalVelocity) * deltaTime;
            CollisionFlags collisionFlags = characterController.Move(frameMotion);

            // 撞到顶部后清除上升速度，避免下一帧继续向天花板施压。
            if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }

            // 动画使用移动后的碰撞结果，因此离地当帧进入 Jump，接触地面当帧进入 Grounded。
            bool groundedAfterMove = (collisionFlags & CollisionFlags.Below) != 0;
            if (groundedAfterMove && verticalVelocity < 0f)
            {
                verticalVelocity = settings.GroundedPull;
            }

            // 非跳跃的短暂离地（下坡、台阶）在容忍窗口内仍按贴地处理；
            // 主动跳跃当帧清零计时，保证起跳立即进入 Rising，不受容忍窗口影响。
            if (groundedAfterMove || jumpStarted)
            {
                airborneDuration = 0f;
            }
            else
            {
                airborneDuration += deltaTime;
            }

            // Animator 的二维方向以角色自身坐标为准，不受世界朝向或相机朝向影响。
            Vector3 localVelocity = owner.InverseTransformDirection(planarVelocity);
            Vector2 localPlanarVelocity = new Vector2(localVelocity.x, localVelocity.z);
            Vector2 localDirection = localPlanarVelocity.sqrMagnitude > 0.0001f ? localPlanarVelocity.normalized : Vector2.zero;

            // 物理速度允许平滑加减速；动画权重直接读取输入，保证按下和松开都立即响应。
            float locomotionWeight01 = Mathf.Clamp01(input.MoveInput.magnitude);
            // 传入上一帧结束时的状态驻留时长；本帧时长由状态机在 Update 内累加。
            CharacterMovementStateId state = stateMachine.Update(
                new CharacterMovementStateContext(
                    groundedBeforeMove,
                    groundedAfterMove,
                    hasMoveInput,
                    input.SprintHeld,
                    jumpStarted,
                    verticalVelocity,
                    airborneDuration < settings.AirborneTolerance,
                    deltaTime,
                    stateMachine.TimeInState));

            return new CharacterMotion(
                state,
                localDirection,
                locomotionWeight01);
        }

        /// <summary>
        /// 把二维输入轴转换为水平世界方向，使前后左右可以相对相机或指定参考物移动。
        /// </summary>
        private Vector3 ResolveMoveDirection(Vector2 moveInput, Transform inputSpace)
        {
            Transform basis = inputSpace;
            if (basis == null)
            {
                // 本地玩家的相机属于共用角色 Prefab；
                // 即使相机是网络根对象的子节点，也必须用它作为前进方向基准。
                UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
                if (mainCamera != null)
                {
                    basis = mainCamera.transform;
                }
            }

            Vector3 forward;
            Vector3 right;
            if (basis != null)
            {
                forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;
            }
            else
            {
                forward = owner.forward;
                right = owner.right;
            }

            // 斜向输入最大保持单位长度，避免斜走速度高于直走。
            Vector3 direction = forward * moveInput.y + right * moveInput.x;
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        /// <summary>
        /// 在配置允许时，让角色以受限角速度朝向期望移动方向。
        /// </summary>
        private void RotateTowards(Vector3 desiredMoveDirection, float deltaTime)
        {
            Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection, Vector3.up);
            owner.rotation = Quaternion.RotateTowards(owner.rotation, targetRotation, settings.RotationSpeed * deltaTime);
        }
    }
}
