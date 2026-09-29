using UnityEngine;
using Mirror;

// 最小联机玩家控制器：客户端拥有输入，服务器执行移动。
// 后续按 docs/plan 阶段六替换为 InputFrame + 固定 Tick 模拟。
[RequireComponent(typeof(CharacterController))]
public class NetworkPlayerController : NetworkBehaviour
{
    [Header("移动参数（阶段一临时值，阶段一冻结配置后集中到 ScriptableObject）")]
    public float moveSpeed = 5f;
    public float jumpSpeed = 4f;
    public float gravity = -9.81f;

    private CharacterController controller;
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
        Vector3 motion = Camera.main != null
            ? Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f) * input
            : input;
        controller.Move(motion.normalized * (moveSpeed * Time.deltaTime));

        if (controller.isGrounded && Input.GetKeyDown(KeyCode.Space))
        {
            velocity.y = jumpSpeed;
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
