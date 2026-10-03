using UnityEngine;
using Mirror;

// 最小联机玩家控制器：客户端拥有输入，服务器执行移动。
// 后续按 docs/plan 阶段六替换为 InputFrame + 固定 Tick 模拟。
/// <summary>
/// 本地玩家读取输入，在客户端通过 CharacterController 执行水平移动、跳跃和重力运动。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerController : NetworkBehaviour
{
    [Header("移动参数（阶段一临时值，阶段一冻结配置后集中到 ScriptableObject）")]
    [Tooltip("水平移动速度，单位为世界单位/秒。移动方向按输入归一化，再乘以此速度。")]
    public float moveSpeed = 5f;
    [Tooltip("接地时按空格赋予的初始竖直速度，单位为世界单位/秒。正值表示向上，跳跃高度由此速度和 Gravity 共同决定。")]
    public float jumpSpeed = 4f;
    [Tooltip("沿世界 Y 轴的重力加速度，单位为世界单位/秒的平方。负值表示向下，每帧累加到竖直速度。")]
    public float gravity = -9.81f;

    // 同一对象上的碰撞与移动组件，Move 通过碰撞约束实际位移。
    private CharacterController controller;
    // 每帧保留的速度状态，当前算法使用 y 分量累计跳跃和重力产生的竖直速度。
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        Vector3 input = new Vector3(
            Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        // 取主相机的水平朝向转换输入，俯仰角保持独立；缺少主相机时使用世界轴输入。
        Vector3 motion = Camera.main != null
            ? Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f) * input
            : input;
        controller.Move(motion.normalized * (moveSpeed * Time.deltaTime));

        if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            velocity.y = jumpSpeed;
        }
        // 先将加速度积分为竖直速度，再以本帧时长计算位移并交给碰撞组件处理。
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
