using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed, sensitivity;

    private Vector2 inputVec, rotationVec;

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
        transform.position += (inputVec.y * transform.forward * Time.fixedDeltaTime * moveSpeed);
        transform.position += (inputVec.x * transform.right * Time.fixedDeltaTime * moveSpeed);
    }
}
