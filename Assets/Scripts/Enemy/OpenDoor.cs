using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenDoor : MonoBehaviour
{
    [Header("문")]
    [SerializeField] private float doorRotateDuration = 0.6f;
    [SerializeField] private float doorZDelta = 90f;

    private readonly Dictionary<Transform, Coroutine> _doorRoutines = new();
    private readonly Dictionary<Transform, DoorData> _doors = new();

    struct DoorData
    {
        public Quaternion closed;
        public Quaternion open;
        public bool isOpen;
        public int sign;        // Door=+1, MinDoor=-1
    }

    void Start()
    {
        // 씬에 있는 모든 Door / MinDoor를 한 번 등록해서
        // "닫힌 회전값"을 초기 상태로 고정해 둔다.
        foreach (var go in GameObject.FindGameObjectsWithTag("Door"))
        {
            var col = go.GetComponent<Collider>();
            if (col) BindDoorForEnemy(col, +1, forceInit: true);
        }

        foreach (var go in GameObject.FindGameObjectsWithTag("MinDoor"))
        {
            var col = go.GetComponent<Collider>();
            if (col) BindDoorForEnemy(col, -1, forceInit: true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        int sign;
        if (other.CompareTag("Door")) sign = +1;
        else if (other.CompareTag("MinDoor")) sign = -1;
        else return;

        BindDoorForEnemy(other, sign);

        var leaf = FirstLeafChild(other.transform);
        if (leaf && _doors.TryGetValue(leaf, out var data))
        {
            if (Quaternion.Angle(leaf.localRotation, data.open) < 1f)
            {
                data.isOpen = true;
                _doors[leaf] = data;
                return;
            }

            if (_doorRoutines.TryGetValue(leaf, out var r) && r != null)
                StopCoroutine(r);

            var co = StartCoroutine(
                RotateLocal_Smooth(leaf, leaf.localRotation, data.open, doorRotateDuration)
            );
            _doorRoutines[leaf] = co;

            data.isOpen = true;
            _doors[leaf] = data;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Door") && !other.CompareTag("MinDoor")) return;

        var leaf = FirstLeafChild(other.transform);
        if (leaf && _doors.TryGetValue(leaf, out var data))
        {
            if (Quaternion.Angle(leaf.localRotation, data.closed) < 1f)
            {
                data.isOpen = false;
                _doors[leaf] = data;
                return;
            }

            if (_doorRoutines.TryGetValue(leaf, out var r) && r != null)
                StopCoroutine(r);

            var co = StartCoroutine(
                RotateLocal_Smooth(leaf, leaf.localRotation, data.closed, doorRotateDuration)
            );
            _doorRoutines[leaf] = co;

            data.isOpen = false;
            _doors[leaf] = data;
        }
    }

    // GameObject 밑에 문 오브젝트 찾기
    static Transform FirstLeafChild(Transform t)
    {
        var cur = t;
        while (cur.childCount > 0) cur = cur.GetChild(0);
        return cur;
    }

    void BindDoorForEnemy(Collider other, int sign, bool forceInit = false)
    {
        var leaf = FirstLeafChild(other.transform);
        if (!leaf) return;

        if (!_doors.TryGetValue(leaf, out var data) || forceInit)
        {
            var e = leaf.localEulerAngles;

            data = new DoorData
            {
                closed = leaf.localRotation,
                open = Quaternion.Euler(e.x, e.y, e.z + sign * doorZDelta),
                isOpen = false,
                sign = sign
            };
            _doors[leaf] = data;
        }
    }

    IEnumerator RotateLocal_Smooth(Transform tr, Quaternion from, Quaternion to, float duration)
    {
        float t = 0f;
        duration = Mathf.Max(0.0001f, duration);
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            tr.localRotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }
        tr.localRotation = to;
    }
}
