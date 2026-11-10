using UnityEngine;
public class SimpleFollow : MonoBehaviour
{
    public Transform target; // 拖入 TrueHead
    void Update() { if (target != null) transform.position = target.position; }
}