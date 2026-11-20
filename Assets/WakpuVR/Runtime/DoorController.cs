
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("문 오브젝트")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("Z축 회전 각도")]
    [SerializeField] private float leftOpenAngle = 90f;
    [SerializeField] private float rightOpenAngle = -90f;

    [Header("문 열림 속도")]
    [SerializeField] private float openSpeed = 90f;

    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;

    private Quaternion leftOpenRotation;
    private Quaternion rightOpenRotation;

    private bool isOpen = false;

    private void Start()
    {
        if (leftDoor != null)
        {
            leftClosedRotation = leftDoor.localRotation;
            leftOpenRotation = leftClosedRotation *
                Quaternion.Euler(0f, 0f, leftOpenAngle);
        }

        if (rightDoor != null)
        {
            rightClosedRotation = rightDoor.localRotation;
            rightOpenRotation = rightClosedRotation *
                Quaternion.Euler(0f, 0f, rightOpenAngle);
        }
    }

    private void Update()
    {
        float step = openSpeed * Time.deltaTime;

        if (leftDoor != null)
        {
            Quaternion target = isOpen
                ? leftOpenRotation
                : leftClosedRotation;

            leftDoor.localRotation = Quaternion.RotateTowards(
                leftDoor.localRotation,
                target,
                step
            );
        }

        if (rightDoor != null)
        {
            Quaternion target = isOpen
                ? rightOpenRotation
                : rightClosedRotation;

            rightDoor.localRotation = Quaternion.RotateTowards(
                rightDoor.localRotation,
                target,
                step
            );
        }
    }

    public void OpenDoor()
    {
        isOpen = true;
    }

    public void CloseDoor()
    {
        isOpen = false;
    }

    public void ToggleDoor()
    {
        isOpen = !isOpen;
    }
}
