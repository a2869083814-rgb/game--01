
using Unity.VisualScripting;
using UnityEngine;

public class 控制 : MonoBehaviour
{
    public float moveSpeed;
    public float walkSpeed = 5f;
    public float runSpeed = 10f;
    public float rotateSpeed = 10f;
    Animator anim;
    Vector3 direction;//传递给动画的moveSpeed
    // Start is called before the first frame update
    void Start()
    {
        anim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        direction = new Vector3(h, 0, v);
        if (Input.GetKey(KeyCode.LeftShift))
        {
            moveSpeed = runSpeed;
        }
        else
        {
            moveSpeed = walkSpeed;
        }
        if (direction.magnitude > 0.1f)
        {
            transform.Translate(direction * moveSpeed * Time.deltaTime, Space.World);

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        }
        UpdateAnim();
    }
    void UpdateAnim()
    {
        if (anim != null)
        {
            anim.SetFloat("moveSpeed", direction.magnitude);
        }
    }
}
