using UnityEngine;

public class ScrollingObject : MonoBehaviour
{
    // this is to tell us how fast this object moves — it will be set in Inspector
    public float scrollSpeed = 4f;

    void Update()
    {
        // moving this object to the left every frame
        // Time.deltaTime ensures consistent speed regardless of frame rate
        transform.position += Vector3.left * scrollSpeed * Time.deltaTime;

        // if the object has scrolled far off the left side of screen, destroy it
        // This prevents the game from slowing down with thousands of invisible objects
        if (transform.position.x < -20f)
        {
            Destroy(gameObject);
        }
    }
}
