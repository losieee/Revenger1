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
        public int sign;        // Door=+1, MinDoor=-1`
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Door"))
        {
            BindDoorForEnemy(other, +1);
            var leaf = FirstLeafChild(other.transform);
            if (leaf && _doors.TryGetValue(leaf, out var data))
            {
                if (_doorRoutines.TryGetValue(leaf, out var r) && r != null) StopCoroutine(r);
                var co = StartCoroutine(RotateLocal_Smooth(leaf, leaf.localRotation, data.open, doorRotateDuration));
                _doorRoutines[leaf] = co;
                data.isOpen = true; _doors[leaf] = data;
            }
        }
        else if (other.CompareTag("MinDoor"))
        {
            BindDoorForEnemy(other, -1);
            var leaf = FirstLeafChild(other.transform);
            if (leaf && _doors.TryGetValue(leaf, out var data))
            {
                if (_doorRoutines.TryGetValue(leaf, out var r) && r != null) StopCoroutine(r);
                var co = StartCoroutine(RotateLocal_Smooth(leaf, leaf.localRotation, data.open, doorRotateDuration));
                _doorRoutines[leaf] = co;
                data.isOpen = true; _doors[leaf] = data;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Door") || other.CompareTag("MinDoor"))
        {
            var leaf = FirstLeafChild(other.transform);
            if (leaf && _doors.TryGetValue(leaf, out var data))
            {
                if (_doorRoutines.TryGetValue(leaf, out var r) && r != null) StopCoroutine(r);
                var co = StartCoroutine(RotateLocal_Smooth(leaf, leaf.localRotation, data.closed, doorRotateDuration));
                _doorRoutines[leaf] = co;
                data.isOpen = false; _doors[leaf] = data;
            }
        }
    }

    // GameObject 밑에 문 오브젝트 찾기
    static Transform FirstLeafChild(Transform t)
    {
        var cur = t;
        while (cur.childCount > 0) cur = cur.GetChild(0);
        return cur;
    }

    void BindDoorForEnemy(Collider other, int sign)
    {
        var leaf = FirstLeafChild(other.transform);
        if (!leaf) return;

        if (!_doors.TryGetValue(leaf, out var data))
        {
            var e = leaf.localEulerAngles;
            data = new DoorData
            {
                closed = leaf.localRotation,
                open = Quaternion.Euler(e.x, e.y, e.z + sign * doorZDelta),
                isOpen = Mathf.Abs(Mathf.DeltaAngle(e.z, e.z + sign * doorZDelta)) < 5f,
                sign = sign
            };
            _doors[leaf] = data;
        }
        else if (data.sign != sign)
        {
            // 문 종류가 다른 트리거를 동시에 만날 가능성 대비 갱신
            var ce = data.closed.eulerAngles;
            data.open = Quaternion.Euler(ce.x, ce.y, ce.z + sign * doorZDelta);
            data.sign = sign;
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
