using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed, sensitivity;
    [SerializeField]
    private float collisionDetectionDist = 5f;

    private Vector2 inputVec, rotationVec;
    private bool canMove = true;
    Vector3 moveDir;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    void Update()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");  
        inputVec = new Vector3(x, y);
        inputVec.Normalize();

        float rotationX = Input.GetAxis("Mouse X") * sensitivity;
        float rotationY = Input.GetAxis("Mouse Y") * -sensitivity;
        rotationVec.x += rotationX;
        rotationVec.y += rotationY;
    }

    private void FixedUpdate()
    {
        transform.localEulerAngles = new Vector3(rotationVec.y, rotationVec.x, 0) * Time.fixedDeltaTime;

        Vector3 forwardDir = inputVec.y * transform.forward;
        Vector3 rightDir = inputVec.x * transform.right;
        moveDir = (forwardDir + rightDir).normalized;

        canMove = true;
        if (Physics.Raycast(transform.position, moveDir,  out RaycastHit hit , collisionDetectionDist))
        {
            canMove = false;
        }

        if(canMove)
        {
            transform.position += (moveDir * Time.fixedDeltaTime * moveSpeed);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, moveDir);
    }
}
