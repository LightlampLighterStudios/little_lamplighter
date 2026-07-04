using UnityEngine;

// in order to scroll an object left and loops it back to the right
// once it has travelled one full width, so it never runs out.


public class ScrollingLoop : MonoBehaviour

{
    public float scrollSpeed = 2f;
    public float loopWidth = 30f;  // set this to match where you place copy B

    void Update()
    {
        transform.position += Vector3.left * scrollSpeed * Time.deltaTime;

        // once this copy has slid one full width past the left,
        // send it two widths to the right, landing behind its twin

        if (transform.position.x <= -loopWidth)
        {
            transform.position += new Vector3(loopWidth * 2f, 0f, 0f);
        }
    }
}