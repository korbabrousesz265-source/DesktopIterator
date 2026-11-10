using UnityEngine;

public class HaloRotation : MonoBehaviour
{
    [Tooltip("旋转速度（负数代表顺时针）")]
    public float rotationSpeed = -15f;

    void Update()
    {
        // 围绕 Z 轴缓慢持续旋转
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }
}